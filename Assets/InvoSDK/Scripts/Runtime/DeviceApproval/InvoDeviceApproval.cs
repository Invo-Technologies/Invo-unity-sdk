using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace InvoSDK
{
    /// <summary>How a device approval ended. Only <see cref="Settled"/> and <see cref="AlreadySettled"/> mean the step happened.</summary>
    public enum InvoDeviceApprovalOutcome
    {
        /// <summary>Approved on the phone AND settled: the transaction moved past this step.</summary>
        Settled,
        /// <summary>The settle call was accepted but held (guardian, risk review, recipient check). Nothing refused, nothing moved yet.</summary>
        Held,
        /// <summary>The transaction was already past this step (an earlier attempt landed).</summary>
        AlreadySettled,
        /// <summary>The transaction is no longer at this step for another reason (expired, refunded…). Read its status.</summary>
        NotPending,
        /// <summary>The player declined, on the phone or on the match-code prompt.</summary>
        Denied,
        /// <summary>The grant lapsed before it was approved. Start again.</summary>
        Expired,
        /// <summary>The game or the player cancelled. The grant simply expires on the server.</summary>
        Cancelled
    }

    /// <summary>Result of <see cref="InvoDeviceApproval.RunAsync"/>.</summary>
    public sealed class InvoDeviceApprovalResult
    {
        public InvoDeviceApprovalOutcome Outcome;
        /// <summary>The settle response, when the settle call was made.</summary>
        public DeviceApprovalSettleResponse Settlement;
        public DeviceApprovalPollResponse LastPoll;

        public bool Succeeded
        {
            get { return Outcome == InvoDeviceApprovalOutcome.Settled || Outcome == InvoDeviceApprovalOutcome.AlreadySettled; }
        }
    }

    /// <summary>
    /// The approval flow that replaces SMS codes. One call runs every stage for one transaction:
    /// <list type="number">
    /// <item><c>begin</c> — Invo returns a short-lived grant for this transaction and flow.</item>
    /// <item>Show it — a QR on desktop, Steam and consoles; the system browser on iOS and Android.
    /// The player approves on their phone with a passkey; the first time, the game screen confirms
    /// a match code instead of any text message.</item>
    /// <item><c>poll</c> until approved, denied or expired.</item>
    /// <item><b>Settle</b> — the approve / confirm-receipt call that actually moves the money.
    /// An approved poll alone moves nothing.</item>
    /// </list>
    /// Everything uses the player token; the game secret is never involved.
    /// </summary>
    public static class InvoDeviceApproval
    {
        /// <summary>"Or go to … and enter …" for a player who cannot scan.</summary>
        public static string ManualEntryText(string verificationUri, string userCode)
        {
            if (string.IsNullOrEmpty(userCode))
                return string.Empty;
            if (string.IsNullOrEmpty(verificationUri))
                return "Code: " + userCode;
            return "Or go to " + verificationUri + " and enter " + userCode;
        }

        /// <summary>The headline shown above the QR for a flow.</summary>
        public static string Headline(string flow, string channel)
        {
            bool qr = channel == InvoApprovalChannel.Qr;
            switch (flow)
            {
                case InvoApprovalFlow.SendReceipt:
                case InvoApprovalFlow.TransferReceipt:
                    return qr ? "Scan with your phone to collect" : "Confirm on the INVO page to collect";
                default:
                    return qr ? "Scan with your phone to approve" : "Approve on the INVO page that just opened";
            }
        }

        /// <summary>
        /// Runs the whole approval for <paramref name="transactionId"/>.
        /// <paramref name="view"/> null uses <see cref="InvoDeviceApprovalOverlay"/>;
        /// <paramref name="channel"/> null picks the system browser on phones and a QR elsewhere.
        /// <para>
        /// Throws <see cref="InvoApiException"/> for refusals (<c>invalid_grant</c>,
        /// <c>DEVICE_APPROVAL_ALREADY_PENDING</c>, <c>PASSKEY_RECOVERY_COOLDOWN</c>, a 403 for the wrong
        /// player, …) and after repeated transport failures. A failure thrown by the settle call
        /// itself is AMBIGUOUS about the money: read the transaction status before telling the
        /// player it failed, and never start a replacement transaction automatically.
        /// </para>
        /// </summary>
        public static async Task<InvoDeviceApprovalResult> RunAsync(
            string transactionId,
            string flow,
            IInvoDeviceApprovalView view = null,
            CancellationToken cancellationToken = default,
            string channel = null)
        {
            APIManager api = APIManager.Instance;
            if (api == null)
                throw new InvalidOperationException("APIManager is not in the scene.");
            if (string.IsNullOrWhiteSpace(transactionId))
                throw new ArgumentException("transactionId is required.", nameof(transactionId));
            if (!InvoApprovalFlow.IsValid(flow))
                throw new ArgumentException("Unknown approval flow: " + flow, nameof(flow));

            channel = channel ?? InvoDeviceApprovalCore.DefaultChannel(Application.isMobilePlatform);
            view = view ?? InvoDeviceApprovalOverlay.Instance;

            using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                Texture2D qrTexture = null;
                TaskCompletionSource<bool> wake = null;
                Action onPageFinished = () => { if (wake != null) wake.TrySetResult(true); };
                InvoHostedApproval.ApprovalPageFinished += onPageFinished;
                var result = new InvoDeviceApprovalResult();

                try
                {
                    DeviceApprovalGrant grant = await api.BeginDeviceApprovalAsync(transactionId, flow, channel);
                    if (grant == null || string.IsNullOrEmpty(grant.device_code) ||
                        string.IsNullOrEmpty(grant.verification_uri_complete))
                        throw new InvoApiException("The device-approval grant was incomplete.", 0,
                                                   "SDK_RESPONSE_PARSE_ERROR", null, null, null);

                    string headline = Headline(flow, channel);
                    Action cancel = () => { try { cancelled.Cancel(); } catch (ObjectDisposedException) { } };
                    if (channel == InvoApprovalChannel.AppBrowser)
                    {
                        string uri = grant.verification_uri_complete;
                        InvoHostedApproval.OpenHostedApproval(uri, api.GameId);
                        view.ShowBrowserWaiting(grant.user_code, headline,
                                                () => InvoHostedApproval.OpenHostedApproval(uri, api.GameId), cancel);
                    }
                    else
                    {
                        qrTexture = InvoQrTexture.Create(grant.verification_uri_complete);
                        view.ShowQr(qrTexture, grant.user_code, grant.verification_uri, headline, cancel);
                    }

                    string deviceCode = grant.device_code;
                    int interval = InvoDeviceApprovalCore.InitialInterval(grant.interval);
                    int transientFailures = 0;

                    while (true)
                    {
                        wake = new TaskCompletionSource<bool>();
                        if (!await WaitAsync(interval, wake.Task, cancelled.Token))
                        {
                            result.Outcome = InvoDeviceApprovalOutcome.Cancelled;
                            return result;
                        }

                        DeviceApprovalPollResponse poll;
                        try
                        {
                            poll = await api.PollDeviceApprovalAsync(deviceCode);
                        }
                        catch (InvoApiException ex) when (InvoDeviceApprovalCore.IsTransientPollFailure(ex.StatusCode) &&
                                                          ++transientFailures < InvoDeviceApprovalCore.MaxTransientPollFailures)
                        {
                            if (ex.RetryAfterSeconds.HasValue && ex.RetryAfterSeconds.Value > interval)
                                interval = ex.RetryAfterSeconds.Value;
                            continue;
                        }

                        if (cancelled.IsCancellationRequested)
                        {
                            result.Outcome = InvoDeviceApprovalOutcome.Cancelled;
                            return result;
                        }

                        transientFailures = 0;
                        result.LastPoll = poll;
                        string status = poll != null ? poll.status : null;

                        if (status == InvoDevicePollStatus.AuthorizationPending)
                        {
                            interval = InvoDeviceApprovalCore.IntervalAfterPending(interval, poll.interval);
                            InvoHostedApproval.ApplyEnrollmentState(poll.enrollment,
                                decision => _ = AnswerEnrollmentAsync(api, deviceCode, decision));
                        }
                        else if (status == InvoDevicePollStatus.SlowDown)
                        {
                            interval = InvoDeviceApprovalCore.IntervalAfterSlowDown(interval, poll.interval);
                        }
                        else if (status == InvoDevicePollStatus.ExpiredToken)
                        {
                            result.Outcome = InvoDeviceApprovalOutcome.Expired;
                            return result;
                        }
                        else if (status == InvoDevicePollStatus.AccessDenied)
                        {
                            result.Outcome = InvoDeviceApprovalOutcome.Denied;
                            return result;
                        }
                        else if (status == InvoDevicePollStatus.Approved)
                        {
                            InvoHostedApproval.HideEnrollmentPrompt();
                            view.ShowStatus("Approved on your phone. Finishing…");

                            // THE STEP THE POLL DOES NOT DO. Not cancellable: once sent, its
                            // outcome has to be read, not abandoned.
                            DeviceApprovalSettleResponse settle = await api.SettleDeviceApprovalAsync(transactionId, flow, deviceCode);
                            result.Settlement = settle;
                            if (settle != null && settle.status == InvoStatus.NotPending)
                                result.Outcome = settle.already_settled
                                    ? InvoDeviceApprovalOutcome.AlreadySettled
                                    : InvoDeviceApprovalOutcome.NotPending;
                            else if (settle != null && settle.IsHold)
                                result.Outcome = InvoDeviceApprovalOutcome.Held;
                            else
                                result.Outcome = InvoDeviceApprovalOutcome.Settled;
                            return result;
                        }
                        // Anything else: an outcome this SDK does not know. Keep polling; the
                        // grant's own expiry ends the loop.
                    }
                }
                finally
                {
                    InvoHostedApproval.ApprovalPageFinished -= onPageFinished;
                    InvoHostedApproval.HideEnrollmentPrompt();
                    if (channel == InvoApprovalChannel.AppBrowser && cancelled.IsCancellationRequested)
                        InvoHostedApproval.Cancel();
                    view.Hide();
                    if (qrTexture != null)
                        UnityEngine.Object.Destroy(qrTexture);
                }
            }
        }

        /// <summary>Waits <paramref name="seconds"/>, cut short by <paramref name="wake"/>. False when cancelled.</summary>
        private static async Task<bool> WaitAsync(int seconds, Task wake, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return false;
            try
            {
                Task delay = Task.Delay(TimeSpan.FromSeconds(seconds), token);
                await Task.WhenAny(delay, wake);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            return !token.IsCancellationRequested;
        }

        /// <summary>
        /// Forwards the match-code decision. Best effort: a prompt the grant has outlived, or that the
        /// backup email answered first, is moot and the next poll reports the real state. Anything
        /// else is logged, never thrown into UI code.
        /// </summary>
        private static async Task AnswerEnrollmentAsync(APIManager api, string deviceCode, InvoEnrollmentDecision decision)
        {
            try
            {
                await api.ConfirmDeviceEnrollmentAsync(deviceCode, decision == InvoEnrollmentDecision.Approve);
            }
            catch (InvoApiException ex) when (InvoDeviceApprovalCore.IsMootEnrollmentAnswer(ex.ErrorCode))
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InvoSDK] Could not record the match-code answer: " + ex.Message);
            }
        }
    }
}
