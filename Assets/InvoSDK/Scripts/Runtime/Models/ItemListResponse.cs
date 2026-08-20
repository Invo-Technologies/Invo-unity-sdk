using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace InvoSDK
{
    [Serializable]
    public class GameItemListResponse
    {
        public List<GameItem> items;
        public string status;
        public int total_items;
    }

    /// <summary>
    /// Mirrors the server's <c>GameItem.to_dict()</c> plus the two fields the
    /// /api/items/list route injects for limited-time offers.
    /// </summary>
    [Serializable]
    public class GameItem
    {
        public string item_id;
        /// <summary>
        /// Owning game. Serialized as a STRING (the server does str(game_id) on a
        /// BigInteger); may be null for admin-created template items.
        /// </summary>
        public string game_id;
        public string item_name;
        public string display_name;
        public string display_subtitle;
        public string display_tag;
        /// <summary>e.g. "2,000" or "750 + 250 bonus".</summary>
        public string display_getting;
        public string item_category;        // featured, weekly, daily, standard
        public string item_type;            // bundle, currency_pack, daily_deal, special_offer
        public string image_url;
        public string icon_url;
        public string thumbnail_url;
        public string item_color;           // e.g. "#FFFFFF" or "blue"
        public bool is_featured;
        public bool is_most_popular;
        public bool is_best_value;
        public bool is_active;
        /// <summary>Server column is an INTEGER (0-100); null when no discount.</summary>
        public int? discount_percentage;
        public float price_usd;
        public float? original_price_usd;
        public double currency_amount;
        /// <summary>Additional bonus currency; the server sends 0 rather than null.</summary>
        public double bonus_amount;
        public string item_rarity;          // e.g. rare, epic, legendary

        // --- Time-limited offers ---
        public bool is_limited_time;
        /// <summary>ISO-8601, may be null.</summary>
        public string available_from;
        /// <summary>ISO-8601, may be null.</summary>
        public string available_until;
        /// <summary>Injected by the list route for live limited-time offers only.</summary>
        public int? time_remaining_seconds;
        /// <summary>
        /// Injected by the list route: "2d 3h 15m", "3h 15m", "15m", or "Expired".
        /// Null for items that are not limited-time.
        /// </summary>
        public string time_remaining_display;

        /// <summary>
        /// Arbitrary JSONB blob from the server — values are NOT all strings, so
        /// this must stay a JToken-backed type. Defaults to an empty object.
        /// </summary>
        public JObject metadata;
    }
}
