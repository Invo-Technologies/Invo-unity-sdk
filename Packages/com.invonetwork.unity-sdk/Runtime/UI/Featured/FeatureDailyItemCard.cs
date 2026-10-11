using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    public class FeatureDailyItemCard : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button buyButton;

        private GameItem itemData;
        private System.Action<GameItem> onBuy;

        public void SetData(GameItem item, System.Action<GameItem> onClick = null)
        {
            itemData = item;
            onBuy = onClick;

            if (item == null)
            {
                Debug.LogWarning("[InvoSDK] FeatureDailyItemCard.SetData called with a null item.");
                return;
            }

            SetText(itemNameText, item.display_name);
            // Real-money pack: USD is the correct label. Display only — the amount sent to the
            // checkout endpoint is formatted with InvoFormat.Amount.
            SetText(priceText, $"${item.price_usd:F2}");

            // https-only, size-capped loader: image_url is server-supplied data.
            if (iconImage != null)
                _ = InvoImageLoader.LoadIntoAsync(item.image_url, iconImage);

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => onBuy?.Invoke(itemData));
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }
    }
}
