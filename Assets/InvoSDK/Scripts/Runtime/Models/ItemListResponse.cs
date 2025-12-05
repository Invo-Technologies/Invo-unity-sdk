using System;
using System.Collections.Generic;

namespace InvoSDK
{
    [Serializable]
    public class GameItemListResponse
    {
        public List<GameItem> items;
        public string status;
        public int total_items;
    }

    [Serializable]
    public class GameItem
    {
        public string item_id;
        public string item_name;
        public string display_name;
        public string display_subtitle;
        public string display_tag;
        public string item_category;        // daily, weekly, standard
        public string item_type;            // bundle, currency_pack, etc.
        public string image_url;
        public string item_color;           // e.g. "#FFFFFF" or "blue"
        public bool is_featured;
        public bool is_most_popular;
        public float? discount_percentage;
        public float price_usd;
        public float? original_price_usd;
        public double currency_amount;
        public string item_rarity;          // e.g. rare, epic, legendary
        public Dictionary<string, string> metadata; // optional: for UI custom text/icons
    }
}