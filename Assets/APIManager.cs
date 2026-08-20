using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using static InvoSDK.TransferResponse;

namespace InvoSDK
{
    /// <summary>
    /// The <c>status</c> strings the Invo API returns on a 2xx response.
    /// A 200 does not mean the operation completed — it means the request was understood.
    /// Always branch on the status string, never on the HTTP code alone.
    /// </summary>
    public static class InvoStatus
    {
        public const string Success = "success";
        public const string NeedsAccountSelection = "needs_account_selection";
        public const string PendingGuardianApproval = "pending_guardian_approval";
        public const string PendingConfirmation = "pending_confirmation";
        public const string Resent = "resent";
    }

    /// <summary>
    /// Transport layer for the Invo API.
    /// Every endpoint authenticates with the game secret in the <c>X-Game-Secret-Key</c> header
    /// (plus a copy in the body for <c>/v1/game-items/list</c>); there is no player login,
    /// bearer token or CSRF handshake on these routes.
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        public static APIManager Instance;

        // The API roots are fixed per environment — they are not inspector-tunable, because
        // a mistyped host silently sends the game secret to somewhere that is not Invo.
        private const string ProductionApiBase = "https://invo.network/api";
        private const string SandboxApiBase = "https://sandbox.invo.network/sandbox/api";

        [Header("Configuration")]
        [SerializeField] private bool useProduction = false;
        [SerializeField] private int requestTimeoutSeconds = 30;

        [Header("Balance Display")]
        [SerializeField] private TextMeshProUGUI playerbalance;
        [SerializeField] private float balancePollInterval = 10f;
        [SerializeField] private float maxBalancePollInterval = 300f;

        private InvoSDKConfig config;

        private bool isPollingActive = true;
        private bool isBalanceRequestInFlight;
        private int consecutiveBalanceFailures;
        private float balanceTimer;
        private float nextPollDelay;
        private bool warnedAboutMissingEmail;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            if (config == null)
            {
                Debug.LogError("[InvoSDK] Config not found! Please run Setup Wizard.");
                return;
            }

