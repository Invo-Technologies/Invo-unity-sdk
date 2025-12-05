using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    public class FeaturedItemCard : MonoBehaviour
    {
        public Image itemImage;
        public TMP_Text titleText;
        public TMP_Text subtitleText;
        public TMP_Text priceText;
        public TMP_Text oldPriceText;
        public TMP_Text discountBadgeText;
        public Button buyButton;

        public async void SetData(GameItem item, System.Action<GameItem> onClick = null)
        {
            titleText.text = item.display_name;
            subtitleText.text = item.display_subtitle;

            priceText.text = $"${item.price_usd:F2}";

            if (item.original_price_usd.HasValue && item.original_price_usd > item.price_usd)
            {
                oldPriceText.gameObject.SetActive(true);
                oldPriceText.text = $"${item.original_price_usd:F2}";
            }
            else oldPriceText.gameObject.SetActive(false);

            if (item.discount_percentage.HasValue)
            {
                discountBadgeText.gameObject.SetActive(true);
                discountBadgeText.text = $"-{item.discount_percentage}%";
            }
            else discountBadgeText.gameObject.SetActive(false);

            await LoadImage(item.image_url, itemImage);
        }

        private async Task LoadImage(string url, Image img)
        {
            if (string.IsNullOrEmpty(url)) return;
            using var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
}
