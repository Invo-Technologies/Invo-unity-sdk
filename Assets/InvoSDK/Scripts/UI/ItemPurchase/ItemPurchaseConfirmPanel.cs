using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// Confirmation step for an in-game-currency item purchase.
    ///
    /// The panel instance is reused for every purchase in a session, so every field that
    /// <see cref="ConfirmPurchase"/> mutates has to be reset in <see cref="Setup"/> — the button
    /// used to stay dead after the first successful purchase because only the error paths
    /// restored it.
    /// </summary>
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
        [Tooltip("Optional. Updated straight from the purchase response so the player does not " +
                 "have to wait for the next balance poll.")]
        [SerializeField] private TMP_Text balanceText;

        [Header("Action Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;

        [Header("Warnings / Notes")]
        [SerializeField] private TMP_Text warningText;
        [SerializeField] private TMP_Text infoText;

        // TODO: no quantity control exists in this layout. The API accepts 1..1000 and requires
        // total_price == unit_price * quantity, so the invariant is computed explicitly below;
        // wire a stepper to this value when the layout gains one.
        private const int PurchaseQuantity = 1;

        private const string DefaultConfirmLabel = "Purchase";
        private const int FallbackCooldownSeconds = 30;
        private const int MaxCooldownSeconds = 600;

        private InvoSDKItem currentItem;

        // Idempotency key for ONE user intent. Minted on the first tap of Confirm and reused for
        // every retry of that same intent, so a retry after a dropped response is de-duplicated
        // by the server instead of debiting the player twice.
        private string pendingClientRequestId;

        private bool isPurchasing;
        private bool isComplete;
        private Coroutine cooldownRoutine;

        /// <summary>
        /// Initialize confirm panel with item data. Also resets all transient purchase state.
        /// </summary>
        public void Setup(InvoSDKItem item)
        {
            currentItem = item;
            if (item == null)
            {
                Debug.LogError("[InvoSDK] ItemPurchaseConfirmPanel.Setup called with a null item.");
                return;
            }

            ResetPurchaseState();

            var api = APIManager.Instance;

            // --- Top Section ---
            SetText(itemNameText, item.itemName);
            // ToUpperInvariant, not ToUpper: the Turkish 'i' would otherwise change the word, and
            // a blank rarity used to throw a NullReferenceException here.
            SetText(itemRarityText, string.IsNullOrWhiteSpace(item.rarity)
                ? string.Empty
                : item.rarity.ToUpperInvariant());
            SetText(itemDescriptionText, item.itemDescription);

            // --- Details Section ---
            // InvoSDKItem.priceUSD is a float and the name is misleading: item purchases settle in
            // GAME CURRENCY, not USD. Cast at the boundary and label it with the game currency so
            // the player is not told "USD" for something charged in Gems (field owned by the models
            // agent, so it is not renamed here).
            decimal unitPrice = (decimal)item.priceUSD;
            decimal totalPrice = unitPrice * PurchaseQuantity;
            string currency = api != null ? api.GetGameCurrency() : string.Empty;
            SetText(priceText, FormatCurrency(totalPrice, currency));

            SetText(playerNameText, ResolvePlayerName(api));
            SetText(gameNameText, api != null ? api.GetGameName() : string.Empty);
            SetText(balanceText, string.Empty);

            // --- Image ---
            if (item.itemSprite != null)
            {
                if (itemImage != null) itemImage.sprite = item.itemSprite;
            }
            else if (itemImage != null)
            {
                _ = InvoImageLoader.LoadIntoAsync(item.imageUrl, itemImage);
            }

            // --- Info & Warning ---
            SetText(infoText, string.IsNullOrEmpty(currency)
                ? "This item will be purchased using your in-game balance."
                : $"This item will be purchased using your {currency} balance.");
            // ShowWarning, not SetText: a previous run may have hidden the label.
            ShowWarning("This action cannot be undone.");

            // --- Button Events ---
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(ClosePanel);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(() => _ = ConfirmPurchase());
            }
        }

        private void ResetPurchaseState()
        {
            StopCooldown();
            isPurchasing = false;
            isComplete = false;
            pendingClientRequestId = null;

            // Restored here, not only in the error paths: the panel is reused, and the success
            // path leaving the button disabled made the second purchase of a session impossible.
            if (confirmButton != null) confirmButton.interactable = true;
            SetText(confirmButtonText, DefaultConfirmLabel);
        }

        private void OnDisable()
        {
            StopCooldown();
            isPurchasing = false;
        }

        /// <summary>
        /// Confirms purchase and triggers backend call.
        /// </summary>
        private async Task ConfirmPurchase()
        {
            if (isPurchasing || isComplete) return;

            if (currentItem == null)
            {
                ShowWarning("Nothing selected to purchase.");
                return;
            }

            var api = APIManager.Instance;
            if (api == null)
            {
                ShowWarning("The store is not ready yet. Try again in a moment.");
                Debug.LogError("[InvoSDK] APIManager.Instance is null — add the APIManager prefab to the scene.");
                return;
            }

            // One key per intent, minted on the first tap and kept across retries.
            if (string.IsNullOrEmpty(pendingClientRequestId))
                pendingClientRequestId = APIManager.NewClientRequestId();

            isPurchasing = true;
            if (confirmButton != null) confirmButton.interactable = false;
            SetText(confirmButtonText, "Processing...");
            ShowWarning(string.Empty);

            // decimal, not float: the API takes decimal and total_price must equal
            // unit_price * quantity exactly. priceUSD is a float on the model, so cast once here.
            decimal unitPrice = (decimal)currentItem.priceUSD;
            decimal totalPrice = unitPrice * PurchaseQuantity;

            try
            {
                var response = await api.PurchaseItemAsync(
                    clientRequestId: pendingClientRequestId,
                    playerEmail: api.GetPlayerEmail(),
                    playerName: ResolvePlayerName(api),
                    itemId: currentItem.itemId,
                    itemName: currentItem.itemName,
                    quantity: PurchaseQuantity,
                    unitPrice: unitPrice,
                    totalPrice: totalPrice,
                    itemCategory: currentItem.category,
                    itemDescription: currentItem.itemDescription);

                HandleSuccess(response);
            }
            catch (InvoApiException ex)
            {
                HandleApiError(ex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InvoSDK] Purchase exception: {ex.Message}");
                SetText(confirmButtonText, "Try Again");
                // The idempotency key is kept, so tapping again cannot charge the player twice.
                ShowWarning("Something went wrong. Tap to try again.");
                if (confirmButton != null) confirmButton.interactable = true;
            }
            finally
            {
                isPurchasing = false;
            }
        }

        private void HandleSuccess(PurchaseItemResponse response)
        {
            // A 2xx can still carry a non-success status — do not tell the player the item is
            // theirs until the body says so.
            if (response != null && !response.IsSuccess)
            {
                Debug.LogWarning($"[InvoSDK] Purchase returned status '{response.status}'.");
                SetText(confirmButtonText, "Try Again");
                ShowWarning("That purchase could not be completed.");
                if (confirmButton != null) confirmButton.interactable = true;
                return;
            }

            isComplete = true;

            string itemName = response?.purchase_details != null
                ? response.purchase_details.item_name
                : currentItem?.itemName;
            Debug.Log($"[InvoSDK] Purchased: {itemName}");

            SetText(confirmButtonText, "Purchase Complete");
            if (confirmButton != null) confirmButton.interactable = false;
            ShowWarning(string.Empty);

            ApplyNewBalance(response);
            Invoke(nameof(ClosePanel), 1.2f);
        }

        /// <summary>
        /// A replayed client_request_id comes back as 409 — the server is saying "this purchase is
        /// already complete", not "it failed". Reporting it as a failure invited the player to buy
        /// again after they had already been debited.
        /// </summary>
        private void HandleApiError(InvoApiException ex)
        {
            if (ex.IsDuplicate)
            {
                Debug.Log($"[InvoSDK] Purchase already completed (duplicate request). {ex}");
                isComplete = true;
                SetText(confirmButtonText, "Purchase Complete");
                if (confirmButton != null) confirmButton.interactable = false;
                ShowWarning("This purchase already went through.");
                RefreshBalanceFromServer();
                Invoke(nameof(ClosePanel), 1.2f);
                return;
            }

            if (ex.IsRateLimited)
            {
                // The five anti-abuse lockouts (insufficient balance, rapid same-item, per-player
                // spam, invalid item, malformed request) all land here. Re-enabling the button lets
                // the player hammer it and extend their own cooldown, so hold it for retry_after.
                int seconds = Mathf.Clamp(ex.RetryAfterSeconds ?? FallbackCooldownSeconds, 1, MaxCooldownSeconds);
                Debug.LogWarning($"[InvoSDK] Purchase throttled for {seconds}s. {ex}");
                ShowWarning("Too many attempts. Please wait before trying again.");
                StartCooldown(seconds);
                return;
            }

            Debug.LogError($"[InvoSDK] Purchase failed. {ex}");
            SetText(confirmButtonText, "Try Again");
            ShowWarning(DescribeError(ex));
            // Same pendingClientRequestId is kept: a retry of this intent must not be charged twice.
            if (confirmButton != null) confirmButton.interactable = true;
        }

        private static string DescribeError(InvoApiException ex)
        {
            if (ex.IsNetworkError)
                return "No connection. Check your network and try again.";
            if (ex.StatusCode == 401 || ex.StatusCode == 403)
                return "This game is not authorised to complete the purchase.";
            if (ex.StatusCode >= 500)
                return "The store is having trouble. Please try again shortly.";
            if (string.Equals(ex.ErrorCode, "INSUFFICIENT_BALANCE", StringComparison.OrdinalIgnoreCase))
                return "You do not have enough balance for this item.";
            // Never render ex.Body: it is the raw response and may contain PII.
            return "That purchase could not be completed.";
        }

        private void StartCooldown(int seconds)
        {
            StopCooldown();
            if (confirmButton != null) confirmButton.interactable = false;

            if (!isActiveAndEnabled)
            {
                SetText(confirmButtonText, DefaultConfirmLabel);
                return;
            }

            cooldownRoutine = StartCoroutine(CooldownRoutine(seconds));
        }

        private void StopCooldown()
        {
            if (cooldownRoutine != null)
            {
                StopCoroutine(cooldownRoutine);
                cooldownRoutine = null;
            }
        }

        private IEnumerator CooldownRoutine(int seconds)
        {
            int remaining = seconds;
            while (remaining > 0)
            {
                SetText(confirmButtonText, $"Try again in {remaining}s");
                yield return new WaitForSecondsRealtime(1f);
                remaining--;
            }

            cooldownRoutine = null;
            SetText(confirmButtonText, DefaultConfirmLabel);
            if (!isComplete && confirmButton != null)
                confirmButton.interactable = true;
        }

        /// <summary>
        /// new_balance is the canonical top-level field of the write-response contract;
        /// balance_info.new_balance is the legacy nested copy and is only a fallback.
        /// </summary>
        private void ApplyNewBalance(PurchaseItemResponse response)
        {
            if (response == null) return;

            // Interpolated rather than cast so this keeps compiling whether the model declares
            // new_balance as a string or a decimal.
            string newBalance = $"{response.new_balance}";
            if (string.IsNullOrWhiteSpace(newBalance) && response.balance_info != null)
                newBalance = response.balance_info.new_balance;

            if (!string.IsNullOrWhiteSpace(newBalance))
            {
                string currency = $"{response.currency_name}";
                if (string.IsNullOrWhiteSpace(currency) && response.purchase_details != null)
                    currency = response.purchase_details.currency_name;
                if (string.IsNullOrWhiteSpace(currency) && APIManager.Instance != null)
                    currency = APIManager.Instance.GetGameCurrency();

                SetText(balanceText, string.IsNullOrWhiteSpace(currency)
                    ? newBalance
                    : $"{newBalance} {currency}");
            }

            RefreshBalanceFromServer();
        }

        /// <summary>Kicks the shared balance label immediately instead of waiting out the poll interval.</summary>
        private void RefreshBalanceFromServer()
        {
            if (APIManager.Instance != null)
                APIManager.Instance.StartBalancePolling();
        }

        private static string ResolvePlayerName(APIManager api)
        {
            // The name, never the email: every receipt and every purchase record used to read
            // "Test Player", and this label used to render the player's email address.
            string name = api != null ? api.GetPlayerName() : null;
            return string.IsNullOrWhiteSpace(name) ? "Player" : name;
        }

        /// <summary>Display-only formatting. Wire values always go through InvoFormat.Amount.</summary>
        private static string FormatCurrency(decimal amount, string currency) =>
            string.IsNullOrEmpty(currency) ? $"{amount:0.##}" : $"{amount:0.##} {currency}";

        private void ShowWarning(string message)
        {
            if (warningText == null) return;
            warningText.text = message;
            warningText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private void ClosePanel()
        {
            gameObject.SetActive(false);
        }
    }
}
