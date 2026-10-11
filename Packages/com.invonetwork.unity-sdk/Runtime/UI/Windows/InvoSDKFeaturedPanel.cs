using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace InvoSDK.UI
{
    /// <summary>
    /// Store front for the server-hosted catalog (featured bundle, weekly packs, daily deals).
    ///
    /// These are REAL-MONEY packs. Buying one needs an Invo hosted-checkout session, and minting a
    /// session requires the SDK secret — which must never leave the developer's server. The game
    /// therefore calls a partner-hosted endpoint (<see cref="checkoutSessionEndpoint"/>) that mints
    /// the session server-side and hands back a signed checkout URL.
    /// </summary>
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

        [Header("Real-Money Checkout")]
        [Tooltip("HTTPS endpoint on YOUR server that mints an Invo checkout session.\n\n" +
                 "It receives POST { player_email, usd_amount } from the game and must reply with " +
                 "{ checkout_url }. Server-side it calls POST <invo-api>/api/checkout/sessions with " +
                 "the X-Game-Secret-Key header. The SDK secret must never be shipped in the client " +
                 "or put in a URL. See the README section 'Hosted checkout'.\n\n" +
                 "Leave blank to use InvoSDKConfig.checkoutSessionEndpoint, which the Setup Wizard " +
                 "sets. Fill this in only to override the shared value for this one panel.")]
        [SerializeField] private string checkoutSessionEndpoint;

        [Tooltip("Timeout, in seconds, for the call to your checkout-session endpoint.")]
        [SerializeField] private int checkoutTimeoutSeconds = 30;

        private const string CheckoutNotConfiguredMessage =
            "Real-money purchases need a server endpoint — see README.";

        private List<GameItem> allItems = new();
        private bool _isDestroyed = false;
        private bool _checkoutInFlight = false;
        private bool _warnedAboutCheckoutEndpoint = false;

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

            ClearMessage();
            if (loadingSpinner != null) loadingSpinner.SetActive(true);

            SafeClear(weeklyFeaturedContainer);
            SafeClear(weeklyItemsContainer);
            SafeClear(dailyDealsContainer);

            try
            {
                if (APIManager.Instance == null)
                {
                    ShowMessage("The store is unavailable right now.");
                    Debug.LogError("[InvoSDK] APIManager.Instance is null — add the APIManager prefab to the scene.");
                    return;
                }

                var response = await APIManager.Instance.GetGameItemsAsync();
                if (_isDestroyed) return;

                allItems = response?.items ?? new List<GameItem>();

                PopulateWeeklyFeatured();
                PopulateWeeklyItems();
                PopulateDailyDeals();
            }
            catch (InvoApiException ex)
            {
                if (_isDestroyed) return;
                // Never render ex.Body — it is the raw response and may carry PII or internals.
                Debug.LogError($"[InvoSDK] Failed to load the store. {ex}");
                ShowMessage(ex.IsNetworkError
                    ? "No connection. Check your network and try again."
                    : "The store could not be loaded. Please try again.");
            }
            catch (Exception ex)
            {
                if (_isDestroyed) return;
                Debug.LogError($"[InvoSDK] Failed to load the store: {ex.Message}");
                ShowMessage("The store could not be loaded. Please try again.");
            }
            finally
            {
                // finally, not a trailing statement: an exception used to skip straight past this
                // and leave the spinner turning forever.
                if (!_isDestroyed && loadingSpinner != null)
                    loadingSpinner.SetActive(false);
            }
        }

        private void PopulateWeeklyFeatured()
        {
            if (_isDestroyed || weeklyFeaturedPrefab == null || weeklyFeaturedContainer == null)
                return;

            var featuredBundle = GetFeaturedItem();
            if (featuredBundle == null)
            {
                Debug.Log("[InvoSDK] No featured item found.");
                return;
            }

            var card = InstantiateSafe(weeklyFeaturedPrefab, weeklyFeaturedContainer);
            if (card == null) return;

            var ui = card.GetComponent<FeaturedItemCard>();
            if (ui != null)
            {
                ui.SetData(featuredBundle, OnPurchaseClicked);
                Debug.Log($"[InvoSDK] Featured: {featuredBundle.display_name}");
            }
            else
                Debug.LogWarning("[InvoSDK] weeklyFeaturedPrefab missing FeaturedItemCard component!");
        }

        /// <summary>
        /// The server marks the hero item with <c>is_featured</c>. The old magic-string heuristic
        /// (item_category == "weekly" &amp;&amp; item_type == "bundle") ignored that field and picked
        /// the wrong item — or nothing at all — whenever merchandising changed.
        /// </summary>
        private GameItem GetFeaturedItem() => allItems.FirstOrDefault(i => i != null && i.is_featured);

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

            // Category still drives the section split; is_featured only decides which item is
            // promoted to the hero slot, and that one is not repeated in the row below it.
            var featured = GetFeaturedItem();
            var weeklyItems = allItems
                .Where(i => i != null
                            && !ReferenceEquals(i, featured)
                            && string.Equals(i.item_category, "weekly", StringComparison.OrdinalIgnoreCase))
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
                    ui.SetData(item, OnPurchaseClicked);
                else
                    Debug.LogWarning("[InvoSDK] weeklyItemPrefab missing FeatureWeeklyItemCard component!");
            }
        }

        private void PopulateDailyDeals()
        {
            if (_isDestroyed || dailyDealPrefab == null || dailyDealsContainer == null)
                return;

            var featured = GetFeaturedItem();
            var dailyItems = allItems
                .Where(i => i != null
                            && !ReferenceEquals(i, featured)
                            && string.Equals(i.item_category, "daily", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Debug.Log($"[InvoSDK] Daily Items Found: {dailyItems.Count}");

            SafeClear(dailyDealsContainer);

            if (dailyItems.Count == 0)
                return;

            foreach (var item in dailyItems)
            {
                var card = InstantiateSafe(dailyDealPrefab, dailyDealsContainer);
                if (card == null) continue;

                var ui = card.GetComponent<FeatureDailyItemCard>();
                if (ui != null)
                    ui.SetData(item, OnPurchaseClicked);
                else
                    Debug.LogWarning("[InvoSDK] dailyDealPrefab missing FeatureDailyItemCard component!");
            }
        }

        // ---------------- CHECKOUT ----------------

        private void OnPurchaseClicked(GameItem item)
        {
            if (_isDestroyed || item == null) return;
            _ = BeginCheckoutAsync(item);
        }

        /// <summary>
        /// Opens Invo hosted checkout for a real-money pack.
        ///
        /// TODO (integrator): host the endpoint this calls. Your server must POST to
        /// <c>{invo-api}/api/checkout/sessions</c> with the header <c>X-Game-Secret-Key: &lt;sdk key&gt;</c>
        /// and a body of <c>{ player_email, usd_amount }</c> (optionally success_url, cancel_url,
        /// metadata), then return the <c>checkout_url</c> from that response to the game. The URL is
        /// signed, single-use and expires in 15 minutes.
        ///
        /// The predecessor of this method put the game's master secret straight into a browser URL
        /// (<c>?game_secret=...</c>) against a host that no longer serves that route. It is gone;
        /// there is deliberately no fallback to it.
        /// </summary>
        private async Task BeginCheckoutAsync(GameItem item)
        {
            if (_checkoutInFlight) return;

            string endpoint = ResolveCheckoutEndpoint();
            if (!IsValidEndpoint(endpoint))
            {
                ShowMessage(CheckoutNotConfiguredMessage);
                if (!_warnedAboutCheckoutEndpoint)
                {
                    _warnedAboutCheckoutEndpoint = true;
                    Debug.LogError(
                        "[InvoSDK] Real-money checkout is disabled: no https checkout-session endpoint is set. " +
                        "Set it in InvoSDK > Setup Wizard (InvoSDKConfig.checkoutSessionEndpoint), or override it " +
                        "on this panel's inspector field. Point it at an endpoint on YOUR server that accepts " +
                        "POST { player_email, usd_amount } and returns { checkout_url }; server-side that endpoint " +
                        "calls POST /api/checkout/sessions with the X-Game-Secret-Key header. The SDK secret must " +
                        "never be sent from the client. See README > Hosted checkout.");
                }
                return;
            }

            var api = APIManager.Instance;
            if (api == null)
            {
                ShowMessage("The store is unavailable right now.");
                Debug.LogError("[InvoSDK] APIManager.Instance is null — add the APIManager prefab to the scene.");
                return;
            }

            string playerEmail = api.GetPlayerEmail();
            if (string.IsNullOrWhiteSpace(playerEmail))
            {
                ShowMessage("Sign in before making a purchase.");
                Debug.LogError("[InvoSDK] No player email in config — cannot start checkout.");
                return;
            }

            _checkoutInFlight = true;
            ClearMessage();

            try
            {
                // price_usd is a float on the model; cast at the boundary and format invariantly.
                // A raw ToString() would emit "9,99" on a comma-decimal device and the backend's
                // Decimal(str(...)) parse would reject it.
                decimal usdAmount = (decimal)item.price_usd;
                var payload = new Dictionary<string, object>
                {
                    ["player_email"] = playerEmail,
                    ["usd_amount"] = InvoFormat.Amount(usdAmount)
                };

                string checkoutUrl = await RequestCheckoutUrlAsync(endpoint, payload);
                if (_isDestroyed) return;

                if (string.IsNullOrEmpty(checkoutUrl))
                {
                    ShowMessage("Checkout could not be started. Please try again.");
                    return;
                }

                Debug.Log($"[InvoSDK] Opening checkout for: {item.display_name}");
                Application.OpenURL(checkoutUrl);
            }
            catch (Exception ex)
            {
                if (_isDestroyed) return;
                Debug.LogError($"[InvoSDK] Checkout session request failed: {ex.Message}");
                ShowMessage("Checkout could not be started. Please try again.");
            }
            finally
            {
                _checkoutInFlight = false;
            }
        }

        private async Task<string> RequestCheckoutUrlAsync(string endpoint, Dictionary<string, object> payload)
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

            using var req = new UnityWebRequest(endpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Mathf.Clamp(checkoutTimeoutSeconds, 5, 120);
            req.SetRequestHeader("Content-Type", "application/json");
            // Deliberately no SDK key: this is the partner's own endpoint, and the secret stays server-side.

            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success || req.responseCode < 200 || req.responseCode > 299)
            {
                Debug.LogError($"[InvoSDK] Checkout endpoint returned {req.responseCode} ({req.error}).");
                return null;
            }

            string text = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogError("[InvoSDK] Checkout endpoint returned an empty body; expected { \"checkout_url\": ... }.");
                return null;
            }

            string url;
            try
            {
                url = JObject.Parse(text)["checkout_url"]?.ToString();
            }
            catch (JsonException)
            {
                Debug.LogError("[InvoSDK] Checkout endpoint returned a body that is not JSON.");
                return null;
            }

            if (!IsValidEndpoint(url))
            {
                Debug.LogError("[InvoSDK] Checkout endpoint did not return an https checkout_url.");
                return null;
            }

            return url.Trim();
        }

        /// <summary>https only — a checkout URL carries a signed session token and must not travel in clear.</summary>
        /// <summary>
        /// Returns the checkout-session endpoint to use: this panel's inspector override when set,
        /// otherwise the shared value from <c>InvoSDKConfig</c> that the Setup Wizard writes.
        /// A project with one store surface configures it once in the wizard; a project with several
        /// can point an individual panel somewhere else without touching the shared config.
        /// </summary>
        private string ResolveCheckoutEndpoint()
        {
            string local = (checkoutSessionEndpoint ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(local)) return local;

            var config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            return (config?.checkoutSessionEndpoint ?? string.Empty).Trim();
        }

        private static bool IsValidEndpoint(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------- SAFE HELPERS ----------------

        private void ShowMessage(string message)
        {
            if (errorText == null)
            {
                Debug.LogWarning($"[InvoSDK] {message} (no errorText assigned on InvoSDKFeaturedPanel)");
                return;
            }
            errorText.text = message;
            errorText.gameObject.SetActive(true);
        }

        private void ClearMessage()
        {
            if (errorText != null) errorText.gameObject.SetActive(false);
        }

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
