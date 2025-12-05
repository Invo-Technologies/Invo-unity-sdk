using System.Collections.Generic;
using UnityEngine;

namespace InvoSDK.UI
{
    public class ItemPurchasePanel : MonoBehaviour
    {
        [SerializeField] private Transform gridContainer;
        [SerializeField] private ItemCardView itemCardPrefab;
        [SerializeField] ItemPurchaseConfirmPanel confirmPanel;
        private List<InvoSDKItem> catalogItems;

        private void OnEnable()
        {
            LoadCatalog();
            PopulateGrid();
        }

        private void LoadCatalog()
        {
            var catalog = Resources.Load<InvoSDKItemCatalog>("InvoSDKItemCatalog");
            if (catalog != null)
                catalogItems = catalog.items;
            else
                Debug.LogWarning("[InvoSDK] No Item Catalog found!");
        }

        private void PopulateGrid()
        {
            foreach (Transform child in gridContainer)
                Destroy(child.gameObject);

            foreach (var item in catalogItems)
            {
                var card = Instantiate(itemCardPrefab, gridContainer);
                card.Initialize(item, OnPurchaseClicked);
            }
        }

        private async void OnPurchaseClicked(InvoSDKItem item)
        {
            Debug.Log($"[InvoSDK] Purchasing item: {item.itemName}");
            confirmPanel.Setup(item);

            confirmPanel.gameObject.SetActive(true);

        }
    }
}
