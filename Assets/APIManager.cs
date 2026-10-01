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
        public const string Approved = "approved";
        public const string Completed = "completed";
        public const string NotPending = "not_pending";
    }

    /// <summary>
    /// Transport layer for the Invo API.
    ///
    /// Two credentials, never mixed:
    /// <list type="bullet">
    /// <item><b>Game secret</b> (<c>X-Game-Secret-Key</c>) — initiate send/transfer, item purchase,
    /// catalog, balance by email, claims, minting player tokens. It belongs on YOUR server. In
    /// production these calls go to <see cref="InvoSDKConfig.gameServerUrl"/>, which forwards them
    /// to Invo with the secret attached. Only a sandbox build may call Invo directly with the
    /// <c>sdkKey</c> from the config.</item>
    /// <item><b>Player token</b> (<c>Authorization: Bearer</c>) — the QR device approval, the
    /// approve / confirm-receipt calls that move the money, the pending list. A 15-minute token
    /// scoped to one player; safe on the client. Minted on demand and re-minted on expiry.</item>
    /// </list>
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        public static APIManager Instance;

        // The API roots are fixed per environment — they are not inspector-tunable, because
        // a mistyped host silently sends the game secret to somewhere that is not Invo.
        private const string ProductionApiBase = "https://invo.network/api";
        private const string SandboxApiBase = "https://sandbox.invo.network/sandbox/api";

        /// <summary>Error code on the exception thrown when a game-secret call has no legal route.</summary>
        public const string GameServerRequiredCode = "SDK_GAME_SERVER_REQUIRED";

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

        // The active player. Seeded from the config; games with real accounts call SetActivePlayer.
        private string activePlayerEmail;
        private string activePlayerName;
        private string activePlayerPhone;

        // Player-token cache. One token per active player; dropped on SetActivePlayer.
        private string playerToken;
        private DateTime playerTokenExpiresUtc;
        private Task<string> playerTokenMint;
        private int playerTokenGeneration;

        /// <summary>
        /// Optional. Attaches YOUR session credential (cookie, bearer, signed header) to every
        /// request sent to <see cref="InvoSDKConfig.gameServerUrl"/>, so your server can tell which
        /// logged-in player is asking before it attaches the game secret. Never put the Invo secret here.
        /// </summary>
        public static Action<UnityWebRequest> GameServerRequestDecorator { get; set; }

        /// <summary>
        /// Optional. Replaces the built-in player-token mint. Return a fresh token from your own
        /// login API (which called <c>POST /api/sdk/player-token</c> server-side). Called again
        /// whenever Invo answers 401 with an <c>SDK_TOKEN_*</c> code.
        /// </summary>
        public static Func<Task<PlayerTokenResponse>> PlayerTokenProvider { get; set; }

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
            activePlayerEmail = config.playerEmail;
            activePlayerName = config.playerName;
            activePlayerPhone = config.playerPhone;
            nextPollDelay = balancePollInterval;

            if (useProduction && !HasGameServer)
                Debug.LogError("[InvoSDK] Production is enabled but InvoSDKConfig.gameServerUrl is empty. " +
                               "Calls that need the game secret will be refused. See the README, 'Server-side proxy'.");
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
        public bool UseProduction => useProduction;

        /// <summary>True when game-secret calls are routed through your server.</summary>
        public bool HasGameServer => config != null && !string.IsNullOrWhiteSpace(config.gameServerUrl);

        public string GetPlayerEmail() => activePlayerEmail ?? string.Empty;
        public string GetPlayerName() => activePlayerName ?? string.Empty;
        public string GetPlayerPhone() => activePlayerPhone ?? string.Empty;
        public string GetGameName() => config != null ? config.gameName : string.Empty;
        public string GetGameCurrency() => config != null ? config.gameCurrencyName : string.Empty;

        /// <summary>
        /// Switches the player the SDK acts for. Drops the previous player's token, so nothing
        /// signed for one identity is ever presented for another. Call it on login and on account switch.
        /// </summary>
        public void SetActivePlayer(string email, string name, string phone = null)
        {
            bool changed = !string.Equals(email, activePlayerEmail, StringComparison.OrdinalIgnoreCase);
            activePlayerEmail = email;
            activePlayerName = name;
            activePlayerPhone = phone;
            if (changed)
            {
                ClearPlayerToken();
                warnedAboutMissingEmail = false;
                StartBalancePolling();
            }
        }

        /// <summary>Forgets the cached player token. The next token-authed call mints a fresh one.</summary>
        public void ClearPlayerToken()
        {
            playerToken = null;
            playerTokenExpiresUtc = DateTime.MinValue;
            playerTokenMint = null;
            playerTokenGeneration++;
        }

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
                    Debug.LogWarning("[InvoSDK] No active player email — balance polling is idle.");
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

        /// <summary>Reads the player's per-currency balances for this game. Game-secret route.</summary>
        public Task<PlayerBalanceResponse> GetPlayerBalanceAsync(
            string email,
            Action<PlayerBalanceResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string path = $"/player-balances/player/by-email/{UnityWebRequest.EscapeURL(email)}";
            return SendAsync("GET", path, null, Auth.GameSecret, onSuccess, onError);
        }

        // =========================
        // GAME ITEMS
        // =========================

        /// <summary>
        /// Lists storefront items. Note this returns every active item across ALL games on the
        /// network, not just yours — the backend returns the same response regardless of which
        /// game is asking. Filter on <c>GameItem.game_id</c> if you only want your own.
        /// This route reads the secret from the BODY (<c>game_secret</c>), not the header. Through
        /// your game server the field is left out and your server adds it.
        /// </summary>
        public Task<GameItemListResponse> GetGameItemsAsync(
            Action<GameItemListResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            var payload = new Dictionary<string, object>();
            if (!HasGameServer)
                payload["game_secret"] = ApiKey;
            return SendAsync("POST", "/v1/game-items/list", payload, Auth.GameSecret, onSuccess, onError);
        }

        // =========================
        // ITEM PURCHASE
        // =========================

        /// <summary>
        /// Buys an item with the player's in-game currency. Game-secret route: in production your
        /// server must look the price up itself, because Invo charges whatever price it is sent.
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

            return SendAsync("POST", "/item-purchases/purchase-item", payload, Auth.GameSecret, onSuccess, onError);
        }

        // =========================
        // DESTINATIONS
        // =========================

        /// <summary>Games this game may SEND currency to.</summary>
        public Task<AvailableDestinationsResponse> GetSendDestinationsAsync(
            Action<AvailableDestinationsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            var payload = new Dictionary<string, object> { ["source_game_id"] = GameId };
            return SendAsync("POST", "/currency-sends/available-destinations", payload, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>Games this game may TRANSFER currency to. Distinct route and rules from sends.</summary>
        public Task<AvailableDestinationsResponse> GetTransferDestinationsAsync(
            Action<AvailableDestinationsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            var payload = new Dictionary<string, object> { ["source_game_id"] = GameId };
            return SendAsync("POST", "/transfers/available-destinations", payload, Auth.GameSecret, onSuccess, onError);
        }

        // =========================
        // CURRENCY SENDS
        // =========================

        /// <summary>
        /// Starts a currency send and reserves the funds. Nothing moves until the sender approves:
        /// run <see cref="InvoDeviceApproval.RunAsync"/> with <see cref="InvoApprovalFlow.Send"/> and
        /// the returned <c>transaction_id</c>. Phones are still required — they address the send
        /// and identify both players — but no code is typed from a text message.
        /// <paramref name="clientRequestId"/> is the idempotency key — reuse it across retries.
        /// Phone numbers must already be E.164 (<see cref="InvoPhone.Normalize"/>).
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

            return SendAsync("POST", "/currency-sends/initiate-send", payload, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>Legacy: confirms a SEND with a texted PIN.</summary>
        [Obsolete("SMS verification is retired. Approve with InvoDeviceApproval.RunAsync(transactionId, InvoApprovalFlow.Send).")]
        public Task<VerifySmsResponse> VerifySendSmsAsync(
            string transactionId,
            string smsPin,
            Action<VerifySmsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            var payload = new Dictionary<string, object>
            {
                ["transaction_id"] = transactionId,
                ["sms_pin"] = smsPin
            };
            return SendAsync("POST", "/currency-sends/verify-sms", payload, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>Legacy: re-sends a SEND's texted PIN. Sends nothing for an in-app (QR) transaction.</summary>
        [Obsolete("SMS verification is retired. Approve with InvoDeviceApproval.RunAsync(transactionId, InvoApprovalFlow.Send).")]
        public Task<ResendPinResponse> ResendSendPinAsync(
            string transactionId,
            Action<ResendPinResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            var payload = new Dictionary<string, object> { ["transaction_id"] = transactionId };
            return SendAsync("POST", "/currency-sends/resend-pin", payload, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>Reads the state of a send. <c>verification_state</c>: awaiting | approved | completed | expired | failed.</summary>
        public Task<TransactionStatusResponse> GetSendStatusAsync(
            string transactionId,
            Action<TransactionStatusResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string path = $"/currency-sends/{UnityWebRequest.EscapeURL(transactionId)}/status";
            return SendAsync("GET", path, null, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>
        /// Fallback collect: redeems a send claim code into this game. Prefer the QR collect —
        /// <see cref="InvoDeviceApproval.RunAsync"/> with <see cref="InvoApprovalFlow.SendReceipt"/> —
        /// and use this only when that answers <c>receiver_not_enrolled_use_claim_code</c>.
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
            var payload = new Dictionary<string, object>
            {
                ["claim_code"] = claimCode,
                ["receiver_player_name"] = receiverName,
                ["receiver_player_email"] = receiverEmail,
                ["receiver_player_phone"] = receiverPhone
            };

            if (receiverPlayerId.HasValue)
                payload["receiver_player_id"] = receiverPlayerId.Value;

            return SendAsync("POST", "/currency-sends/claim-currency", payload, Auth.GameSecret, onSuccess, onError);
        }

        // =========================
        // TRANSFERS
        // =========================

        /// <summary>
        /// Starts a transfer and reserves the funds. Same shape as a send but a separate backend
        /// flow — the two must never share endpoints. Approve with
        /// <see cref="InvoDeviceApproval.RunAsync"/> and <see cref="InvoApprovalFlow.Transfer"/>.
        /// <paramref name="clientRequestId"/> is the idempotency key; reuse it across retries.
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

            return SendAsync("POST", "/transfers/initiate-transfer", payload, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>Legacy: confirms a TRANSFER with a texted PIN.</summary>
        [Obsolete("SMS verification is retired. Approve with InvoDeviceApproval.RunAsync(transactionId, InvoApprovalFlow.Transfer).")]
        public Task<VerifySmsResponse> VerifyTransferSmsAsync(
            string transactionId,
            string smsPin,
            Action<VerifySmsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            var payload = new Dictionary<string, object>
            {
                ["transaction_id"] = transactionId,
                ["sms_pin"] = smsPin
            };
            return SendAsync("POST", "/transfers/verify-sms", payload, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>Legacy: re-sends a TRANSFER's texted PIN. Sends nothing for an in-app (QR) transaction.</summary>
        [Obsolete("SMS verification is retired. Approve with InvoDeviceApproval.RunAsync(transactionId, InvoApprovalFlow.Transfer).")]
        public Task<ResendPinResponse> ResendTransferPinAsync(
            string transactionId,
            Action<ResendPinResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            var payload = new Dictionary<string, object> { ["transaction_id"] = transactionId };
            return SendAsync("POST", "/transfers/resend-pin", payload, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>Reads the state of a transfer. <c>claim_code</c> is filled for the source game once approved.</summary>
        public Task<TransactionStatusResponse> GetTransferStatusAsync(
            string transactionId,
            Action<TransactionStatusResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            string path = $"/transfers/{UnityWebRequest.EscapeURL(transactionId)}/status";
            return SendAsync("GET", path, null, Auth.GameSecret, onSuccess, onError);
        }

        /// <summary>
        /// Fallback collect: redeems a transfer claim code. Prefer the QR collect with
        /// <see cref="InvoApprovalFlow.TransferReceipt"/>. All five arguments are required.
        /// <para>
        /// This route authenticates with the RECEIVING game's secret, so it only succeeds when the
        /// currency is being claimed into THIS game.
        /// </para>
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
            var payload = new Dictionary<string, object>
            {
                ["claim_code"] = claimCode,
                ["target_player_name"] = targetPlayerName,
                ["target_player_email"] = targetPlayerEmail,
                ["target_player_phone"] = targetPlayerPhone,
                ["target_currency_id"] = targetCurrencyId
            };
            return SendAsync("POST", "/transfers/claim-transfer", payload, Auth.GameSecret, onSuccess, onError);
        }

        // =========================
        // PLAYER TOKEN
        // =========================

        /// <summary>
        /// Returns a valid player token for the active player, minting one when the cache is empty
        /// or within a minute of expiry. Concurrent callers share one mint.
        /// The player must already exist in this game (Invo answers 404 <c>player_not_found</c>
        /// otherwise); initiating a send or transfer creates the sender.
        /// </summary>
        public Task<string> GetPlayerTokenAsync()
        {
            if (!string.IsNullOrEmpty(playerToken) && DateTime.UtcNow < playerTokenExpiresUtc)
                return Task.FromResult(playerToken);

            Task<string> mint = playerTokenMint;
            if (mint == null || mint.IsCompleted)
            {
                mint = MintPlayerTokenAsync(playerTokenGeneration);
                // A mint that finished synchronously (a failure before its first await) has
                // already run its cleanup; caching it would replay that failure forever.
                playerTokenMint = mint.IsCompleted ? null : mint;
            }
            return mint;
        }

        private async Task<string> MintPlayerTokenAsync(int generation)
        {
            try
            {
                PlayerTokenResponse resp;
                if (PlayerTokenProvider != null)
                {
                    resp = await PlayerTokenProvider();
                }
                else
                {
                    string email = GetPlayerEmail();
                    if (string.IsNullOrEmpty(email))
                        throw new InvoApiException("No active player: call APIManager.SetActivePlayer first.",
                                                   0, "SDK_NO_ACTIVE_PLAYER", null, null, null);
                    var payload = new Dictionary<string, object> { ["player_email"] = email };
                    resp = await SendAsync<PlayerTokenResponse>("POST", "/sdk/player-token", payload, Auth.GameSecret, null, null);
                }

                if (resp == null || string.IsNullOrEmpty(resp.token))
                    throw new InvoApiException("The player-token response carried no token.", 0,
                                               "SDK_RESPONSE_PARSE_ERROR", null, null, null);

                // A player switch while the mint was in flight makes this token someone else's.
                if (generation != playerTokenGeneration)
                    throw new InvoApiException("The active player changed while a token was being minted.", 0,
                                               "SDK_PLAYER_CHANGED", null, null, null);

                playerToken = resp.token;
                playerTokenExpiresUtc = ParseExpiry(resp.expires_at);
                return playerToken;
            }
            finally
            {
                if (generation == playerTokenGeneration)
                    playerTokenMint = null;
            }
        }

        private static DateTime ParseExpiry(string iso)
        {
            // Refresh a minute early; a token that dies mid-request costs a 401 round trip.
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                                  DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime t))
                return t.AddSeconds(-60);
            return DateTime.UtcNow.AddMinutes(14);
        }

        // =========================
        // DEVICE APPROVAL (RFC 8628) — the QR flow. Player-token routes.
        // =========================

        /// <summary>
        /// Starts a device approval for ONE transaction. Render <c>verification_uri_complete</c>
        /// as a QR (channel <c>qr</c>) or open it in the system browser (<c>app_browser</c>).
        /// A repeat begin while a grant is pending replaces it with a fresh code. A grant the player
        /// already approved answers <c>409 DEVICE_APPROVAL_ALREADY_PENDING</c> — settle it instead.
        /// Most callers want <see cref="InvoDeviceApproval.RunAsync"/>, which runs every stage.
        /// </summary>
        public Task<DeviceApprovalGrant> BeginDeviceApprovalAsync(string transactionId, string flow, string channel)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
                throw new ArgumentException("transactionId is required.", nameof(transactionId));
            if (!InvoApprovalFlow.IsValid(flow))
                throw new ArgumentException("Unknown approval flow: " + flow, nameof(flow));
            if (channel != InvoApprovalChannel.Qr && channel != InvoApprovalChannel.AppBrowser)
                throw new ArgumentException("channel must be qr or app_browser.", nameof(channel));

            var payload = new Dictionary<string, object>
            {
                ["transaction_id"] = transactionId,
                ["flow"] = flow,
                ["channel"] = channel
            };
            return SendAsync<DeviceApprovalGrant>("POST", "/sdk/approvals/device/begin", payload, Auth.PlayerToken, null, null);
        }

        /// <summary>
        /// One poll. Never faster than the grant's <c>interval</c>. The RFC 8628 outcomes
        /// (<c>authorization_pending</c>, <c>slow_down</c>, <c>access_denied</c>, <c>expired_token</c>)
        /// come back as a result, not an exception. <c>invalid_grant</c> and transport failures throw.
        /// An <c>approved</c> poll moves NOTHING: call <see cref="SettleDeviceApprovalAsync"/> next.
        /// </summary>
        public async Task<DeviceApprovalPollResponse> PollDeviceApprovalAsync(string deviceCode)
        {
            var payload = new Dictionary<string, object> { ["device_code"] = deviceCode };
            try
            {
                var resp = await SendAsync<DeviceApprovalPollResponse>("POST", "/sdk/approvals/device/poll", payload, Auth.PlayerToken, null, null);
                if (resp != null && string.IsNullOrEmpty(resp.status))
                    resp.status = InvoDevicePollStatus.Approved;
                return resp;
            }
            catch (InvoApiException ex) when (ex.StatusCode == 400 && IsPollOutcome(ex.ErrorCode))
            {
                DeviceApprovalPollResponse outcome = null;
                try { outcome = JsonConvert.DeserializeObject<DeviceApprovalPollResponse>(ex.Body ?? "{}"); }
                catch (JsonException) { }
                outcome = outcome ?? new DeviceApprovalPollResponse();
                outcome.status = ex.ErrorCode;
                return outcome;
            }
        }

        private static bool IsPollOutcome(string code)
        {
            return code == InvoDevicePollStatus.AuthorizationPending || code == InvoDevicePollStatus.SlowDown ||
                   code == InvoDevicePollStatus.AccessDenied || code == InvoDevicePollStatus.ExpiredToken;
        }

        /// <summary>
        /// Answers the on-screen "set up INVO on this phone? code ####" prompt for a first-time phone.
        /// <paramref name="approve"/> false ends the whole grant. Not retried.
        /// </summary>
        public Task<ConfirmEnrollmentResponse> ConfirmDeviceEnrollmentAsync(string deviceCode, bool approve)
        {
            var payload = new Dictionary<string, object>
            {
                ["device_code"] = deviceCode,
                ["decision"] = approve ? "approve" : "deny"
            };
            return SendAsync<ConfirmEnrollmentResponse>("POST", "/sdk/approvals/device/confirm-enrollment", payload, Auth.PlayerToken, null, null);
        }

        /// <summary>
        /// THE MONEY STEP. After a poll answered <c>approved</c>, settles the transaction with the
        /// grant's <c>device_code</c>, routed by flow:
        /// transfer → <c>/sdk/transfers/{id}/approve</c>, send → <c>/sdk/send/{id}/approve</c>,
        /// send_receipt → <c>/sdk/send/{id}/confirm-receipt</c>,
        /// transfer_receipt → <c>/sdk/transfers/{id}/confirm-receipt</c>.
        /// <para>
        /// <c>TRANSACTION_NOT_PENDING</c> is returned as <see cref="InvoStatus.NotPending"/> with
        /// <c>already_settled</c> — it is the normal answer to "did my earlier attempt land?".
        /// Never auto-retried: the device code is single use. A thrown failure is AMBIGUOUS about the
        /// money (the backend may have committed before failing), so read the transaction status
        /// before telling the player it failed, and never start a replacement transaction.
        /// </para>
        /// </summary>
        public async Task<DeviceApprovalSettleResponse> SettleDeviceApprovalAsync(string transactionId, string flow, string deviceCode)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
                throw new ArgumentException("transactionId is required.", nameof(transactionId));
            if (!InvoApprovalFlow.IsValid(flow))
                throw new ArgumentException("Unknown approval flow: " + flow, nameof(flow));

            string id = UnityWebRequest.EscapeURL(transactionId);
            string path;
            switch (flow)
            {
                case InvoApprovalFlow.Transfer: path = $"/sdk/transfers/{id}/approve"; break;
                case InvoApprovalFlow.Send: path = $"/sdk/send/{id}/approve"; break;
                case InvoApprovalFlow.SendReceipt: path = $"/sdk/send/{id}/confirm-receipt"; break;
                default: path = $"/sdk/transfers/{id}/confirm-receipt"; break;
            }

            var payload = new Dictionary<string, object> { ["device_code"] = deviceCode };
            try
            {
                return await SendAsync<DeviceApprovalSettleResponse>("POST", path, payload, Auth.PlayerToken, null, null);
            }
            catch (InvoApiException ex) when (ex.ErrorCode == "TRANSACTION_NOT_PENDING")
            {
                string current = null;
                try { current = Str(JObject.Parse(ex.Body ?? "{}")["current_status"]); }
                catch (JsonException) { }
                return new DeviceApprovalSettleResponse
                {
                    status = InvoStatus.NotPending,
                    transaction_id = transactionId,
                    current_status = current,
                    already_settled = InvoDeviceApprovalCore.IsPastStep(flow, current)
                };
            }
        }

        /// <summary>
        /// Everything awaiting the active player: transactions they started and must approve
        /// (<c>identity_gate</c>) and sends addressed to them that they can collect
        /// (<c>receiving_confirm</c>). Poll on open and every 30–60 s while the panel is visible.
        /// </summary>
        public Task<PendingActionsResponse> GetPendingActionsAsync(
            Action<PendingActionsResponse> onSuccess = null,
            Action<InvoApiException> onError = null)
        {
            return SendAsync("GET", "/sdk/transfers/pending", null, Auth.PlayerToken, onSuccess, onError);
        }

        // =========================
        // HTTP
        // =========================

        private enum Auth
        {
            /// <summary>X-Game-Secret-Key: your server in production, the sandbox key otherwise.</summary>
            GameSecret,
            /// <summary>Authorization: Bearer player token, straight to Invo.</summary>
            PlayerToken
        }

        private Task<T> SendAsync<T>(string method, string path, object payload, Auth auth,
                                     Action<T> onSuccess, Action<InvoApiException> onError)
        {
            return auth == Auth.PlayerToken
                ? SendWithPlayerTokenAsync(method, path, payload, onSuccess, onError)
                : SendWithGameSecretAsync(method, path, payload, onSuccess, onError);
        }

        private async Task<T> SendWithGameSecretAsync<T>(string method, string path, object payload,
                                                         Action<T> onSuccess, Action<InvoApiException> onError)
        {
            string url;
            string secret = null;
            if (HasGameServer)
            {
                // Your server receives Invo's own path under its base URL, checks the player's
                // session, attaches X-Game-Secret-Key and forwards. See the README.
                url = config.gameServerUrl.TrimEnd('/') + "/api" + path;
            }
            else if (!useProduction && !string.IsNullOrEmpty(ApiKey))
            {
                url = ApiBase + path;
                secret = ApiKey;
            }
            else
            {
                var refused = new InvoApiException(
                    useProduction
                        ? "This call needs the game secret, which must never ship in a production build. " +
                          "Set InvoSDKConfig.gameServerUrl to your server (README, 'Server-side proxy')."
                        : "No route for a game-secret call: set InvoSDKConfig.sdkKey (sandbox) or gameServerUrl.",
                    0, GameServerRequiredCode, null, null, null);
                onError?.Invoke(refused);
                throw refused;
            }

            using var req = BuildRequest(method, url, payload);
            if (secret != null)
                req.SetRequestHeader("X-Game-Secret-Key", secret);
            else
                GameServerRequestDecorator?.Invoke(req);

            await req.SendWebRequest();
            return HandleResponse(method, url, req, onSuccess, onError);
        }

        private async Task<T> SendWithPlayerTokenAsync<T>(string method, string path, object payload,
                                                          Action<T> onSuccess, Action<InvoApiException> onError)
        {
            string url = ApiBase + path;
            for (int attempt = 0; ; attempt++)
            {
                string token;
                try
                {
                    token = await GetPlayerTokenAsync();
                }
                catch (InvoApiException ex)
                {
                    onError?.Invoke(ex);
                    throw;
                }

                using var req = BuildRequest(method, url, payload);
                req.SetRequestHeader("Authorization", "Bearer " + token);
                await req.SendWebRequest();

                // A token 401 is rejected before the route runs, so one re-mint and retry is safe
                // even on the money calls. Never re-prompt the player for it.
                if (attempt == 0 && req.responseCode == 401 && IsTokenRejection(req))
                {
                    if (playerToken == token)
                        ClearPlayerToken();
                    continue;
                }
                return HandleResponse(method, url, req, onSuccess, onError);
            }
        }

        private static bool IsTokenRejection(UnityWebRequest req)
        {
            string body = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (string.IsNullOrWhiteSpace(body))
                return true;
            try
            {
                string code = Str(JObject.Parse(body)["code"]);
                return code == null || code.StartsWith("SDK_TOKEN_", StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                return true;
            }
        }

        private UnityWebRequest BuildRequest(string method, string url, object payload)
        {
            UnityWebRequest req;
            if (method == "GET")
            {
                req = UnityWebRequest.Get(url);
            }
            else
            {
                string json = JsonConvert.SerializeObject(payload ?? new Dictionary<string, object>());
                req = new UnityWebRequest(url, method)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
            }
            req.timeout = requestTimeoutSeconds;
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }

        /// <summary>
        /// Success is decided by the HTTP status code only. Any 2xx body is deserialized and
        /// returned even when its <c>status</c> field is not "success" — pending approval,
        /// account selection, holds and cooldown replies are all 2xx and are the caller's to branch on.
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
        /// Turns a failed request into an <see cref="InvoApiException"/>. The machine code lives
        /// under a different key per route family, so it is read as <c>code ?? error_code ?? error</c>
        /// (the last only when it is a bare token such as <c>authorization_pending</c>, not a sentence).
        /// The raw body is kept on the exception but never folded into the message, which is what gets logged.
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
                    string error = Str(envelope["error"]);
                    message = Str(envelope["message"]) ?? error;
                    errorCode = Str(envelope["code"]) ?? Str(envelope["error_code"]);
                    if (errorCode == null && error != null && IsBareToken(error))
                        errorCode = error;
                    errorId = Str(envelope["error_id"]) ?? Str(envelope["error_ref"]);
                    retryAfter = ReadSeconds(envelope["retry_after_seconds"]) ?? ReadSeconds(envelope["retry_after"]);
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

        /// <summary>The token as a string when it is one; null for absent, null, numbers and objects.</summary>
        private static string Str(JToken token)
        {
            return token != null && token.Type == JTokenType.String ? (string)token : null;
        }

        private static bool IsBareToken(string value)
        {
            if (value.Length == 0 || value.Length > 64)
                return false;
            foreach (char c in value)
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    return false;
            return true;
        }

        private static int? ReadSeconds(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            return ParseSeconds(token.ToString());
        }

        private static int? ParseSeconds(string raw)
        {
            // retry_after is a number of seconds on 429s but an ISO timestamp on the recovery
            // cooldown; a timestamp does not parse here and is left to retry_after_seconds.
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
