using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using static InvoSDK.TransferResponse;

namespace InvoSDK
{
    public class APIManager : MonoBehaviour
    {
        public static APIManager Instance;

        [Header("Configuration")]
        [SerializeField] private string sandboxBase = "https://sandbox.invo.network";
        [SerializeField] private string productionBase = "https://invo.network";

        private string _accessToken;
        private string _csrfToken;
        private bool _isAuthenticating = false;

        public string AccessToken => _accessToken;
        public string CsrfToken => _csrfToken;

        [SerializeField] private bool useProduction = false;
        [SerializeField] private TextMeshProUGUI playerbalance;
        private InvoSDKConfig config;
        [SerializeField] private float balancePollInterval = 10f;
        private float balanceTimer;
        private bool isPollingActive = true;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
                if (config == null)
                {
                    Debug.LogError("[InvoSDK] Config not found! Please run Setup Wizard.");
                    return;
                }

                useProduction = config.useProduction;
                if (!useProduction)
                    _ = InitializeSandboxAuth();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            //if (useProduction || (!string.IsNullOrEmpty(_accessToken) && !string.IsNullOrEmpty(_csrfToken)))
                PollPlayerBalance();
        }

        private void Update()
        {
            if (!isPollingActive)
                return;

            balanceTimer += Time.deltaTime;

            if (balanceTimer >= balancePollInterval)
            {
                balanceTimer = 0f;
                PollPlayerBalance();
            }
        }

        // =========================
        // ENVIRONMENT ROUTES
        // =========================
        public string ApiBase => useProduction ? $"{productionBase}/api" : $"{sandboxBase}/sandbox/api";
        public string AuthBase => useProduction ? $"{productionBase}/auth" : $"{sandboxBase}/sandbox/auth";
        public string ApiKey => config != null ? config.sdkKey : string.Empty;
        public string GameId => config != null ? config.gameId : string.Empty;

        // =========================
        // SANDBOX AUTH (LOGIN + CSRF)
        // =========================
        private async Task InitializeSandboxAuth()
        {
            Debug.Log("[InvoSDK] Sandbox detected – performing login and CSRF setup...");
            string email = config.playerEmail;
            string password = config.playerPassword;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[InvoSDK] Missing sandbox login credentials in config.");
                return;
            }

