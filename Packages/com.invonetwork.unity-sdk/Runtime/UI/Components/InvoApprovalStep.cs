using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static InvoSDK.TransferResponse;

namespace InvoSDK.UI
{
    /// <summary>What the sender's verification step ended with, in panel terms.</summary>
    public sealed class InvoApprovalStepResult
    {
        /// <summary>True: the sender's approval landed and the transaction is awaiting collection (or done).</summary>
        public bool Approved;
        /// <summary>The player stopped it, or the panel closed.</summary>
        public bool Cancelled;
        /// <summary>True when starting the approval again for the SAME transaction makes sense
        /// (declined, the code lapsed). False when the transaction itself is over.</summary>
        public bool CanRetry;
        /// <summary>Player-facing explanation when not approved.</summary>
        public string Message;
        /// <summary>Transfers only: the self-claim code returned by the approve call.</summary>
        public string ClaimCode;
        public string ClaimCodeExpiresAt;
    }

    /// <summary>
    /// The sender's verification step shared by the send and transfer panels: the phone approval
    /// (QR or system browser) plus the waits around it — a guardian hold before it can start, a
    /// guardian or review hold after it, and the status read that settles an ambiguous answer.
    /// It never moves money on its own judgement; every "approved" here is read back from Invo.
    /// </summary>
    public static class InvoApprovalStep
    {
        private const float StatusPollSeconds = 5f;
        private const float HoldTimeoutSeconds = 15f * 60f;

        /// <summary>Reads the transaction's status: <see cref="APIManager.GetSendStatusAsync"/> or
        /// <see cref="APIManager.GetTransferStatusAsync"/>.</summary>
        public delegate Task<TransactionStatusResponse> StatusReader(string transactionId);

        /// <summary>
        /// Runs the sender approval for <paramref name="transactionId"/> and waits out any hold that
        /// follows. <paramref name="info"/> receives progress lines for the panel's status text.
        /// </summary>
        public static async Task<InvoApprovalStepResult> ApproveAsync(
            string transactionId,
            string flow,
            IInvoDeviceApprovalView view,
            StatusReader readStatus,
            Action<string> info,
            CancellationToken token)
        {
            InvoDeviceApprovalResult run;
            try
            {
                run = await InvoDeviceApproval.RunAsync(transactionId, flow, view, token);
            }
            catch (InvoApiException ex)
            {
                // The failure may have come from the settle call AFTER Invo committed it. Read the
                // transaction before saying anything; only a fresh read decides "it failed".
                var state = await TryReadStateAsync(readStatus, transactionId);
                if (IsApprovedState(state))
                    return new InvoApprovalStepResult { Approved = true };
                return Failure(ex, state);
            }

            switch (run.Outcome)
            {
                case InvoDeviceApprovalOutcome.Settled:
                case InvoDeviceApprovalOutcome.AlreadySettled:
                    return new InvoApprovalStepResult
                    {
                        Approved = true,
                        ClaimCode = run.Settlement != null ? run.Settlement.claim_code : null,
                        ClaimCodeExpiresAt = run.Settlement != null ? run.Settlement.claim_code_expires_at : null
                    };

                case InvoDeviceApprovalOutcome.Held:
                    info?.Invoke(DescribeHold(run.Settlement != null ? run.Settlement.HoldReason : null));
                    return await WaitForApprovedStateAsync(transactionId, readStatus, token);

                case InvoDeviceApprovalOutcome.NotPending:
                {
                    var state = await TryReadStateAsync(readStatus, transactionId);
                    if (IsApprovedState(state))
                        return new InvoApprovalStepResult { Approved = true };
                    return new InvoApprovalStepResult { Message = DescribeTerminal(state), CanRetry = false };
                }

                case InvoDeviceApprovalOutcome.Denied:
                    return new InvoApprovalStepResult
                    {
                        CanRetry = true,
                        Message = "The approval was declined, so nothing was sent. Try again if that was a mistake."
                    };

                case InvoDeviceApprovalOutcome.Expired:
                    return new InvoApprovalStepResult
                    {
                        CanRetry = true,
                        Message = "The QR code expired before it was approved. Show a new one to try again."
                    };

                default:
                    return new InvoApprovalStepResult { Cancelled = true };
            }
        }

        /// <summary>
        /// Before the approval can start: waits while a guardian (or a recipient check) holds the
        /// transaction. Returns null when the sender may now approve, or a result when the wait
        /// itself ended the flow (approved without the sender, expired, cancelled).
        /// </summary>
        public static async Task<InvoApprovalStepResult> WaitUntilApprovableAsync(
            string transactionId,
            StatusReader readStatus,
            CancellationToken token)
        {
            float deadline = Time.realtimeSinceStartup + HoldTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!await DelayAsync(StatusPollSeconds, token))
                    return new InvoApprovalStepResult { Cancelled = true };

                var state = await TryReadStateAsync(readStatus, transactionId);
                if (IsApprovedState(state))
                    return new InvoApprovalStepResult { Approved = true };
                if (IsTerminalState(state))
                    return new InvoApprovalStepResult { Message = DescribeTerminal(state) };

