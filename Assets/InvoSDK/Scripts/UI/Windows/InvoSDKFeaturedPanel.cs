using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using static System.Net.WebRequestMethods;

namespace InvoSDK.UI
{
    public class InvoSDKFeaturedPanel : MonoBehaviour
    {
        [Header("Containers")]
        [SerializeField] private Transform weeklyFeaturedContainer;
        [SerializeField] private Transform weeklyItemsContainer;
        [SerializeField] private Transform dailyDealsContainer;

        [Header("Prefabs")]
        [SerializeField] private GameObject weeklyFeaturedPrefab;   // Featured bundle
        [SerializeField] private GameObject weeklyItemPrefab;       // Weekly currency packs
        [SerializeField] private GameObject dailyDealPrefab;        // Daily deals

        [Header("UI Feedback")]
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private GameObject loadingSpinner;

        private List<GameItem> allItems = new();
        private bool _isDestroyed = false;

        private async void Start()
        {
            SafeClear(weeklyFeaturedContainer);
            SafeClear(weeklyItemsContainer);
            SafeClear(dailyDealsContainer);
            await LoadFeaturedContent();
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
        }

        private async Task LoadFeaturedContent()
        {
            if (_isDestroyed) return;

            if (errorText != null) errorText.gameObject.SetActive(false);
            if (loadingSpinner != null) loadingSpinner.SetActive(true);

            SafeClear(weeklyFeaturedContainer);
            SafeClear(weeklyItemsContainer);
            SafeClear(dailyDealsContainer);

            await APIManager.Instance.GetGameItemsAsync(
                onSuccess: response =>
                {
                    if (_isDestroyed) return;

                    allItems = response?.items ?? new List<GameItem>();

                    PopulateWeeklyFeatured();
                    PopulateWeeklyItems();
                    PopulateDailyDeals();
                },
                onError: err =>
                {
                    if (_isDestroyed) return;

                    errorText.text = $"Failed to load store: {err}";
                    errorText.gameObject.SetActive(true);
                });

            if (loadingSpinner != null) loadingSpinner.SetActive(false);
        }
        private void PopulateWeeklyFeatured()
        {
            if (_isDestroyed || weeklyFeaturedPrefab == null || weeklyFeaturedContainer == null)
                return;

            var featuredBundle = allItems.FirstOrDefault(i =>
                string.Equals(i.item_category, "weekly", System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.item_type, "bundle", System.StringComparison.OrdinalIgnoreCase));

            if (featuredBundle == null)
            {
                Debug.Log("[InvoSDK] No weekly featured bundle found.");
                return;
            }

            var card = InstantiateSafe(weeklyFeaturedPrefab, weeklyFeaturedContainer);
            if (card == null) return;

            var ui = card.GetComponent<FeaturedItemCard>();
            if (ui != null)
            {
                ui.SetData(featuredBundle, OnPurchaseClicked);
                Debug.Log($"[InvoSDK] Weekly Featured: {featuredBundle.display_name}");
            }
            else
                Debug.LogWarning("[InvoSDK] weeklyFeaturedPrefab missing FeaturedItemCard component!");
        }

        private void PopulateWeeklyItems()
        {
            if (_isDestroyed)
            {
                Debug.LogWarning("[InvoSDK] Skipped weekly items, panel destroyed");
                return;
            }
            if (weeklyItemPrefab == null)
            {
                Debug.LogError("[InvoSDK] weeklyItemPrefab not assigned!");
                return;
            }
            if (weeklyItemsContainer == null)
            {
                Debug.LogError("[InvoSDK] weeklyItemsContainer not assigned!");
                return;
            }

            var weeklyItems = allItems
                .Where(i => string.Equals(i.item_category, "weekly", System.StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(i.item_type, "bundle", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            Debug.Log($"[InvoSDK] Weekly Items Found: {weeklyItems.Count}");

            SafeClear(weeklyItemsContainer);

            foreach (var item in weeklyItems)
            {
                var card = InstantiateSafe(weeklyItemPrefab, weeklyItemsContainer);
                if (card == null)
                {
                    Debug.LogError("[InvoSDK] Weekly item card instantiation failed!");
                    continue;
                }

                var ui = card.GetComponent<FeatureWeeklyItemCard>();
                if (ui != null)
                {
                    ui.SetData(item, OnPurchaseClicked);
                    Debug.Log($"[InvoSDK] Added Weekly Item: {item.display_name}");
                }
                else
                {
                    Debug.LogWarning("[InvoSDK] weeklyItemPrefab missing FeatureWeeklyItemCard component!");
                }
            }
        }


        private void PopulateDailyDeals()
        {
            if (_isDestroyed || dailyDealPrefab == null || dailyDealsContainer == null)
                return;

            // Strictly only daily category
            var dailyItems = allItems
                .Where(i => string.Equals(i.item_category, "daily", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            Debug.Log($"[InvoSDK] Daily Items Found: {dailyItems.Count}");

            if (dailyItems.Count == 0)
                return;

            SafeClear(dailyDealsContainer);

            foreach (var item in dailyItems)
            {
                var card = InstantiateSafe(dailyDealPrefab, dailyDealsContainer);
                if (card == null) continue;

                var ui = card.GetComponent<FeatureDailyItemCard>();
                if (ui != null)
                {
                    ui.SetData(item, OnPurchaseClicked);
                    Debug.Log($"[InvoSDK] Added Daily Deal: {item.display_name}");
                }
                else
                    Debug.LogWarning("[InvoSDK] dailyDealPrefab missing FeatureDailyItemCard component!");
            }
        }


        private void OnPurchaseClicked(GameItem item)
        {
            if (_isDestroyed) return;
            Debug.Log($"[InvoSDK] Purchase clicked for item: {item.display_name}");
            // Need to open Stripe Checkout URL
            Application.OpenURL($"https://console.invo.network/stripe-checkout/?game_secret={APIManager.Instance.ApiKey}&player_email={APIManager.Instance.GetPlayerEmail()}&usd_amount={item.price_usd}");
            //Application.OpenURL("https://console.invo.network/stripe-checkout/?game_secret=ivsdk_1k7q4ZYDkOeZ9IOYbEPnIP0zLUDUdDeNONZJOky6X3qaAFR69yAI3w7Bqx&player_email=zeerak.tahir@invogames.com&usd_amount=10");
            // Future use: 
            //InvoSDKPurchaseConfirmPanel.Instance.Show(item);
        }

        // ---------------- SAFE HELPERS ----------------
        private void SafeClear(Transform container)
        {
            if (container == null) return;
            var children = new List<GameObject>();
            foreach (Transform child in container)
                children.Add(child.gameObject);
            foreach (var obj in children)
                if (obj != null)
                    Destroy(obj);
        }

        private GameObject InstantiateSafe(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[InvoSDK] Tried to instantiate null prefab!");
                return null;
            }
            if (parent == null)
            {
                Debug.LogWarning("[InvoSDK] Tried to instantiate prefab with null parent!");
                return null;
            }
            return Instantiate(prefab, parent);
        }
    }
}