            await RefreshTokensAsync(email, password);
        }

        private async Task RefreshTokensAsync(string email, string password)
        {
            if (_isAuthenticating)
                return;
            _isAuthenticating = true;

            bool loginOK = await LoginAsync(email, password);
            if (loginOK)
            {
                bool csrfOK = await FetchCsrfTokenAsync();
                if (csrfOK)
                    Debug.Log("[InvoSDK] Login + CSRF initialized successfully.");
            }

            _isAuthenticating = false;
        }

        private async Task<bool> LoginAsync(string email, string password)
        {
            string url = $"{AuthBase}/login";
            var body = JsonConvert.SerializeObject(new { email, password });
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[InvoSDK] Login failed: {req.error}");
                return false;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<LoginResponse>(req.downloadHandler.text);
                _accessToken = data.access_token;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InvoSDK] Login parse error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> FetchCsrfTokenAsync()
        {
            string url = $"{AuthBase}/csrf-token";
            using var req = UnityWebRequest.Get(url);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[InvoSDK] CSRF request failed: {req.error}");
                return false;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<CsrfResponse>(req.downloadHandler.text);
                _csrfToken = data.csrf_token;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InvoSDK] CSRF parse error: {ex.Message}");
                return false;
            }
        }

        [Serializable] private class LoginResponse { public string access_token; }
        [Serializable] private class CsrfResponse { public string csrf_token; }

        // =========================
        // AUTO RETRY WRAPPER
        // =========================
        private async Task<T> SendWithRetryAsync<T>(Func<Task<T>> sendFunc)
        {
            try
            {
                return await sendFunc();
            }
            catch (Exception e)
            {
                if (e.Message.Contains("401") || e.Message.Contains("403"))
                {
                    Debug.LogWarning("[InvoSDK] Token expired – retrying login...");
                    await RefreshTokensAsync(config.playerEmail, config.playerPassword);
                    return await sendFunc();
                }
                throw;
            }
        }

        // =========================
        // PLAYER BALANCE
        // =========================
        private void PollPlayerBalance()
        {
            string email = GetPlayerEmail();
            if (string.IsNullOrEmpty(email))
            {
                Debug.LogWarning("[InvoSDK] Missing player email in config.");
                return;
            }

            _ = SendWithRetryAsync(async () =>
            {
                await GetPlayerBalanceAsync(email,
                    onSuccess: (resp) =>
                    {
                        if (resp != null && resp.balances != null && resp.balances.Count > 0)
                        {
                            string total = resp.balances[0].total_balance;
                            if (playerbalance != null)
                                playerbalance.text = total;
                        }
                        else if (playerbalance != null)
                            playerbalance.text = "-";
                    },
                    onError: (err) =>
                    {
                        Debug.LogError($"[InvoSDK] Balance polling failed: {err}");
                        if (playerbalance != null)
                            playerbalance.text = "-";
                    });
                return true;
            });
        }

        // =========================
        // GAME ITEMS
        // =========================
        public Task<GameItemListResponse> GetGameItemsAsync(Action<GameItemListResponse> onSuccess = null, Action<string> onError = null)
        {
            return SendWithRetryAsync(async () =>
            {
                string url = $"{ApiBase}/v1/game-items/list";
                var payload = new { game_secret = ApiKey };
                var result = await PostAsync<GameItemListResponse>(url, payload);
                onSuccess?.Invoke(result);
                return result;
            });
        }

        // =========================
        // BALANCE
        // =========================
        public Task<PlayerBalanceResponse> GetPlayerBalanceAsync(string email, Action<PlayerBalanceResponse> onSuccess = null, Action<string> onError = null)
        {
            return SendWithRetryAsync(async () =>
            {
                string url = $"{ApiBase}/player-balances/player/by-email/{email}";
                return await GetAsync(url, onSuccess, onError);
            });
        }

        // =========================
        // AVAILABLE DESTINATIONS
        // =========================
        public Task<AvailableDestinationsResponse> GetAvailableDestinationsAsync(Action<AvailableDestinationsResponse> onSuccess = null, Action<string> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/available-destinations";
            var payload = new { source_game_id = GameId };
            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // INITIATE SEND
        // =========================
        public Task<InitiateSendResponse> InitiateSendAsync(
            string senderName, string senderEmail, string senderPhone,
            string receivingGameId, string receiverPhone, string amount,
            Action<InitiateSendResponse> onSuccess = null, Action<string> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/initiate-send";
            var payload = new
            {
                client_request_id = Guid.NewGuid().ToString(),
                sender_player_name = senderName,
                sender_player_email = senderEmail,
                sender_player_phone = senderPhone,
                receiver_player_phone = receiverPhone,
                receiving_game_id = receivingGameId,
                amount = amount
            };
            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // VERIFY SMS - Currency SEND
        // =========================
        public Task<VerifySmsResponse> VerifySmsAsync(string transactionId, string pin, Action<VerifySmsResponse> onSuccess = null, Action<string> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/verify-sms";
            var payload = new { transaction_id = transactionId, sms_pin = pin };
            return PostAsync(url, payload, onSuccess, onError);
        }
        // =========================
        // VERIFY SMS - Transfer
        // =========================
        public Task<VerifySmsResponse> VerifySmsTransferAsync(string transactionId, string pin, Action<VerifySmsResponse> onSuccess = null, Action<string> onError = null)
        {
            string url = $"{ApiBase}/transfers/verify-sms";
            var payload = new { transaction_id = transactionId, sms_pin = pin };
            return PostAsync(url, payload, onSuccess, onError);
        }


        // =========================
        // CLAIM TRANSFER
        // =========================
        public Task<ClaimTransferResponse> ClaimCurrencyAsync(string claimCode, string playerName, string playerPhone, Action<ClaimTransferResponse> onSuccess = null, Action<string> onError = null)
        {
            string url = $"{ApiBase}/currency-sends/claim-currency";
            var payload = new
            {
                claim_code = claimCode,
                target_player_name = playerName,
                target_player_phone = playerPhone
            };
            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // PURCHASE ITEM
        // =========================
        public Task<PurchaseItemResponse> PurchaseItemAsync(string playerEmail, string playerName, string itemId, string itemName, int quantity, float unitPrice, float totalPrice, Action<PurchaseItemResponse> onSuccess, Action<string> onError)
        {
            string url = $"{ApiBase}/item-purchases/purchase-item";
            var payload = new
            {
                client_request_id = Guid.NewGuid().ToString(),
                player_email = playerEmail,
                player_name = playerName,
                item_id = itemId,
                item_name = itemName,
                item_quantity = quantity,
                unit_price = unitPrice.ToString("F2"),
                total_price = totalPrice.ToString("F2")
            };
            return PostAsync(url, payload, onSuccess, onError);
        }

        // =========================
        // HTTP HELPERS WITH AUTH HEADERS
        // =========================
        private void AddAuthHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("X-Game-Secret-Key", ApiKey);
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(_accessToken))
                request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

            if (!string.IsNullOrEmpty(_csrfToken))
                request.SetRequestHeader("X-CSRF-Token", _csrfToken);
        }

        private async Task<T> GetAsync<T>(string url, Action<T> onSuccess = null, Action<string> onError = null)
        {
            using var req = UnityWebRequest.Get(url);
            AddAuthHeaders(req);

            await req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonConvert.DeserializeObject<T>(req.downloadHandler.text);
                onSuccess?.Invoke(resp);
                return resp;
            }

            string err = $"GET {url} failed: {req.error}\n{req.downloadHandler.text}";
            onError?.Invoke(err);
            throw new Exception(err);
        }

        private async Task<T> PostAsync<T>(string url, object payload, Action<T> onSuccess = null, Action<string> onError = null)
        {
            string json = JsonConvert.SerializeObject(payload);
            byte[] body = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            AddAuthHeaders(req);

            await req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonConvert.DeserializeObject<T>(req.downloadHandler.text);
                onSuccess?.Invoke(resp);
                return resp;
            }

            string err = $"POST {url} failed: {req.error}\n{req.downloadHandler.text}";
            onError?.Invoke(err);
            throw new Exception(err);
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

        public string GetPlayerEmail() => config?.playerEmail ?? string.Empty;
        public string GetPlayerName() => config?.playerName ?? string.Empty;
        public string GetGameName() => config?.gameName ?? string.Empty;
        public string GetGameCurrency() => config?.gameCurrencyName ?? string.Empty;
    }
}