                // The pending list says whether the sender's own step is open yet.
                PendingActionsResponse pending = null;
                try { pending = await APIManager.Instance.GetPendingActionsAsync(); }
                catch (InvoApiException ex) { Debug.LogWarning("[InvoSDK] Pending list read failed: " + ex); }

                if (pending != null && pending.pending != null)
                {
                    var item = pending.pending.Find(p => p.transfer_id == transactionId &&
                                                         p.kind == PendingAction.KindIdentityGate);
                    if (item != null && !item.held)
                        return null;
                }
            }
            return new InvoApprovalStepResult
            {
                Message = "This is still waiting for approval. Check back later; nothing has been sent yet."
            };
        }

        private static async Task<InvoApprovalStepResult> WaitForApprovedStateAsync(
            string transactionId, StatusReader readStatus, CancellationToken token)
        {
            float deadline = Time.realtimeSinceStartup + HoldTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!await DelayAsync(StatusPollSeconds, token))
                    return new InvoApprovalStepResult { Cancelled = true };
                var state = await TryReadStateAsync(readStatus, transactionId);
                if (IsApprovedState(state))
                    return new InvoApprovalStepResult { Approved = true };
                if (IsTerminalState(state))
                    return new InvoApprovalStepResult { Message = DescribeTerminal(state) };
            }
            return new InvoApprovalStepResult
            {
                Message = "This is still on hold. Check back later; you will see it in your history once it clears."
            };
        }

        // ------------------------------------------------------------------

        /// <summary>The transaction's <c>verification_state</c>, or null when it could not be read.</summary>
        private static async Task<string> TryReadStateAsync(StatusReader readStatus, string transactionId)
        {
            try
            {
                var status = await readStatus(transactionId);
                return status != null ? status.verification_state : null;
            }
            catch (InvoApiException ex)
            {
                Debug.LogWarning("[InvoSDK] Status read failed: " + ex);
                return null;
            }
        }

        private static bool IsApprovedState(string state) =>
            string.Equals(state, "approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase);

        private static bool IsTerminalState(string state) =>
            string.Equals(state, "expired", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase);

        private static string DescribeTerminal(string state)
        {
            if (string.Equals(state, "expired", StringComparison.OrdinalIgnoreCase))
                return "This expired before it was approved. The reserved amount is returned to your balance.";
            if (string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase))
                return "This could not be completed. The reserved amount is returned to your balance.";
            return "We could not confirm this. Check your history before trying again.";
        }

        private static string DescribeHold(string reason)
        {
            switch (reason)
            {
                case "GUARDIAN_APPROVAL_PENDING":
                    return "Approved. It now needs the guardian on file to approve as well; we will finish when they do.";
                case "RISK_HOLD":
                    return "Approved. Invo is reviewing it before it goes through; nothing else is needed from you.";
                default:
                    return "Approved. One more check is running; we will finish as soon as it clears.";
            }
        }

        private static InvoApprovalStepResult Failure(InvoApiException ex, string state)
        {
            if (IsTerminalState(state))
                return new InvoApprovalStepResult { Message = DescribeTerminal(state) };

            switch (ex.ErrorCode)
            {
                case "DEVICE_APPROVAL_ALREADY_PENDING":
                    return new InvoApprovalStepResult
                    {
                        CanRetry = true,
                        Message = "This was already approved on a phone and is finishing. Wait a few minutes, then try again."
                    };
                case "PASSKEY_RECOVERY_COOLDOWN":
                    return new InvoApprovalStepResult
                    {
                        Message = "Your INVO passkey was recently recovered, so sending is paused for a while. Try again later."
                    };
                case "DEVICE_APPROVAL_NOT_CONFIGURED":
                case "TENANT_NOT_MIGRATED":
                case "sdk_verification_disabled":
                    return new InvoApprovalStepResult
                    {
                        Message = "Phone approval is not enabled for this game yet. Contact Invo to enable it."
                    };
                case "player_not_found":
                    return new InvoApprovalStepResult { Message = "Your Invo account was not found in this game." };
                case APIManager.GameServerRequiredCode:
                    return new InvoApprovalStepResult { Message = "This build is not connected to its game server." };
            }

            if (ex.IsNetworkError)
                return new InvoApprovalStepResult
                {
                    CanRetry = true,
                    Message = "No connection. If you already approved on your phone, check your history before trying again."
                };

            return new InvoApprovalStepResult
            {
                CanRetry = ex.StatusCode >= 500 || ex.IsRateLimited,
                Message = string.IsNullOrEmpty(ex.ErrorId)
                    ? "We could not confirm the approval. Check your history before trying again."
                    : $"We could not confirm the approval. Check your history before trying again. (Ref {ex.ErrorId})"
            };
        }

        private static async Task<bool> DelayAsync(float seconds, CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);
                return !token.IsCancellationRequested;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
