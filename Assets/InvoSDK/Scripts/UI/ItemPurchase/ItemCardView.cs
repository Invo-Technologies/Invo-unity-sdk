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

            nameText.text = data.itemName;
            newPriceText.text = $"{data.priceUSD} {APIManager.Instance.GetGameCurrency()}";

            if (data.IsDiscounted)
            {
                oldPriceText.text = $"{data.originalPriceUSD.Value:F2}";
                oldPriceText.gameObject.SetActive(true);
            }
            else
            {
                oldPriceText.gameObject.SetActive(false);
            }

            if (data.itemSprite != null)
            {
                itemImage.sprite = data.itemSprite;
            }
            else if (!string.IsNullOrEmpty(data.imageUrl))
            {
                _ = APIManager.LoadSpriteAsync(data.imageUrl, itemImage);
            }

            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(() => onPurchase?.Invoke(itemData));
        }
    }
}
