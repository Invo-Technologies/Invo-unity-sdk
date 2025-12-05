using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    public class TransferCurrencyPanel : MonoBehaviour
    {
        [Header("Step Panels")]
        public GameObject step1Panel;
        public GameObject step2Panel;
        public GameObject step3Panel;
        public GameObject step4Panel;

        [Header("Step Indicators")]
        public TMP_Text step1Label;
        public TMP_Text step2Label;
        public TMP_Text step3Label;
        public TMP_Text step4Label;

        [Header("Step 1 - Transfer Info")]
        public TMP_InputField senderPhoneInput;
        public TMP_InputField receiverPhoneInput;
        public TMP_Dropdown toGameDropdown;
        public Image toGameIcon;
        public TMP_Text toGameNameText;
        public TMP_Text toCurrencyNameText;
        public Image toCurrencyIcon;

        [Header("From Game Info (Auto)")]
        public Image fromGameIcon;
        public TMP_Text fromGameNameText;
        public TMP_Text fromCurrencyNameText;
        public Image fromCurrencyIcon;

        [Header("Amount Inputs")]
        public TMP_InputField amountInputField;
        public TMP_Text serviceFeeText;
        public TMP_Text receiverGetsText;
        [Range(0f, 1f)] public float serviceFeePercent = 0.1f;

        [Header("Step 2 - Review Summary")]
        public TMP_Text reviewFromGameText;
        public TMP_Text reviewToGameText;
        public TMP_Text reviewAmountText;
        public TMP_Text reviewFeeText;
        public TMP_Text reviewReceiverGetsText;

        [Header("Step 3 - Verification")]
        public VerificationCodeInput verificationCodeInput;
        public TMP_Text verificationStatusText;

        [Header("Step 4 - Confirmation")]
        public TMP_Text confirmationTitleText;
        public TMP_Text confirmationSubtitleText;
        public Image confirmationGameIcon;

        [Header("Buttons")]
        public Button step1NextButton;
        public Button step2PrevButton;
        public Button step2NextButton;
        public Button step3PrevButton;
        public Button step3VerifyButton;
        public Button step4CloseButton;

        private List<TransferResponse.AvailableGame> availableGames = new();
        private TransferResponse.AvailableGame selectedToGame;
        private InvoSDKConfig config;
        private string sourceGameId;
        private int currentStep = 1;

        private string _transactionId;
        private bool _isProcessing = false;

        private void Awake()
        {
            config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            if (config == null)
                Debug.LogError("[InvoSDK] Missing InvoSDKConfig asset.");
        }

        private void OnEnable()
        {
            step1NextButton.onClick.AddListener(() => { PopulateReviewStep(); ChangeStep(2); });
            step2PrevButton.onClick.AddListener(() => ChangeStep(1));
            step2NextButton.onClick.AddListener(() => { _ = OnInitiateTransferClicked(); });
            step3PrevButton.onClick.AddListener(() => ChangeStep(2));
            step3VerifyButton.onClick.AddListener(() => _ = OnVerifyTransferClicked());
            step4CloseButton.onClick.AddListener(OnCloseClicked);

            amountInputField.onValueChanged.AddListener(_ => UpdateAmounts());
            currentStep = 1;
            ShowStep(1);
            _ = LoadAvailableGames();
        }

        private void OnDisable() => amountInputField.onValueChanged.RemoveAllListeners();

        // ---------------- STEP MANAGEMENT ----------------
        private void ShowStep(int step)
        {
            step1Panel.SetActive(step == 1);
            step2Panel.SetActive(step == 2);
            step3Panel.SetActive(step == 3);
            step4Panel.SetActive(step == 4);

            step1Label.alpha = step == 1 ? 1f : 0.5f;
            step2Label.alpha = step == 2 ? 1f : 0.5f;
            step3Label.alpha = step == 3 ? 1f : 0.5f;
            step4Label.alpha = step == 4 ? 1f : 0.5f;
        }

        private void ChangeStep(int newStep)
        {
            currentStep = Mathf.Clamp(newStep, 1, 4);
            ShowStep(currentStep);
        }

        // ---------------- STEP 1: LOAD & SELECTION ----------------
        private async Task LoadAvailableGames()
        {
            var response = await APIManager.Instance.GetAvailableDestinationsAsync();
            if (response == null)
            {
                Debug.LogError("[InvoSDK] Failed to fetch available destinations.");
                return;
            }

            availableGames = response.available_games;
            sourceGameId = response.source_game_id;
            UpdateFromGameInfo();
            PopulateGameDropdown();
        }

        private void UpdateFromGameInfo()
        {
            fromGameNameText.text = config.gameName;
            fromCurrencyNameText.text = config.gameCurrencyName;
            _ = LoadSpriteFromUrl(config.gameIconUrl, fromGameIcon);
            _ = LoadSpriteFromUrl(config.gameCurrencyUrl, fromCurrencyIcon);
        }

        private void PopulateGameDropdown()
        {
            var filtered = availableGames.FindAll(g => g.game_id != sourceGameId);
            toGameDropdown.ClearOptions();
            toGameDropdown.AddOptions(filtered.ConvertAll(g => g.game_name));
            toGameDropdown.onValueChanged.RemoveAllListeners();
            toGameDropdown.onValueChanged.AddListener(OnToGameSelected);

            if (filtered.Count > 0)
            {
                OnToGameSelected(0);
                toGameDropdown.value = 0;
            }
        }

        private void OnToGameSelected(int index)
        {
            var filtered = availableGames.FindAll(g => g.game_id != sourceGameId);
            if (index < 0 || index >= filtered.Count) return;

            selectedToGame = filtered[index];
            toGameNameText.text = selectedToGame.game_name;
            toCurrencyNameText.text = selectedToGame.currency_name;
            _ = LoadSpriteFromUrl(selectedToGame.game_icon, toGameIcon);
            _ = LoadSpriteFromUrl(selectedToGame.currency_symbol_url, toCurrencyIcon);
        }

        private void UpdateAmounts()
        {
            if (!float.TryParse(amountInputField.text, out float amount))
            {
                serviceFeeText.text = "-0.00";
                receiverGetsText.text = "0.00";
                return;
            }

            float fee = amount * serviceFeePercent;
            float receiverGets = amount - fee;
            serviceFeeText.text = $"-{fee:F2}";
            receiverGetsText.text = $"{receiverGets:F2}";
        }

        private void PopulateReviewStep()
        {
            
            reviewFromGameText.text = config.gameName;
            reviewToGameText.text = selectedToGame?.game_name ?? "N/A";
            reviewAmountText.text = amountInputField.text;
            reviewFeeText.text = serviceFeeText.text;
            reviewReceiverGetsText.text = receiverGetsText.text;
        }

        // ---------------- STEP 2: INITIATE TRANSFER ----------------
        private async Task OnInitiateTransferClicked()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                string playerEmail = config.playerEmail;
                string playerName = config.playerName;
                string playerPhone = FormatPhoneNumber(receiverPhoneInput.text);
                string playerTargetPhone = FormatPhoneNumber(senderPhoneInput.text);
                string amount = amountInputField.text;
                string targetGameId = selectedToGame.game_id;

                Debug.Log($"[InvoSDK] Initiating transfer to {selectedToGame.game_name}...");

                string url = $"{APIManager.Instance.ApiBase}/transfers/initiate-transfer";
                var payload = new
                {
                    client_request_id = Guid.NewGuid().ToString(),
                    source_player_name = playerName,
                    source_player_email = playerEmail,
                    source_player_phone = playerPhone,
                    target_player_phone = playerTargetPhone,
                    target_game_id = targetGameId,
                    amount = amount
                };

                using var req = new UnityWebRequest(url, "POST");
                string json = JsonConvert.SerializeObject(payload);
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Game-Secret-Key", APIManager.Instance.ApiKey);

                await req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var data = JsonConvert.DeserializeObject<InitiateTransferResponse>(req.downloadHandler.text);
                    _transactionId = data.transaction_id;
                    Debug.Log($"[InvoSDK] Transfer initiated: {data.message}");
                    verificationStatusText.text = "Enter the SMS code sent to your phone.";
                    ChangeStep(3);
                }
                else
                {
                    Debug.LogError($"[InvoSDK] Transfer initiation failed: {req.downloadHandler.text}");
                    verificationStatusText.text = "Transfer initiation failed.";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[InvoSDK] Transfer error: {e.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ---------------- STEP 3: VERIFY TRANSFER ----------------
        private async Task OnVerifyTransferClicked()
        {
            string pin = verificationCodeInput.GetFullCode();
            if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(_transactionId))
            {
                verificationStatusText.text = "Missing PIN or transaction ID.";
                return;
            }

            try
            {
                var result = await APIManager.Instance.VerifySmsAsync(_transactionId, pin);
                if (result != null && result.status == "success")
                {
                    ShowConfirmation("Transfer Successful!", "Your transfer was verified successfully.");
                }
                else
                {
                    verificationStatusText.text = "Verification failed.";
                }
            }
            catch (Exception ex)
            {
                verificationStatusText.text = "Error verifying transfer: " + ex.Message;
            }
        }

        // ---------------- STEP 4: CONFIRMATION ----------------
        private void ShowConfirmation(string title, string subtitle)
        {
            confirmationTitleText.text = title;
            confirmationSubtitleText.text = subtitle;
            _ = LoadSpriteFromUrl(selectedToGame?.game_icon, confirmationGameIcon);
            ChangeStep(4);
        }

        private void OnCloseClicked() => ResetPanel();

        // ---------------- UTILITIES ----------------
        private async Task LoadSpriteFromUrl(string url, Image targetImage)
        {
            if (string.IsNullOrEmpty(url)) return;

            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                await req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var tex = DownloadHandlerTexture.GetContent(req);
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                    if (targetImage != null)
                        targetImage.sprite = sprite;
                }
            }
        }

        private void ResetPanel()
        {
            currentStep = 1;
            ShowStep(1);
            amountInputField.text = "";
            serviceFeeText.text = "-0.00";
            receiverGetsText.text = "0.00";
            verificationStatusText.text = "";
            _transactionId = null;
            _ = LoadAvailableGames();
        }
        private string FormatPhoneNumber(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            string digits = Regex.Replace(raw, @"[^\d+]", "");
            if (!digits.StartsWith("+")) digits = "+" + digits;
            return digits.Length > 13 ? digits.Substring(0, 13) : digits;
        }

        [Serializable]
        public class InitiateTransferResponse
        {
            public string status;
            public string message;
            public string transaction_id;
        }
    }
}
