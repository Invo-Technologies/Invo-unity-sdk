using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace InvoSDK.UI
{
    /// <summary>
    /// Grid of locally-authored catalog items (<see cref="InvoSDKItemCatalog"/>), each of which
    /// is bought with the player's in-game currency through the item-purchase API.
    ///
    /// NOTE FOR INTEGRATORS: this local catalog and the server catalog returned by
    /// <c>APIManager.GetGameItemsAsync</c> are two disjoint paths. Server items (<c>GameItem</c>)
    /// cannot be routed through this panel today because the two shapes are not compatible
    /// (item_id/itemId, price_usd/priceUSD, and GameItem carries no description).
    /// </summary>
    public class ItemPurchasePanel : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private Transform gridContainer;
        [SerializeField] private ItemCardView itemCardPrefab;
        [SerializeField] private ItemPurchaseConfirmPanel confirmPanel;

        [Header("Empty State")]
        [Tooltip("Shown when the catalog asset is missing or contains no items.")]
        [SerializeField] private TMP_Text emptyStateText;

        private const string CatalogResourceName = "InvoSDKItemCatalog";

        // Never null. PopulateGrid used to iterate the field straight after a failed load,
        // which threw a NullReferenceException on every project without the catalog asset.
        private readonly List<InvoSDKItem> catalogItems = new List<InvoSDKItem>();
        private string emptyStateMessage;

        private void OnEnable()
        {
            LoadCatalog();
            PopulateGrid();
        }

        private void LoadCatalog()
        {
            catalogItems.Clear();
            emptyStateMessage = null;

            var catalog = Resources.Load<InvoSDKItemCatalog>(CatalogResourceName);
            if (catalog == null)
            {
                emptyStateMessage = "The item catalog is unavailable right now.";
                Debug.LogError(
                    $"[InvoSDK] No item catalog found. Create one via InvoSDK > Item Catalog and save it as " +
                    $"Assets/InvoSDK/Resources/{CatalogResourceName}.asset — the store will stay empty until then.");
                return;
            }

            if (catalog.items == null || catalog.items.Count == 0)
            {
                emptyStateMessage = "No items are available right now.";
                return;
            }

            foreach (var item in catalog.items)
            {
                if (item != null)
                    catalogItems.Add(item);
            }

            if (catalogItems.Count == 0)
                emptyStateMessage = "No items are available right now.";
        }

        private void PopulateGrid()
        {
            if (gridContainer == null)
            {
                Debug.LogError("[InvoSDK] ItemPurchasePanel.gridContainer is not assigned.");
                return;
            }

            foreach (Transform child in gridContainer)
                Destroy(child.gameObject);

            if (catalogItems.Count == 0)
            {
                ShowEmptyState(emptyStateMessage ?? "No items are available right now.");
                return;
            }

            if (itemCardPrefab == null)
            {
                ShowEmptyState("The store could not be displayed.");
                Debug.LogError("[InvoSDK] ItemPurchasePanel.itemCardPrefab is not assigned.");
                return;
            }

            HideEmptyState();

            foreach (var item in catalogItems)
            {
                var card = Instantiate(itemCardPrefab, gridContainer);
                card.Initialize(item, OnPurchaseClicked);
            }
        }

        private void ShowEmptyState(string message)
        {
            if (emptyStateText == null) return;
            emptyStateText.text = message;
            emptyStateText.gameObject.SetActive(true);
        }

        private void HideEmptyState()
        {
            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(false);
        }

        private void OnPurchaseClicked(InvoSDKItem item)
        {
            if (item == null) return;

            if (confirmPanel == null)
            {
                Debug.LogError("[InvoSDK] ItemPurchasePanel.confirmPanel is not assigned — cannot confirm a purchase.");
                return;
            }

            confirmPanel.Setup(item);
            confirmPanel.gameObject.SetActive(true);
        }
    }
}
