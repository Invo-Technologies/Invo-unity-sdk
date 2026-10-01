using System;
using System.Collections.Generic;

namespace InvoSDK
{
    /// <summary>The four flows a device approval can authorise. The flow picks the settle endpoint.</summary>
    public static class InvoApprovalFlow
    {
        /// <summary>Sender approves moving their own value to another of their games.</summary>
        public const string Transfer = "transfer";
        /// <summary>Sender approves giving value to another player.</summary>
        public const string Send = "send";
        /// <summary>Receiver collects a send addressed to them.</summary>
        public const string SendReceipt = "send_receipt";
        /// <summary>The same player collects their own transfer in the destination game.</summary>
        public const string TransferReceipt = "transfer_receipt";

        public static bool IsValid(string flow)
        {
            return flow == Transfer || flow == Send || flow == SendReceipt || flow == TransferReceipt;
        }
    }

    /// <summary>How the approval page reaches the player's phone.</summary>
    public static class InvoApprovalChannel
    {
        /// <summary>The game draws <c>verification_uri_complete</c> as a QR; the player scans it with their phone.
        /// Desktop, Steam and console builds.</summary>
        public const string Qr = "qr";
        /// <summary>The game opens the page in the system browser on the same phone.
        /// iOS and Android builds. See <see cref="InvoHostedApproval"/>.</summary>
        public const string AppBrowser = "app_browser";
    }

    /// <summary>RFC 8628 poll outcomes. Every value except <see cref="Approved"/> arrives as HTTP 400.</summary>
    public static class InvoDevicePollStatus
    {
        public const string Approved = "approved";
        public const string AuthorizationPending = "authorization_pending";
        public const string SlowDown = "slow_down";
        public const string AccessDenied = "access_denied";
        public const string ExpiredToken = "expired_token";
        public const string InvalidGrant = "invalid_grant";
    }

    /// <summary>Response of <c>POST /api/sdk/player-token</c>.</summary>
    [Serializable]
    public class PlayerTokenResponse
    {
        public string token;
        /// <summary>ISO-8601. Tokens live 15 minutes and cannot be refreshed; mint a new one.</summary>
        public string expires_at;
        /// <summary>Opaque player identity. Never the raw identity hash.</summary>
        public string identity_id;
    }

    /// <summary>Response of <c>POST /api/sdk/approvals/device/begin</c> (RFC 8628 section 3.2 names).</summary>
    [Serializable]
    public class DeviceApprovalGrant
    {
        /// <summary>Bearer factor for the poll and the settle call. Keep it in memory; never show or log it.</summary>
        public string device_code;
        /// <summary>Short code to print under the QR, for a player typing the URL by hand.</summary>
        public string user_code;
        /// <summary>The page without the code, for "go to … and enter …".</summary>
        public string verification_uri;
        /// <summary>The page with the code. This is what the QR encodes.</summary>
        public string verification_uri_complete;
        /// <summary>Seconds until the grant expires (10 minutes today).</summary>
        public int expires_in;
        /// <summary>Minimum seconds between polls.</summary>
        public int interval;
        public string channel;
    }

    /// <summary>
    /// One poll of <c>POST /api/sdk/approvals/device/poll</c>, folded into one shape: the approved
    /// 200 body and the RFC 8628 400 outcomes alike. <see cref="status"/> is one of
    /// <see cref="InvoDevicePollStatus"/>.
    /// </summary>
    [Serializable]
    public class DeviceApprovalPollResponse
    {
        public string status;
        public string transaction_id;
        public string flow;
        public string approved_at;
        /// <summary>Seconds to wait before the next poll, when the server states one.</summary>
        public int interval;
        /// <summary>Present only while a first-time phone waits for the game screen to confirm it.</summary>
        public EnrollmentInfo enrollment;
    }

    /// <summary>Response of <c>POST /api/sdk/approvals/device/confirm-enrollment</c>.</summary>
    [Serializable]
    public class ConfirmEnrollmentResponse
    {
        public string status;
    }

