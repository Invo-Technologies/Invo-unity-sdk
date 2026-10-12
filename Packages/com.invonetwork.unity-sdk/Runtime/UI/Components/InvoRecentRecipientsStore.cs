using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace InvoSDK.UI
{
    /// <summary>
    /// The numbers a player has sent to, newest first, kept on this device only (PlayerPrefs).
    /// INVO's API does not return recipients' numbers (the activity feed deliberately hides them), so
    /// the list is local. Keys are a hash of the player's email, so one device can hold several
    /// players' lists without writing their emails to disk. Call <see cref="Clear"/> on sign-out if
    /// players share a device.
    /// </summary>
    public static class InvoRecentRecipientsStore
    {
        public const int MaxRecipients = 5;
        private const string KeyPrefix = "InvoSDK.recentRecipients.";

        public static List<string> Get(string playerEmail)
        {
            if (string.IsNullOrWhiteSpace(playerEmail))
                return new List<string>();
            string raw = PlayerPrefs.GetString(Key(playerEmail), string.Empty);
            return Parse(raw);
        }

        public static void Remember(string playerEmail, string phone)
        {
            if (string.IsNullOrWhiteSpace(playerEmail) || string.IsNullOrWhiteSpace(phone))
                return;
            List<string> merged = Merge(Get(playerEmail), phone, MaxRecipients);
            PlayerPrefs.SetString(Key(playerEmail), string.Join("|", merged));
            PlayerPrefs.Save();
        }

        public static void Clear(string playerEmail)
        {
            if (string.IsNullOrWhiteSpace(playerEmail))
                return;
            PlayerPrefs.DeleteKey(Key(playerEmail));
            PlayerPrefs.Save();
        }

        /// <summary>Moves <paramref name="phone"/> to the front, drops duplicates, keeps at most <paramref name="max"/>.</summary>
        public static List<string> Merge(IList<string> existing, string phone, int max)
        {
            var result = new List<string>();
            string normalized = InvoPhone.Normalize(phone) ?? phone?.Trim();
            if (!string.IsNullOrEmpty(normalized))
                result.Add(normalized);
            if (existing != null)
            {
                foreach (string entry in existing)
                {
                    if (result.Count >= max)
                        break;
                    if (string.IsNullOrWhiteSpace(entry) || result.Contains(entry))
                        continue;
                    result.Add(entry);
                }
            }
            if (result.Count > max)
                result.RemoveRange(max, result.Count - max);
            return result;
        }

        public static List<string> Parse(string raw)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw))
                return list;
            foreach (string part in raw.Split('|'))
                if (!string.IsNullOrWhiteSpace(part) && !list.Contains(part))
                    list.Add(part);
            return list;
        }

        private static string Key(string playerEmail)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(playerEmail.Trim().ToLowerInvariant()));
                var sb = new StringBuilder(KeyPrefix);
                for (int i = 0; i < 8; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
