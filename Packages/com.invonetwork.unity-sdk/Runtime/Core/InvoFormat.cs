using System.Globalization;
using System.Text.RegularExpressions;

namespace InvoSDK
{
    /// <summary>
    /// Wire-format helpers. Every value that goes into a request body or URL must pass
    /// through here — the Invo API parses decimals with Python's Decimal(), which rejects
    /// the comma separator that ToString() emits under many device locales.
    /// </summary>
    public static class InvoFormat
    {
        /// <summary>
        /// Formats an amount for the wire, always with a period as the decimal separator.
        /// Never use raw ToString("F2") on a value bound for a payload.
        /// </summary>
        public static string Amount(decimal value) =>
            value.ToString("F2", CultureInfo.InvariantCulture);

        public static string Amount(float value) =>
            value.ToString("F2", CultureInfo.InvariantCulture);

        public static string Amount(double value) =>
            value.ToString("F2", CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses user-entered text into an amount, accepting either separator so a player
        /// typing "1,50" on a German keyboard is understood.
        /// </summary>
        public static bool TryParseAmount(string text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string normalized = text.Trim().Replace(" ", string.Empty);

            // A single comma with no period is a decimal separator, not a thousands mark.
            if (!normalized.Contains(".") && normalized.IndexOf(',') == normalized.LastIndexOf(','))
                normalized = normalized.Replace(",", ".");
            else
                normalized = normalized.Replace(",", string.Empty);

            return decimal.TryParse(normalized, NumberStyles.Number,
                                    CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>
    /// E.164 phone handling. The Invo API requires a leading '+' followed by 10-15 digits
    /// and will not infer a country code, so neither do we — a number without one is
    /// rejected here rather than silently turned into a different, valid, wrong number.
    /// </summary>
    public static class InvoPhone
    {
        public const int MinDigits = 10;
        public const int MaxDigits = 15;

        private static readonly Regex NonDialCharacters = new Regex(@"[^\d+]", RegexOptions.Compiled);

        /// <summary>
        /// Normalises user input to strict E.164, or returns null when the input cannot be
        /// a valid number. Callers must surface a null as a validation message rather than
        /// sending it — the same normalised value has to be used at initiate and at claim,
        /// because the backend compares the digits exactly.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Strip formatting, then collapse any leading '+' signs into exactly one.
            string stripped = NonDialCharacters.Replace(raw.Trim(), string.Empty);
            bool hadPlus = stripped.StartsWith("+");
            string digits = stripped.Replace("+", string.Empty);

            if (!hadPlus) return null;                    // no country code, and we must not guess one
            if (digits.Length < MinDigits) return null;
            if (digits.Length > MaxDigits) return null;   // truncating would produce a different real number

            return "+" + digits;
        }

        public static bool IsValid(string raw) => Normalize(raw) != null;

        /// <summary>Human-readable reason a number was rejected, for display next to the input.</summary>
        public static string DescribeProblem(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Enter a phone number.";

            string stripped = NonDialCharacters.Replace(raw.Trim(), string.Empty);
            if (!stripped.StartsWith("+"))
                return "Include the country code, starting with + (for example +15551234567).";

            int digits = stripped.Replace("+", string.Empty).Length;
            if (digits < MinDigits)
                return "That number is too short. Include the country code and full number.";
            if (digits > MaxDigits)
                return "That number is too long. Check the country code and full number.";

            return null;
        }

        /// <summary>Masks a number for display and logging, keeping the last four digits.</summary>
        public static string Mask(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            string digits = NonDialCharacters.Replace(raw, string.Empty).Replace("+", string.Empty);
            return digits.Length <= 4 ? new string('*', digits.Length) : new string('*', digits.Length - 4) + digits.Substring(digits.Length - 4);
        }
    }
}