    /// <summary>
    /// Result of the settle call that follows an approved grant: <c>/api/sdk/{transfers|send}/{id}/approve</c>
    /// or <c>/confirm-receipt</c> with <c>device_code</c>. THIS is the call that moves money.
    /// </summary>
    [Serializable]
    public class DeviceApprovalSettleResponse
    {
        /// <summary>
        /// <c>approved</c> (sender flows; the transaction is now <c>pending_claim</c>),
        /// <c>completed</c> (receipt flows; the value is credited),
        /// <c>pending_guardian_approval</c> / <c>held_for_review</c> / <c>step_up_required</c> /
        /// <c>pending_confirmation</c> (a 202 hold: nothing moved yet, nothing refused), or
        /// <c>not_pending</c> (the transaction already left this step; see <see cref="already_settled"/>).
        /// </summary>
        public string status;
        public string next;
        public string transaction_id;
        /// <summary>Transfers only: the self-claim code, for a destination game that cannot confirm-receipt.</summary>
        public string claim_code;
        public string claim_code_expires_at;
        /// <summary>Receipt flows: net amount credited.</summary>
        public string amount_received;
        /// <summary>On a 202 hold: <c>GUARDIAN_APPROVAL_PENDING</c>, <c>RISK_HOLD</c>, <c>STEP_UP_REQUIRED</c>,
        /// <c>RECIPIENT_IDENTITY_PENDING</c>.</summary>
        public string error_code;
        public string code;
        public GuardianApprovalInfo guardian_approval;
        public string poll_endpoint;

        /// <summary>Set by the SDK on <c>not_pending</c>: the transaction's status at the time of the call.</summary>
        public string current_status;
        /// <summary>Set by the SDK on <c>not_pending</c>: true only when <see cref="current_status"/> proves this
        /// step already happened. False for any status the SDK does not recognise (fail closed).</summary>
        public bool already_settled;

        public bool IsHold
        {
            get
            {
                return status == "pending_guardian_approval" || status == "held_for_review" ||
                       status == "step_up_required" || status == "pending_confirmation";
            }
        }

        public string HoldReason { get { return !string.IsNullOrEmpty(error_code) ? error_code : code; } }
    }

    /// <summary>Guardian hold details on a settle 202.</summary>
    [Serializable]
    public class GuardianApprovalInfo
    {
        public string approval_id;
        public string state;
        public string expires_at;
        public string consent_channel;
        public string poll_endpoint;
        public string resend_endpoint;
    }

    /// <summary>Response of <c>GET /api/sdk/transfers/pending</c>, scoped to the player in the token.</summary>
    [Serializable]
    public class PendingActionsResponse
    {
        public List<PendingAction> pending = new List<PendingAction>();
    }

    /// <summary>One item awaiting the player.</summary>
    [Serializable]
    public class PendingAction
    {
        public const string KindIdentityGate = "identity_gate";
        public const string KindReceivingConfirm = "receiving_confirm";

        /// <summary>The transaction id, whatever the flow.</summary>
        public string transfer_id;
        /// <summary><see cref="KindIdentityGate"/> (I started it and must approve it) or
        /// <see cref="KindReceivingConfirm"/> (it is addressed to me and I can collect it).</summary>
        public string kind;
        /// <summary><c>transfer</c> or <c>send</c>.</summary>
        public string flow;
        /// <summary>Gross for the sender, net for the receiver.</summary>
        public string amount;
        public string currency;
        /// <summary>The other game's name.</summary>
        public string counterparty_game;
        /// <summary>Display only. The backend enforces expiry.</summary>
        public string expires_at;
        public bool step_up_required;
        /// <summary>True when the item cannot be acted on yet (e.g. a guardian hold).</summary>
        public bool held;
        public string hold_reason;

        /// <summary>The device-approval flow that collects or approves this item.</summary>
        public string ApprovalFlow
        {
            get
            {
                bool receiving = kind == KindReceivingConfirm;
                if (flow == InvoApprovalFlow.Transfer)
                    return receiving ? InvoApprovalFlow.TransferReceipt : InvoApprovalFlow.Transfer;
                return receiving ? InvoApprovalFlow.SendReceipt : InvoApprovalFlow.Send;
            }
        }
    }
}
