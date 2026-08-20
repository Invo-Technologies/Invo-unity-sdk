using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    public class FeatureWeeklyItemCard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text newPriceText;
        [SerializeField] private TMP_Text oldPriceText;
        [SerializeField] private TMP_Text discountTagText;
        [SerializeField] private Button purchaseButton;

        private GameItem itemData;
        private System.Action<GameItem> onPurchase;

        public void SetData(GameItem item, System.Action<GameItem> onClick = null)
        {
            itemData = item;
            onPurchase = onClick;

            if (item == null)
            {
                Debug.LogWarning("[InvoSDK] FeatureWeeklyItemCard.SetData called with a null item.");
                return;
            }

            SetText(itemNameText, item.display_name);
            SetText(subtitleText, item.display_subtitle);

            // Real-money pack: USD is the correct label. Display only — the amount sent to the
            // checkout endpoint is formatted with InvoFormat.Amount.
            SetText(newPriceText, $"${item.price_usd:F2}");

            if (oldPriceText != null)
            {
                bool hasOldPrice = item.original_price_usd.HasValue && item.original_price_usd.Value > item.price_usd;
                oldPriceText.gameObject.SetActive(hasOldPrice);
                if (hasOldPrice)
                    oldPriceText.text = $"${item.original_price_usd.Value:F2}";
            }

            if (discountTagText != null)
            {
                bool hasDiscount = item.discount_percentage.HasValue && item.discount_percentage.Value > 0;
                discountTagText.gameObject.SetActive(hasDiscount);
                if (hasDiscount)
                    discountTagText.text = $"-{item.discount_percentage.Value}%";
            }

            // https-only, size-capped loader: image_url is server-supplied data.
            if (iconImage != null)
                _ = InvoImageLoader.LoadIntoAsync(item.image_url, iconImage);

            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
                purchaseButton.onClick.AddListener(() => onPurchase?.Invoke(itemData));
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }
    }
}
