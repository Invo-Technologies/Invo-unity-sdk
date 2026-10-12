using System;
using System.Globalization;
using System.Text;

namespace InvoSDK
{
    /// <summary>
    /// The two answers the game screen can give to "Set up INVO on &lt;label&gt;?".
    /// The game forwards the answer to ITS OWN server, which posts it to
    /// <c>POST /api/sdk/approvals/device/confirm-enrollment {device_code, decision}</c>.
    /// The SDK never holds <c>device_code</c>; it stays on the game server.
    /// </summary>
    public enum InvoEnrollmentDecision
    {
        Approve,
        Deny
    }

    /// <summary>
    /// Values of <c>enrollment.state</c> on the server's poll while a first-time phone waits
    /// for the game screen to confirm the match code.
    /// </summary>
    public static class InvoEnrollmentState
    {
        public const string AwaitingScreen = "awaiting_screen";
        public const string Confirmed = "confirmed";
        public const string Denied = "denied";
    }

    /// <summary>
    /// Pure logic behind <see cref="InvoHostedApproval"/>: scheme derivation, URL checks and
    /// the prompt copy. No engine dependency, so it is unit-testable in EditMode.
    /// </summary>
    public static class InvoHostedApprovalCore
    {
        /// <summary>The return scheme is <c>invo-sdk-&lt;game_id&gt;</c>, derived from the game id and
        /// nothing else. The server derives the same value; it is never caller-supplied.</summary>
        public const string SchemePrefix = "invo-sdk-";

        /// <summary>The only URL the hosted page ever navigates back to. Approved, denied and expired
        /// all use this same bare URL: the callback carries NOTHING, the truth is the server's poll.</summary>
        public const string ReturnHost = "done";

        public const string YesLabel = "Yes";
        public const string NoLabel = "No";
        public const string StopLabel = "Stop it (wrong code)";

        /// <summary>Cap for any server-supplied display value.</summary>
        public const int MaxDisplayLength = 64;

        /// <summary>Tighter cap for the device label, which is rendered inside the prompt copy.</summary>
        public const int MaxLabelLength = 32;

        private const int MaxGameIdLength = 40;

        /// <summary>Game ids are numeric on the platform. Anything else cannot form a scheme.</summary>
        public static bool IsValidGameId(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || gameId.Length > MaxGameIdLength)
                return false;
            for (int i = 0; i < gameId.Length; i++)
            {
                if (gameId[i] < '0' || gameId[i] > '9')
                    return false;
            }
            return true;
        }

        /// <summary>Returns <c>invo-sdk-&lt;game_id&gt;</c>, or null when the game id cannot form a scheme.</summary>
        public static string ReturnScheme(string gameId)
        {
            return IsValidGameId(gameId) ? SchemePrefix + gameId : null;
        }

        /// <summary>Returns <c>invo-sdk-&lt;game_id&gt;://done</c>, or null when the game id is invalid.</summary>
        public static string ReturnUrl(string gameId)
        {
            string scheme = ReturnScheme(gameId);
            return scheme == null ? null : scheme + "://" + ReturnHost;
        }

        /// <summary>
        /// True only for an absolute https URL with no whitespace. The hosted page lives on
        /// INVO's platform domain and is only ever opened over https; anything else is refused
        /// before it reaches a browser.
        /// </summary>
        public static bool IsHostedApprovalUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
            for (int i = 0; i < url.Length; i++)
            {
                if (char.IsWhiteSpace(url[i]) || char.IsControl(url[i]))
                    return false;
            }
            Uri parsed;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed))
                return false;
            return string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrEmpty(parsed.Host);
        }

        /// <summary>
        /// True when <paramref name="url"/> uses this game's return scheme. Only the scheme is
        /// compared (case-insensitively, as URL schemes are). Host, path, query and fragment are
        /// deliberately never read: the return URL carries nothing, and a value smuggled onto it
        /// must not be able to tell the game anything.
        /// </summary>
        public static bool IsReturnUrl(string url, string gameId)
        {
            string expected = ReturnScheme(gameId);
            if (expected == null)
                return false;
            string scheme = ExtractScheme(url);
            return scheme != null && string.Equals(scheme, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when <paramref name="url"/> uses ANY <c>invo-sdk-&lt;digits&gt;</c> scheme. Used for a
        /// cold-start deep link before the game id is known; still a wake-up only.
        /// </summary>
        public static bool IsAnyReturnUrl(string url)
        {
            string scheme = ExtractScheme(url);
            if (scheme == null || scheme.Length <= SchemePrefix.Length)
                return false;
            if (!scheme.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            return IsValidGameId(scheme.Substring(SchemePrefix.Length));
        }

        /// <summary>The scheme part of a URL (before the first colon), or null if there is none.</summary>
        public static string ExtractScheme(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;
            int colon = url.IndexOf(':');
            if (colon <= 0)
                return null;
            string scheme = url.Substring(0, colon);
            for (int i = 0; i < scheme.Length; i++)
            {
                char c = scheme[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                          || c == '-' || c == '+' || c == '.';
                if (!ok)
                    return null;
            }
            return scheme;
        }

        /// <summary>The wire value for <c>decision</c> on confirm-enrollment.</summary>
        public static string DecisionWire(InvoEnrollmentDecision decision)
        {
            return decision == InvoEnrollmentDecision.Approve ? "approve" : "deny";
        }

        /// <summary>
        /// Copy for the match-code prompt. The phone page shows the same code, so the player
        /// confirms a MATCH, not a guess by label.
        /// </summary>
        public static string PromptText(string deviceLabel, string matchCode)
        {
            return "Set up INVO on " + QuotedLabel(deviceLabel) + "? Code "
                   + SanitizeForDisplay(matchCode, "unknown")
                   + ". Say Yes only if the phone you just opened shows this code.";
        }

        /// <summary>Copy shown after the game said Yes while the phone finishes its ceremony.</summary>
        public static string FinishingText(string deviceLabel)
        {
            return "Finishing on " + QuotedLabel(deviceLabel) + "…";
        }

        /// <summary>The device label, capped at <see cref="MaxLabelLength"/> and quoted so a
        /// label that reads like a sentence cannot rewrite the prompt.</summary>
        public static string QuotedLabel(string deviceLabel)
        {
            return "\"" + SanitizeForDisplay(deviceLabel, "this phone", MaxLabelLength) + "\"";
        }

        public static string SanitizeForDisplay(string value, string fallback)
        {
            return SanitizeForDisplay(value, fallback, MaxDisplayLength);
        }

        /// <summary>
        /// Device labels and codes arrive from the server and are rendered as text. Control and
        /// Unicode format characters (bidi overrides, zero-width joiners) are dropped, whole
        /// <c>&lt;...&gt;</c> spans are removed (TextMeshPro and uGUI would otherwise honour
        /// rich-text tags; an unclosed <c>&lt;</c> drops the rest) and the value is capped at
        /// <paramref name="maxLength"/>, falling back when empty.
        /// </summary>
        public static string SanitizeForDisplay(string value, string fallback, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;
            StringBuilder sb = new StringBuilder(value.Length);
            bool inTag = false;
            for (int i = 0; i < value.Length && sb.Length < maxLength; i++)
            {
                char c = value[i];
                if (inTag)
                {
                    if (c == '>')
                        inTag = false;
                    continue;
                }
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }
                if (c == '>' || char.IsControl(c)
                    || char.GetUnicodeCategory(c) == UnicodeCategory.Format)
                    continue;
                sb.Append(c);
            }
            string cleaned = sb.ToString().Trim();
            return cleaned.Length == 0 ? fallback : cleaned;
        }
    }
}
