using System;
using System.Collections.Generic;

namespace InvoSDK.UI
{
    /// <summary>One game the player can pick as a destination.</summary>
    public class InvoGameOption
    {
        public string id;
        public string name;
        /// <summary>Genre, platform, currency: whatever helps tell similar names apart.</summary>
        public string subtitle;
        /// <summary>Shown on the tile, e.g. the player's balance there.</summary>
        public string detail;
        public string iconUrl;
        /// <summary>True for games the player already plays (shown first, as tiles).</summary>
        public bool isYours;
        /// <summary>False when the game cannot receive right now; it stays listed, greyed, with <see cref="unavailableReason"/>.</summary>
        public bool available = true;
        public string unavailableReason;
        /// <summary>Where this option sits in the panel's destination list.</summary>
        public int index;
    }

    /// <summary>The picker's filtering and ranking, kept free of Unity so it can be tested.</summary>
    public static class InvoGameSearch
    {
        /// <summary>
        /// Games matching <paramref name="query"/>, best first: a name that starts with the query, then
        /// a word in the name that does, then the query anywhere in the name or subtitle. Unavailable
        /// games sort after available ones but are never dropped. An empty query returns every game,
        /// available first, then by name.
        /// </summary>
        public static List<InvoGameOption> Filter(IEnumerable<InvoGameOption> options, string query)
        {
            string q = (query ?? string.Empty).Trim();
            var scored = new List<(InvoGameOption option, int score)>();
            foreach (InvoGameOption option in options ?? Array.Empty<InvoGameOption>())
            {
                if (option == null)
                    continue;
                int score = q.Length == 0 ? 1 : Score(option, q);
                if (score > 0)
                    scored.Add((option, score));
            }
            scored.Sort((a, b) =>
            {
                if (a.option.available != b.option.available)
                    return a.option.available ? -1 : 1;
                if (a.score != b.score)
                    return b.score.CompareTo(a.score);
                return string.Compare(a.option.name, b.option.name, StringComparison.OrdinalIgnoreCase);
            });
            var result = new List<InvoGameOption>(scored.Count);
            foreach (var entry in scored)
                result.Add(entry.option);
            return result;
        }

        private static int Score(InvoGameOption option, string q)
        {
            string name = option.name ?? string.Empty;
            if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                return 4;
            foreach (string word in name.Split(new[] { ' ', '-', ':', '_' }, StringSplitOptions.RemoveEmptyEntries))
                if (word.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                    return 3;
            if (name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            if (!string.IsNullOrEmpty(option.subtitle) && option.subtitle.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;
            return 0;
        }

        /// <summary>Up to two initials for a game with no icon: "Star Raiders" -> "SR".</summary>
        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "?";
            string[] words = name.Split(new[] { ' ', '-', ':', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
                return words[0].Substring(0, Math.Min(2, words[0].Length)).ToUpperInvariant();
            return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
        }
    }
}
