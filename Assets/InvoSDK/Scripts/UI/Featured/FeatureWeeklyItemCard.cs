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

            itemNameText.text = item.display_name;
            //subtitleText.text = item.display_subtitle;
            newPriceText.text = $"${item.price_usd:F2}";
            oldPriceText.text = item.original_price_usd.HasValue
                ? $"${item.original_price_usd.Value:F2}"
                : "";
            //discountTagText.text = item.discount_percentage.HasValue
            //    ? $"{item.discount_percentage.Value:F0}%"
            //    : "";

            // Async load image
            if (!string.IsNullOrEmpty(item.image_url))
                _ = APIManager.LoadSpriteAsync(item.image_url, iconImage);

            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(() => onPurchase?.Invoke(itemData));
        }
    }
}
