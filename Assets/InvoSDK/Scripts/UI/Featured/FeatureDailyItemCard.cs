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

            itemNameText.text = item.display_name;
            priceText.text = $"${item.price_usd:F2}";

            if (!string.IsNullOrEmpty(item.image_url))
                _ = APIManager.LoadSpriteAsync(item.image_url, iconImage);

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuy?.Invoke(itemData));
        }
    }
}
