using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// Everything waiting for the player to collect (<c>GET /api/sdk/transfers/pending</c>, kind
    /// <c>receiving_confirm</c>), each with its own Collect button that runs the receiver's phone
    /// approval. Held items are shown greyed with the reason. There is no claim-code form: INVO is
    /// retiring claim codes, and a receiver with no account in this game collects through the link
    /// INVO emails them.
    /// </summary>
    public class InvoCollectPanel : MonoBehaviour
    {
        public Transform listContainer;
        public InvoPendingItemView itemTemplate;
        public TMP_Text emptyText;
        public TMP_Text statusText;
        [Tooltip("The QR view used while collecting. Empty = the built-in overlay.")]
        public InvoDeviceApprovalPanelView approvalView;
        [Tooltip("Hidden while the approval view is up.")]
        public GameObject listSection;
        [Tooltip("Optional badge on the Collect tab showing how many items are waiting.")]
        public TMP_Text tabBadgeText;
        [Min(15f)] public float refreshSeconds = 45f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CancellationTokenSource cts;
        private bool collecting;
        private float nextRefresh;

        private void Awake()
        {
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            SetText(statusText, string.Empty);
            if (approvalView != null) approvalView.Hide();
            ShowList(true);
            nextRefresh = 0f;
        }

        private void OnDisable()
        {
            try { cts?.Cancel(); } catch (ObjectDisposedException) { }
            cts?.Dispose();
            cts = null;
        }

        private void Update()
        {
            if (collecting || Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + refreshSeconds;
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (APIManager.Instance == null)
                return;
            PendingActionsResponse resp;
            try
            {
                resp = await APIManager.Instance.GetPendingActionsAsync();
            }
            catch (InvoApiException ex)
            {
                if (this == null) return;
                SetText(statusText, ex.ErrorCode == APIManager.GameServerRequiredCode
                    ? "Collecting needs the game server. Check Tools > Invo SDK > Settings."
                    : "We could not check for currency sent to you. Try again in a moment.");
                return;
            }
            if (this == null) return;
            Render(resp?.pending);
        }

        /// <summary>Shows the receivable items in <paramref name="pending"/>. Public so tools can preview states.</summary>
        public void Render(IEnumerable<PendingAction> pending)
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();

            int count = 0, ready = 0;
            if (pending != null && itemTemplate != null && listContainer != null)
            {
                foreach (PendingAction item in pending)
                {
                    if (item == null || item.kind != PendingAction.KindReceivingConfirm)
                        continue;
                    InvoPendingItemView view = Instantiate(itemTemplate, listContainer);
                    view.gameObject.SetActive(true);
                    view.Bind(item, Describe(item), it => _ = CollectAsync(it));
                    spawned.Add(view.gameObject);
                    count++;
                    if (!item.held) ready++;
                }
                itemTemplate.transform.SetAsLastSibling();
            }
            if (emptyText != null) emptyText.gameObject.SetActive(count == 0);
            if (tabBadgeText != null)
            {
                tabBadgeText.text = ready.ToString(CultureInfo.InvariantCulture);
                tabBadgeText.transform.parent.gameObject.SetActive(ready > 0);
            }
        }

        private async Task CollectAsync(PendingAction item)
        {
            if (collecting || item == null || item.held)
                return;
            collecting = true;
            SetText(statusText, string.Empty);
            ShowList(false);
            try
            {
                InvoDeviceApprovalResult run;
                try
                {
                    run = await InvoDeviceApproval.RunAsync(item.transfer_id, item.ApprovalFlow, approvalView,
                                                            cts != null ? cts.Token : CancellationToken.None);
                }
                catch (InvoApiException ex)
                {
                    SetText(statusText, DescribeError(ex));
                    return;
                }
                switch (run.Outcome)
                {
                    case InvoDeviceApprovalOutcome.Settled:
                    case InvoDeviceApprovalOutcome.AlreadySettled:
                        string amount = run.Settlement != null && !string.IsNullOrEmpty(run.Settlement.amount_received)
                            ? run.Settlement.amount_received : item.amount;
                        SetText(statusText, $"Collected {amount} {item.currency}.");
                        APIManager.Instance.StartBalancePolling();
                        break;
                    case InvoDeviceApprovalOutcome.Held:
                        SetText(statusText, "Approved. One more check is running; it lands in your balance when it clears.");
                        break;
                    case InvoDeviceApprovalOutcome.Denied:
                        SetText(statusText, "Collection was declined. Nothing changed; you can try again.");
                        break;
                    case InvoDeviceApprovalOutcome.Expired:
                        SetText(statusText, "The code expired. Tap Collect to show a new one.");
                        break;
                    case InvoDeviceApprovalOutcome.NotPending:
                        SetText(statusText, "That one is no longer waiting. It may have been collected or expired.");
                        break;
                }
            }
            finally
            {
                collecting = false;
                if (this != null)
                {
                    ShowList(true);
                    nextRefresh = 0f;
                }
            }
        }

        private void ShowList(bool show)
        {
            if (listSection != null) listSection.SetActive(show);
        }

        private static string Describe(PendingAction item)
        {
            string from = string.IsNullOrEmpty(item.counterparty_game) ? "another game" : item.counterparty_game;
            string what = item.flow == InvoApprovalFlow.Transfer ? $"Your transfer from {from}" : $"Sent from {from}";
            if (item.held)
                return item.hold_reason == "guardian_pending"
                    ? "On hold: waiting for a guardian to approve"
                    : "On hold: this phone needs an extra check first";
            string expiry = DescribeExpiry(item.expires_at);
            return string.IsNullOrEmpty(expiry) ? what : $"{what} · {expiry}";
        }

        public static string DescribeExpiry(string iso)
        {
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                                   DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime at))
                return string.Empty;
            TimeSpan left = at - DateTime.UtcNow;
            if (left <= TimeSpan.Zero) return "expired";
            if (left.TotalDays >= 2) return $"expires in {(int)left.TotalDays} days";
            if (left.TotalHours >= 2) return $"expires in {(int)left.TotalHours} hours";
            return $"expires in {Math.Max(1, (int)left.TotalMinutes)} min";
        }

        private static string DescribeError(InvoApiException ex)
        {
            switch (ex.ErrorCode)
            {
                case "receiver_not_enrolled_use_claim_code":
                    return "You don't have an account in this game yet. Use the link INVO emailed you to collect it.";
                case "not_intended_receiver":
                    return "This one is addressed to a different player or game.";
                case "PASSKEY_RECOVERY_COOLDOWN":
                    return "Your INVO account was just recovered, so collecting is paused for a while. Try again later.";
            }
            return ex.IsRateLimited ? "Too many tries. Wait a moment, then try again." : "We could not collect that. Try again in a moment.";
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }
    }
}
