using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    public class ItemCardView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image itemImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text newPriceText;
        [SerializeField] private TMP_Text oldPriceText;
        [SerializeField] private Button purchaseButton;

        private InvoSDKItem itemData;
        private System.Action<InvoSDKItem> onPurchase;

        public void Initialize(InvoSDKItem data, System.Action<InvoSDKItem> onClick)
        {
            itemData = data;
            onPurchase = onClick;

            if (data == null)
            {
                Debug.LogWarning("[InvoSDK] ItemCardView.Initialize called with a null item.");
                return;
            }

            SetText(nameText, data.itemName);

            // InvoSDKItem.priceUSD is a float whose name is misleading: catalog items are settled in
            // GAME CURRENCY through the item-purchase API, not in USD. Cast at the boundary and label
            // with the game currency (the field itself is owned by the models agent, so it keeps its
            // name). Display only — anything bound for a payload goes through InvoFormat.Amount.
            decimal price = (decimal)data.priceUSD;
            string currency = APIManager.Instance != null ? APIManager.Instance.GetGameCurrency() : string.Empty;
            SetText(newPriceText, Format(price, currency));

            if (data.IsDiscounted)
            {
                decimal originalPrice = (decimal)data.originalPriceUSD.Value;
                SetText(oldPriceText, Format(originalPrice, currency));
                if (oldPriceText != null) oldPriceText.gameObject.SetActive(true);
            }
            else if (oldPriceText != null)
            {
                oldPriceText.gameObject.SetActive(false);
            }

            if (itemImage != null)
            {
                if (data.itemSprite != null)
                    itemImage.sprite = data.itemSprite;
                else
                    _ = InvoImageLoader.LoadIntoAsync(data.imageUrl, itemImage);
            }

            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
                purchaseButton.onClick.AddListener(() => onPurchase?.Invoke(itemData));
            }
        }

        private static string Format(decimal amount, string currency) =>
            string.IsNullOrEmpty(currency) ? $"{amount:0.##}" : $"{amount:0.##} {currency}";

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }
    }
}