            useProduction = config.useProduction;
            nextPollDelay = balancePollInterval;
        }

        private void Start()
        {
            if (isPollingActive)
                _ = PollPlayerBalanceAsync();
        }

        private void Update()
        {
            if (!isPollingActive)
                return;

            balanceTimer += Time.deltaTime;

            if (balanceTimer >= nextPollDelay)
            {
                balanceTimer = 0f;
                _ = PollPlayerBalanceAsync();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // =========================
        // ENVIRONMENT / CONFIG
        // =========================
        public string ApiBase => useProduction ? ProductionApiBase : SandboxApiBase;
        public string ApiKey => config != null ? config.sdkKey : string.Empty;
        public string GameId => config != null ? config.gameId : string.Empty;

        public string GetPlayerEmail() => config != null ? config.playerEmail : string.Empty;
        public string GetPlayerName() => config != null ? config.playerName : string.Empty;
        public string GetGameName() => config != null ? config.gameName : string.Empty;
        public string GetGameCurrency() => config != null ? config.gameCurrencyName : string.Empty;

        /// <summary>
        /// Mints an idempotency key. Callers create ONE per user intent (one tap of "Send")
        /// and reuse it across every retry of that intent, so a resend after a dropped
        /// response is de-duplicated by the server instead of charging the player twice.
        /// </summary>
        public static string NewClientRequestId() => Guid.NewGuid().ToString();

        // =========================
        // BALANCE POLLING
        // =========================

        /// <summary>Resumes the balance poll loop and clears any backoff.</summary>
        public void StartBalancePolling()
        {
            isPollingActive = true;
            consecutiveBalanceFailures = 0;
            nextPollDelay = balancePollInterval;
            balanceTimer = 0f;
            _ = PollPlayerBalanceAsync();
        }

        /// <summary>Stops the balance poll loop. An in-flight request still completes.</summary>
        public void StopBalancePolling()
        {
            isPollingActive = false;
        }

        /// <summary>
        /// One balance request at a time. Overlapping polls used to queue up behind a slow
        /// or rate-limited server and then all land at once, which is what triggered the lockout.
        /// </summary>
        private async Task PollPlayerBalanceAsync()
        {
            if (isBalanceRequestInFlight)
                return;

            string email = GetPlayerEmail();
            if (string.IsNullOrEmpty(email))
            {
                if (!warnedAboutMissingEmail)
                {
                    warnedAboutMissingEmail = true;
                    Debug.LogWarning("[InvoSDK] Missing player email in config — balance polling is idle.");
                }
                return;
            }

            isBalanceRequestInFlight = true;
            try
            {
                var response = await GetPlayerBalanceAsync(email);
                consecutiveBalanceFailures = 0;
                nextPollDelay = balancePollInterval;
                ApplyBalanceToLabel(response);
            }
            catch (InvoApiException ex)
            {
                consecutiveBalanceFailures = Math.Min(consecutiveBalanceFailures + 1, 8);
                nextPollDelay = BackoffDelay(consecutiveBalanceFailures, ex.RetryAfterSeconds);
                Debug.LogWarning($"[InvoSDK] Balance poll failed, next attempt in {nextPollDelay:0}s. {ex}");
                SetBalanceLabel("-");
            }
            finally
            {
                isBalanceRequestInFlight = false;
            }
        }

        private float BackoffDelay(int failures, int? retryAfterSeconds)
        {
            float delay = Mathf.Min(balancePollInterval * Mathf.Pow(2f, failures - 1), maxBalancePollInterval);
            if (retryAfterSeconds.HasValue)
                delay = Mathf.Max(delay, retryAfterSeconds.Value);
            return delay;
        }

        private void ApplyBalanceToLabel(PlayerBalanceResponse response)
        {
            // available_balance, not total_balance: reserved funds are committed to pending
            // sends and cannot be spent, and showing them makes a purchase look affordable
            // right up until the server refuses it.
            if (response != null && response.balances != null && response.balances.Count > 0)
                SetBalanceLabel(response.balances[0].available_balance);
            else
                SetBalanceLabel("-");
        }

        private void SetBalanceLabel(string text)
        {
            if (playerbalance != null)
                playerbalance.text = string.IsNullOrEmpty(text) ? "-" : text;
        }

        // =========================
        // BALANCE
        // =========================

        /// <summary>Reads the player's per-currency balances for this game.</summary>
        public Task<PlayerBalanceResponse> GetPlayerBalanceAsync(
            string email,
            Action<PlayerBalanceResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/player-balances/player/by-email/{UnityWebRequest.EscapeURL(email)}";
            return GetAsync(url, onSuccess, onError);
        }

        // =========================
        // GAME ITEMS
        // =========================

        /// <summary>
        /// Lists storefront items. Note this returns every active item across ALL games on the
        /// network, not just yours — the backend returns the same response regardless of which
        /// game is asking. Filter on <c>GameItem.game_id</c> if you only want your own.
        /// The secret is sent in the body as well as the header: the backend's
        /// <c>validate_request</c> for this route reads the body only, so
        /// <c>game_secret</c> is required, not redundant.
        /// </summary>
        public Task<GameItemListResponse> GetGameItemsAsync(
            Action<GameItemListResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/v1/game-items/list";
            var payload = new Dictionary<string, object>
            {
                ["game_secret"] = ApiKey
            };
            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // ITEM PURCHASE
        // =========================

        /// <summary>
        /// Buys an item with the player's in-game currency.
        /// <paramref name="clientRequestId"/> is the idempotency key: mint it once per tap with
        /// <see cref="NewClientRequestId"/> and reuse the same value on every retry, otherwise a
        /// retry after a timeout is charged as a second purchase.
        /// A 2xx response can still carry a non-success <c>status</c> — check it before granting the item.
        /// </summary>
        public Task<PurchaseItemResponse> PurchaseItemAsync(
            string clientRequestId,
            string playerEmail,
            string playerName,
            string itemId,
            string itemName,
            int quantity,
            decimal unitPrice,
            decimal totalPrice,
            string itemCategory = null,
            string itemDescription = null,
            Action<PurchaseItemResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/item-purchases/purchase-item";
            var payload = new Dictionary<string, object>
            {
                ["client_request_id"] = clientRequestId,
                ["player_email"] = playerEmail,
                ["player_name"] = playerName,
                ["item_id"] = itemId,
                ["item_name"] = itemName,
                ["item_quantity"] = quantity,
                ["unit_price"] = InvoFormat.Amount(unitPrice),
                ["total_price"] = InvoFormat.Amount(totalPrice)
            };

            if (!string.IsNullOrEmpty(itemCategory))
                payload["item_category"] = itemCategory;
            if (!string.IsNullOrEmpty(itemDescription))
                payload["item_description"] = itemDescription;

            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // DESTINATIONS
        // =========================

        /// <summary>Games this game may SEND currency to (phone-addressed, claim-code flow).</summary>
        public Task<AvailableDestinationsResponse> GetSendDestinationsAsync(
            Action<AvailableDestinationsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/available-destinations";
            var payload = new Dictionary<string, object> { ["source_game_id"] = GameId };
            return PostAsync(url, payload, onSuccess, onError);
        }

        /// <summary>Games this game may TRANSFER currency to. Distinct route and rules from sends.</summary>
        public Task<AvailableDestinationsResponse> GetTransferDestinationsAsync(
            Action<AvailableDestinationsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/transfers/available-destinations";
            var payload = new Dictionary<string, object> { ["source_game_id"] = GameId };
            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // CURRENCY SENDS
        // =========================

        /// <summary>
        /// Starts a currency send. Reserves the funds and sends the sender an SMS PIN.
        /// <paramref name="clientRequestId"/> is the idempotency key — reuse it across retries.
        /// Phone numbers must already be E.164 (<see cref="InvoPhone.Normalize"/>); the same
        /// normalised value has to be used again at claim time.
        /// <paramref name="receiverEmail"/> is optional and is omitted from the body when blank.
        /// The response <c>status</c> may be <see cref="InvoStatus.PendingGuardianApproval"/> on a 2xx.
        /// </summary>
        public Task<InitiateSendResponse> InitiateSendAsync(
            string clientRequestId,
            string senderName,
            string senderEmail,
            string senderPhone,
            string receiverPhone,
            string receiverEmail,
            string receivingGameId,
            string amount,
            Action<InitiateSendResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/initiate-send";
            var payload = new Dictionary<string, object>
            {
                ["client_request_id"] = clientRequestId,
                ["sender_player_name"] = senderName,
                ["sender_player_email"] = senderEmail,
                ["sender_player_phone"] = senderPhone,
                ["receiver_player_phone"] = receiverPhone,
                ["receiving_game_id"] = receivingGameId,
                ["amount"] = NormalizeAmount(amount)
            };

            if (!string.IsNullOrEmpty(receiverEmail))
                payload["receiver_player_email"] = receiverEmail;

            return PostAsync(url, payload, onSuccess, onError);
        }

        /// <summary>Confirms a SEND with the SMS PIN. Returns the claim code on success.</summary>
        public Task<VerifySmsResponse> VerifySendSmsAsync(
            string transactionId,
            string smsPin,
            Action<VerifySmsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/verify-sms";
            var payload = new Dictionary<string, object>
            {
                ["transaction_id"] = transactionId,
                ["sms_pin"] = smsPin
            };
            return PostAsync(url, payload, onSuccess, onError);
        }

        /// <summary>
        /// Re-sends the SEND verification PIN. A 2xx with <see cref="InvoStatus.Resent"/> means sent;
        /// the same route also answers 2xx with a cooldown, so read <c>retry_after</c> before offering
        /// the button again.
        /// </summary>
        public Task<ResendPinResponse> ResendSendPinAsync(
            string transactionId,
            Action<ResendPinResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/resend-pin";
            var payload = new Dictionary<string, object> { ["transaction_id"] = transactionId };
            return PostAsync(url, payload, onSuccess, onError);
        }

        /// <summary>Polls the state of a send (verification, guardian approval, claim).</summary>
        public Task<TransactionStatusResponse> GetSendStatusAsync(
            string transactionId,
            Action<TransactionStatusResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/{UnityWebRequest.EscapeURL(transactionId)}/status";
            return GetAsync(url, onSuccess, onError);
        }

        /// <summary>
        /// Redeems a send claim code into this game.
        /// A 2xx with <see cref="InvoStatus.NeedsAccountSelection"/> means the phone matches several
        /// accounts — present <c>candidates</c> and call again with the chosen
        /// <paramref name="receiverPlayerId"/>.
        /// </summary>
        public Task<ClaimCurrencyResponse> ClaimCurrencyAsync(
            string claimCode,
            string receiverName,
            string receiverEmail,
            string receiverPhone,
            int? receiverPlayerId = null,
            Action<ClaimCurrencyResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/claim-currency";
            var payload = new Dictionary<string, object>
            {
                ["claim_code"] = claimCode,
                ["receiver_player_name"] = receiverName,
                ["receiver_player_email"] = receiverEmail,
                ["receiver_player_phone"] = receiverPhone
            };

            if (receiverPlayerId.HasValue)
                payload["receiver_player_id"] = receiverPlayerId.Value;

            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // TRANSFERS
        // =========================

        /// <summary>
        /// Starts a transfer. Same shape as a send but a separate backend flow — the two must never
        /// share endpoints. <paramref name="clientRequestId"/> is the idempotency key; reuse it across retries.
        /// <paramref name="targetEmail"/> is optional and is omitted from the body when blank.
        /// </summary>
        public Task<InitiateTransferResponse> InitiateTransferAsync(
            string clientRequestId,
            string sourceName,
            string sourceEmail,
            string sourcePhone,
            string targetPhone,
            string targetEmail,
            string targetGameId,
            string amount,
            Action<InitiateTransferResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/transfers/initiate-transfer";
            var payload = new Dictionary<string, object>
            {
                ["client_request_id"] = clientRequestId,
                ["source_player_name"] = sourceName,
                ["source_player_email"] = sourceEmail,
                ["source_player_phone"] = sourcePhone,
                ["target_player_phone"] = targetPhone,
                ["target_game_id"] = targetGameId,
                ["amount"] = NormalizeAmount(amount)
            };

            if (!string.IsNullOrEmpty(targetEmail))
                payload["target_player_email"] = targetEmail;

            return PostAsync(url, payload, onSuccess, onError);
        }

        /// <summary>
        /// Confirms a TRANSFER with the SMS PIN. Distinct from <see cref="VerifySendSmsAsync"/>:
        /// a transfer verified against the sends route always 404s.
        /// </summary>
        public Task<VerifySmsResponse> VerifyTransferSmsAsync(
            string transactionId,
            string smsPin,
            Action<VerifySmsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/transfers/verify-sms";
            var payload = new Dictionary<string, object>
            {
                ["transaction_id"] = transactionId,
                ["sms_pin"] = smsPin
            };
            return PostAsync(url, payload, onSuccess, onError);
        }

        /// <summary>Re-sends the TRANSFER verification PIN. Honour <c>retry_after</c> in the response.</summary>
        public Task<ResendPinResponse> ResendTransferPinAsync(
            string transactionId,
            Action<ResendPinResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/transfers/resend-pin";
            var payload = new Dictionary<string, object> { ["transaction_id"] = transactionId };
            return PostAsync(url, payload, onSuccess, onError);
        }

        /// <summary>Polls the state of a transfer (verification, guardian approval, claim).</summary>
        public Task<TransactionStatusResponse> GetTransferStatusAsync(
            string transactionId,
            Action<TransactionStatusResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/transfers/{UnityWebRequest.EscapeURL(transactionId)}/status";
            return GetAsync(url, onSuccess, onError);
        }

        /// <summary>
        /// Redeems a transfer claim code. All five arguments are required by the backend.
        /// <para>
        /// IMPORTANT: this route authenticates with the RECEIVING game's secret key. This plugin
        /// instance holds one key — the key of the game it is installed in — so a claim only
        /// succeeds when the currency is being claimed into THIS game. A cross-game claim needs
        /// the receiving game's own key and will 404 here.
        /// </para>
        /// A 2xx with <see cref="InvoStatus.NeedsAccountSelection"/> means the phone matches several
        /// accounts; present <c>candidates</c> and let the player choose.
        /// </summary>
        public Task<ClaimTransferResponse> ClaimTransferAsync(
            string claimCode,
            string targetPlayerName,
            string targetPlayerEmail,
            string targetPlayerPhone,
            string targetCurrencyId,
            Action<ClaimTransferResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string url = $"{ApiBase}/transfers/claim-transfer";
            var payload = new Dictionary<string, object>
            {
                ["claim_code"] = claimCode,
                ["target_player_name"] = targetPlayerName,
                ["target_player_email"] = targetPlayerEmail,
                ["target_player_phone"] = targetPlayerPhone,
                ["target_currency_id"] = targetCurrencyId
            };
            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // HTTP
        // =========================

        /// <summary>
        /// The only credentials these routes accept. There is no bearer token and no CSRF token
        /// on the game API — adding either does nothing.
        /// </summary>
        private void AddAuthHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("X-Game-Secret-Key", ApiKey);
            request.SetRequestHeader("Content-Type", "application/json");
        }

        private async Task<T> GetAsync<T>(string url, Action<T> onSuccess = null, Action<InvoApiException> onError = null)
        {
            using var req = UnityWebRequest.Get(url);
            req.timeout = requestTimeoutSeconds;
            AddAuthHeaders(req);

            await req.SendWebRequest();

            return HandleResponse("GET", url, req, onSuccess, onError);
        }

        private async Task<T> PostAsync<T>(string url, object payload, Action<T> onSuccess = null, Action<InvoApiException> onError = null)
        {
            string json = JsonConvert.SerializeObject(payload);
            byte[] body = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = requestTimeoutSeconds;
            AddAuthHeaders(req);

            await req.SendWebRequest();

            return HandleResponse("POST", url, req, onSuccess, onError);
        }

        /// <summary>
        /// Success is decided by the HTTP status code only. Any 2xx body is deserialized and
        /// returned even when its <c>status</c> field is not "success" — pending approval,
        /// account selection and cooldown replies are all 2xx and are the caller's to branch on.
        /// </summary>
        private static T HandleResponse<T>(string method, string url, UnityWebRequest req,
                                           Action<T> onSuccess, Action<InvoApiException> onError)
        {
            long status = req.responseCode;
            string body = req.downloadHandler != null ? req.downloadHandler.text : null;

            if (status >= 200 && status <= 299)
            {
                T parsed;
                try
                {
                    if (string.IsNullOrWhiteSpace(body))
                        throw new JsonSerializationException("Empty response body.");

                    parsed = JsonConvert.DeserializeObject<T>(body);
                }
                catch (JsonException)
                {
                    var parseError = new InvoApiException(
                        $"{method} {Redact(url)} returned a body that could not be read as {typeof(T).Name}.",
                        status, "SDK_RESPONSE_PARSE_ERROR", null, null, body);
                    onError?.Invoke(parseError);
                    throw parseError;
                }

                onSuccess?.Invoke(parsed);
                return parsed;
            }

            var error = BuildException(method, url, req, status, body);
            onError?.Invoke(error);
            throw error;
        }

        /// <summary>
        /// Turns a failed request into an <see cref="InvoApiException"/> carrying the backend
        /// envelope { error, error_code, error_id } and any retry hint. The raw body is kept on
        /// the exception but never folded into the message, which is what gets logged.
        /// </summary>
        private static InvoApiException BuildException(string method, string url, UnityWebRequest req,
                                                       long status, string body)
        {
            string message = null;
            string errorCode = null;
            string errorId = null;
            int? retryAfter = null;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var envelope = JObject.Parse(body);
                    message = (string)envelope["error"] ?? (string)envelope["message"];
                    errorCode = (string)envelope["error_code"];
                    errorId = (string)envelope["error_id"];
                    retryAfter = ReadSeconds(envelope["retry_after"]) ?? ReadSeconds(envelope["retry_after_seconds"]);
                }
                catch (JsonException)
                {
                    // Not JSON (proxy error page, truncated response). Body is preserved on the exception.
                }
            }

            if (!retryAfter.HasValue)
                retryAfter = ParseSeconds(req.GetResponseHeader("Retry-After"));

            if (string.IsNullOrEmpty(message))
            {
                message = status == 0
                    ? $"{method} {Redact(url)} did not reach the server ({req.error})."
                    : $"{method} {Redact(url)} failed with HTTP {status}.";
            }

            return new InvoApiException(message, status, errorCode, errorId, retryAfter, body);
        }

        private static int? ReadSeconds(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            return ParseSeconds(token.ToString());
        }

        private static int? ParseSeconds(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                return null;
            if (seconds <= 0)
                return 0;
            return (int)Math.Ceiling(seconds);
        }

        /// <summary>Keeps the player's email out of log lines built from a URL.</summary>
        private static string Redact(string url)
        {
            const string marker = "/by-email/";
            int index = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return index < 0 ? url : url.Substring(0, index + marker.Length) + "***";
        }

        /// <summary>
        /// Re-formats a caller-supplied amount string to invariant wire format, so a value typed
        /// on a locale that uses a comma separator is not rejected by the server's decimal parser.
        /// Unparseable input is passed through untouched and left for the backend to reject.
        /// </summary>
        private static string NormalizeAmount(string amount)
        {
            return InvoFormat.TryParseAmount(amount, out decimal value) ? InvoFormat.Amount(value) : amount;
        }

        // =========================
        // UTILS
        // =========================
        public static async Task<Sprite> LoadSpriteAsync(string url, Image target = null)
        {
            if (string.IsNullOrEmpty(url)) return null;
            using var req = UnityWebRequestTexture.GetTexture(url);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[InvoSDK] Sprite load failed: {req.error}");
                return null;
            }

            var tex = DownloadHandlerTexture.GetContent(req);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            if (target != null)
                target.sprite = sprite;
            return sprite;
        }
    }
}
