using System;

namespace InvoSDK
{
    /// <summary>
    /// Pure decision helpers for the device-approval (QR) flow. No UnityEngine dependency, so the
    /// money-relevant rules are covered by EditMode tests.
    /// </summary>
    public static class InvoDeviceApprovalCore
    {
        /// <summary>Interval RFC 8628 says to assume when the server states none.</summary>
        public const int DefaultIntervalSeconds = 5;

        /// <summary>Seconds RFC 8628 section 3.5 adds on every <c>slow_down</c>.</summary>
        public const int SlowDownIncrementSeconds = 5;

        /// <summary>Consecutive transport failures tolerated while polling before giving up.</summary>
        public const int MaxTransientPollFailures = 5;

        /// <summary>
        /// Whether <paramref name="currentStatus"/> (from a <c>TRANSACTION_NOT_PENDING</c> answer) proves
        /// that THIS flow's step already happened.
        /// <para>
        /// An ALLOW-list per flow, and fail closed: the sender approves are past their step from
        /// <c>pending_claim</c> on, but the receipt flows only at <c>completed</c> — a receipt call
        /// landing on <c>pending_pin_verification</c> means the SENDER has not approved yet. Any status
        /// this SDK does not know answers false, so a status Invo adds later is never read as
        /// "the money moved". Mirrors the web SDK's <c>DEVICE_SETTLED_STATUSES</c>.
        /// </para>
        /// </summary>
        public static bool IsPastStep(string flow, string currentStatus)
        {
            if (string.IsNullOrWhiteSpace(currentStatus))
                return false;
            string s = currentStatus.Trim().ToLowerInvariant();
            bool finished = s == "completed" || s == "distributed";
            switch (flow)
            {
                case InvoApprovalFlow.Transfer:
                case InvoApprovalFlow.Send:
                    return finished || s == "pending_claim" || s == "verified" || s == "pin_verified_awaiting_claim";
                case InvoApprovalFlow.SendReceipt:
                case InvoApprovalFlow.TransferReceipt:
                    return finished;
                default:
                    return false;
            }
        }

        /// <summary>The first wait: the grant's interval, or the RFC default when it states none.</summary>
        public static int InitialInterval(int grantInterval)
        {
            return grantInterval > 0 ? grantInterval : DefaultIntervalSeconds;
        }

        /// <summary>The wait after an <c>authorization_pending</c>: the server's figure when it gives one.</summary>
        public static int IntervalAfterPending(int current, int stated)
        {
            return stated > 0 ? stated : Math.Max(1, current);
        }

        /// <summary>The wait after a <c>slow_down</c>: five seconds more, or the server's figure if larger.</summary>
        public static int IntervalAfterSlowDown(int current, int stated)
        {
            return Math.Max(current + SlowDownIncrementSeconds, stated);
        }

        /// <summary>Which channel a build uses: the system browser on a phone, a QR everywhere else.</summary>
        public static string DefaultChannel(bool isMobilePlatform)
        {
            return isMobilePlatform ? InvoApprovalChannel.AppBrowser : InvoApprovalChannel.Qr;
        }

        /// <summary>
        /// Whether a poll failure is worth another poll: no response, rate limited, or a server error.
        /// <c>invalid_grant</c>, 401s after the token retry and other 4xx are final.
        /// </summary>
        public static bool IsTransientPollFailure(long httpStatus)
        {
            return httpStatus == 0 || httpStatus == 429 || httpStatus >= 500;
        }

        /// <summary>
        /// The confirm-enrollment refusals that mean the prompt was already moot (answered by the
        /// backup email, withdrawn, or outlived by its grant). The next poll reports the real state.
        /// </summary>
        public static bool IsMootEnrollmentAnswer(string errorCode)
        {
            return errorCode == "DEVICE_APPROVAL_ENROLLMENT_ALREADY_DECIDED" ||
                   errorCode == "DEVICE_APPROVAL_NO_ENROLLMENT_PENDING" ||
                   errorCode == InvoDevicePollStatus.ExpiredToken ||
                   errorCode == InvoDevicePollStatus.InvalidGrant;
        }
    }
}
