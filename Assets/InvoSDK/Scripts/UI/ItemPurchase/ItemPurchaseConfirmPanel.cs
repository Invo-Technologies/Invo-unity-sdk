using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;

namespace InvoSDK.UI
{
    public class ItemPurchaseConfirmPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image itemImage;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text itemRarityText;
        [SerializeField] private TMP_Text itemDescriptionText;

        [Header("Details Section")]
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text gameNameText;

        [Header("Action Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;

        [Header("Warnings / Notes")]
        [SerializeField] private TMP_Text warningText;
        [SerializeField] private TMP_Text infoText;

        private InvoSDKItem currentItem;

        /// <summary>
        /// Initialize confirm panel with item data.
        /// </summary>
        public void Setup(InvoSDKItem item)
        {
            currentItem = item;

            // --- Top Section ---
            itemNameText.text = item.itemName;
            itemRarityText.text = item.rarity.ToUpper();
            itemDescriptionText.text = item.itemDescription;

            // --- Details Section ---
            priceText.text = $"{item.priceUSD} {APIManager.Instance.GetGameCurrency()}";
            playerNameText.text = APIManager.Instance.GetPlayerEmail();
            gameNameText.text = APIManager.Instance.GetGameName();

            confirmButtonText.text = $"Purchase";

            // --- Image ---
            if (item.itemSprite != null)
                itemImage.sprite = item.itemSprite;
            else if (!string.IsNullOrEmpty(item.imageUrl))
                _ = APIManager.LoadSpriteAsync(item.imageUrl, itemImage);

            // --- Info & Warning ---
            infoText.text = $"This item will be purchased using your in-game balance.";
            warningText.text = "⚠ This action cannot be undone.";

            // --- Button Events ---
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => ClosePanel());

            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => _ = ConfirmPurchase());
        }

        /// <summary>
        /// Confirms purchase and triggers backend call.
        /// </summary>
        private async Task ConfirmPurchase()
        {
            //Debug.Log("Click Purchase");
            confirmButton.interactable = false;
            confirmButtonText.text = "Processing...";

            try
            {
                await APIManager.Instance.PurchaseItemAsync(
                    playerEmail: APIManager.Instance.GetPlayerEmail(),
                    playerName: "Test Player",
                    itemId: currentItem.itemId,
                    itemName: currentItem.itemName,
                    quantity: 1,
                    unitPrice: currentItem.priceUSD,
                    totalPrice: currentItem.priceUSD,
                    onSuccess: (response) =>
                    {
                        Debug.Log($"[InvoSDK] ✅ Purchased: {response.purchase_details.item_name}");
                        confirmButtonText.text = "Purchase Complete ✅";
                        Invoke(nameof(ClosePanel), 1.2f);
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"[InvoSDK] ❌ Purchase failed: {error}");
                        confirmButtonText.text = "Purchase Failed!";
                        confirmButton.interactable = true;
                    }
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InvoSDK] Purchase Exception: {ex.Message}");
                confirmButtonText.text = "Error Occurred!";
                confirmButton.interactable = true;
            }
        }

        private void ClosePanel()
        {
            gameObject.SetActive(false);
        }
    }
}
