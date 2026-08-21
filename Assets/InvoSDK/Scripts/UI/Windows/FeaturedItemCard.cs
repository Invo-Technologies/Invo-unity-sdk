using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// Loads catalog artwork from a server-supplied URL.
    ///
    /// Catalog image URLs are attacker-influenceable data, not trusted config: they arrive in an
    /// API response and are handed straight to the networking stack. Everything downloaded here is
    /// therefore restricted to https and capped in size, and the decoded texture is checked before
    /// a Sprite is built from it (a failed decode yields a placeholder texture and Sprite.Create on
    /// a null texture throws).
    /// </summary>
    public static class InvoImageLoader
    {
        /// <summary>Hard cap on a single catalog image. Anything larger is aborted mid-download.</summary>
        public const int MaxImageBytes = 4 * 1024 * 1024;

        /// <summary>Per-image timeout in seconds.</summary>
        public const int TimeoutSeconds = 20;

        /// <summary>Largest dimension accepted for a decoded texture.</summary>
        public const int MaxDimension = 4096;

        /// <summary>True when the URL is safe to fetch: absolute, https, and not empty.</summary>
        public static bool IsAllowedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Downloads <paramref name="url"/> and assigns it to <paramref name="target"/>.
        /// Silently does nothing for a rejected URL, a failed request or an undecodable image —
        /// the card keeps whatever placeholder art the prefab shipped with.
        /// </summary>
        public static async Task<Sprite> LoadIntoAsync(string url, Image target)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            if (!IsAllowedUrl(url))
            {
                Debug.LogWarning("[InvoSDK] Refused to load a catalog image: the URL is not an absolute https URL.");
                return null;
            }

            using var req = UnityWebRequestTexture.GetTexture(url.Trim());
            req.timeout = TimeoutSeconds;

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (req.downloadedBytes > MaxImageBytes)
                {
                    req.Abort();
                    Debug.LogWarning($"[InvoSDK] Catalog image aborted: larger than {MaxImageBytes / 1024 / 1024}MB.");
                    return null;
                }
                await Task.Yield();
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[InvoSDK] Catalog image failed to load: {req.error}");
                return null;
            }

            var tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null || tex.width <= 0 || tex.height <= 0)
            {
                Debug.LogWarning("[InvoSDK] Catalog image could not be decoded.");
                return null;
            }

            if (tex.width > MaxDimension || tex.height > MaxDimension)
            {
                Debug.LogWarning($"[InvoSDK] Catalog image rejected: larger than {MaxDimension}px.");
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            // The card may have been destroyed while the download was in flight.
            if (target != null)
                target.sprite = sprite;

            return sprite;
        }
    }

    public class FeaturedItemCard : MonoBehaviour
    {
        public Image itemImage;
        public TMP_Text titleText;
        public TMP_Text subtitleText;
        public TMP_Text priceText;
        public TMP_Text oldPriceText;
        public TMP_Text discountBadgeText;
        public Button buyButton;

        private GameItem itemData;
        private System.Action<GameItem> onBuy;

        public void SetData(GameItem item, System.Action<GameItem> onClick = null)
        {
            itemData = item;
            onBuy = onClick;

            if (item == null)
            {
                Debug.LogWarning("[InvoSDK] FeaturedItemCard.SetData called with a null item.");
                return;
            }

            SetText(titleText, item.display_name);
            SetText(subtitleText, item.display_subtitle);

            // These cards are real-money packs, so USD is the correct label here.
            // Display only — the amount sent to the checkout endpoint goes through InvoFormat.Amount.
            SetText(priceText, $"${item.price_usd:F2}");

            if (oldPriceText != null)
            {
                bool hasOldPrice = item.original_price_usd.HasValue && item.original_price_usd.Value > item.price_usd;
                oldPriceText.gameObject.SetActive(hasOldPrice);
                if (hasOldPrice)
                    oldPriceText.text = $"${item.original_price_usd.Value:F2}";
            }

            if (discountBadgeText != null)
            {
                bool hasDiscount = item.discount_percentage.HasValue && item.discount_percentage.Value > 0;
                discountBadgeText.gameObject.SetActive(hasDiscount);
                if (hasDiscount)
                    discountBadgeText.text = $"-{item.discount_percentage.Value}%";
            }

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => onBuy?.Invoke(itemData));
            }

            if (itemImage != null)
                _ = InvoImageLoader.LoadIntoAsync(item.image_url, itemImage);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }
    }
}
