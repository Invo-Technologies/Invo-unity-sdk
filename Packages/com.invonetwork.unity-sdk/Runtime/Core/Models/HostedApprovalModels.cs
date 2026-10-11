using System;

namespace InvoSDK
{
    /// <summary>
    /// What the game SERVER hands the client after it called
    /// <c>POST /api/sdk/approvals/device/begin {transaction_id, flow, "channel": "app_browser"}</c>.
    /// The RFC 8628 <c>device_code</c> is deliberately absent: it stays on the game server, which
    /// is the only party that polls and the only party that can settle money with it.
    /// </summary>
    [Serializable]
    public class HostedApprovalHandoff
    {
        /// <summary>The hosted page on INVO's platform domain. Open it with
        /// <see cref="InvoHostedApproval.OpenHostedApproval(string)"/>; never in an embedded WebView.</summary>
        public string verification_uri_complete;

        /// <summary>Human-readable code for the "type it instead" fallback.</summary>
        public string user_code;

        /// <summary>Seconds until the grant expires (10 minutes today).</summary>
        public int expires_in;

        /// <summary>Minimum seconds between server polls.</summary>
        public int interval;
    }

    /// <summary>
    /// The <c>enrollment</c> block the server's poll carries while a first-time phone waits for
    /// the game screen to confirm the match code. Absent entirely for an already-enrolled phone.
    /// The game server relays it to the client; feed it to
    /// <see cref="InvoHostedApproval.ApplyEnrollmentState"/>.
    /// </summary>
    [Serializable]
    public class EnrollmentInfo
    {
        /// <summary>One of <see cref="InvoEnrollmentState"/>.</summary>
        public string state;

        /// <summary>The phone as the server describes it, e.g. "iPhone (Safari)".</summary>
        public string device_label;

        /// <summary>The code the phone page is showing. The player confirms a MATCH.</summary>
        public string match_code;

        /// <summary>ISO-8601 timestamp of the enrolment request.</summary>
        public string requested_at;

        /// <summary>True when the phone is REPLACING a lost INVO passkey rather than setting one up.</summary>
        public bool recovery;
    }
}
