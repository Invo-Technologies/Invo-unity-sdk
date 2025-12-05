using System;
using UnityEngine;

namespace InvoSDK
{
    [Serializable]
    public class InvoSDKItem
    {
        [Header("Basic Info")]
        public string itemId;
        public string itemName;
        [TextArea] public string itemDescription;

        [Header("Pricing")]
        public float priceUSD;
        public float? originalPriceUSD;

        [Header("Visuals")]
        public Sprite itemSprite;             // Optional local sprite
        public string imageUrl;               // Optional URL for remote images

        [Header("Metadata")]
        public string rarity;
        public string category;
        public string tag;

        public bool IsDiscounted => originalPriceUSD.HasValue && originalPriceUSD.Value > priceUSD;
    }
}
