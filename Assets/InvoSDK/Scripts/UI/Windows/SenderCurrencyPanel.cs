using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    public class SenderCurrencyPanel : MonoBehaviour
    {
        [Header("Step Panels")]
        public GameObject step1Panel; // Recipient
        public GameObject step2Panel; // Review
        public GameObject step3Panel; // Verification
        public GameObject step4Panel; // Confirmation

        [Header("Step Indicators")]
        public TMP_Text step1Label;
        public TMP_Text step2Label;
        public TMP_Text step3Label;
        public TMP_Text step4Label;

        [Header("Step 1 - Recipient & Amount")]
        public TMP_InputField senderPhoneInput;
        public TMP_InputField receiverPhoneInput;
        public TMP_Dropdown toGameDropdown;
        public TMP_InputField amountInput;
        public TMP_Text availableBalanceText;
        public TMP_Text currencyNameText;
        public Image fromGameIcon;
        public TMP_Text fromGameNameText;
        public TMP_Text fromCurrencyText;
        public Image fromCurrencyIcon;
        public Button nextButtonStep1;

        [Header("Amount Summary Display")]
        public TMP_Text amountEnteredText;
        public TMP_Text serviceFeeText;
        public TMP_Text receiverGetsText;
        [Range(0f, 1f)] public float serviceFeePercent = 0.10f;

        [Header("Step 2 - Review Details")]
        public TMP_Text reviewFromPhone;
        public TMP_Text reviewToPhone;
        public TMP_Text reviewFromGame;
        public TMP_Text reviewToGame;
        public TMP_Text reviewSendAmount;
        public TMP_Text reviewServiceFee;
        public TMP_Text reviewReceiverGets;
        public Button confirmButtonStep2;
        public Button backButtonStep2;

        [Header("Step 3 - SMS Verification")]
        [SerializeField] private VerificationCodeInput verificationCodeInput;
        public Button backButtonStep3;

        [Header("Step 4 - Confirmation")]
        public TMP_Text confirmationTitle;
        public TMP_Text confirmationSubtitle;
        public TMP_Text claimCodeLabel;
        public Button doneButton;

        private List<TransferResponse.AvailableGame> availableGames = new();
        private TransferResponse.AvailableGame selectedToGame;
        private string sourceGameId;
        private InvoSDKConfig config;

        private float enteredAmount;
        private float serviceFee;
        private int currentStep = 1;

        private string currentTransactionId;
        private string currentClaimCode;

        private void OnEnable()
        {
            _ = InitializeAsync();
            verificationCodeInput.OnCodeCompleted += OnVerificationCodeEntered;
        }

        private void OnDisable()
        {
            verificationCodeInput.OnCodeCompleted -= OnVerificationCodeEntered;
        }

        // ------------------------------------------------------------------
        private async Task InitializeAsync()
        {
            config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            if (config == null)
            {
                Debug.LogError("[InvoSDK] Missing SDK config asset!");
                return;
            }

            senderPhoneInput.onValueChanged.AddListener(value =>
            {
                // format and set back to the input field
                var formatted = FormatPhoneNumber(value);
                if (senderPhoneInput.text != formatted) senderPhoneInput.text = formatted;
            });

            receiverPhoneInput.onValueChanged.AddListener(value =>
            {
                var formatted = FormatPhoneNumber(value);
                if (receiverPhoneInput.text != formatted) receiverPhoneInput.text = formatted;
            });
            amountInput.onValueChanged.AddListener(_ => UpdateAmounts());

            nextButtonStep1.onClick.AddListener(OnStep1Next);
            confirmButtonStep2.onClick.AddListener(async () => await OnConfirmSendAsync());
            backButtonStep2.onClick.AddListener(() => ShowStep(1));
            backButtonStep3.onClick.AddListener(() => ShowStep(2));
            doneButton.onClick.AddListener(async () => await OnClaimCurrencyAsync());

            ShowStep(1);
            await LoadAvailableGames();
            UpdateAmounts();
        }

        private void ShowStep(int step)
        {
            currentStep = step;
            step1Panel.SetActive(step == 1);
            step2Panel.SetActive(step == 2);
            step3Panel.SetActive(step == 3);
            step4Panel.SetActive(step == 4);

            step1Label.alpha = step == 1 ? 1f : 0.4f;
            step2Label.alpha = step == 2 ? 1f : 0.4f;
            step3Label.alpha = step == 3 ? 1f : 0.4f;
            step4Label.alpha = step == 4 ? 1f : 0.4f;
        }

        // ------------------------------------------------------------------
        private async Task LoadAvailableGames()
        {
            var resp = await APIManager.Instance.GetAvailableDestinationsAsync();
            if (resp == null)
            {
                Debug.LogError("[InvoSDK] Available destinations response null.");
                return;
            }

            availableGames = resp.available_games;
            sourceGameId = resp.source_game_id;

            fromGameNameText.text = config.gameName;
            fromCurrencyText.text = config.gameCurrencyName;
            currencyNameText.text = config.gameCurrencyName;

            _ = LoadSpriteFromUrl(config.gameIconUrl, fromGameIcon);
            _ = LoadSpriteFromUrl(config.gameCurrencyUrl, fromCurrencyIcon);

            var filtered = availableGames.FindAll(g => g.game_id != sourceGameId);
            toGameDropdown.ClearOptions();
            toGameDropdown.AddOptions(filtered.ConvertAll(g => g.game_name));

            toGameDropdown.onValueChanged.RemoveAllListeners();
            toGameDropdown.onValueChanged.AddListener(i => selectedToGame = filtered[i]);

            if (filtered.Count > 0)
            {
                selectedToGame = filtered[0];
                toGameDropdown.value = 0;
            }
        }

        // ------------------------------------------------------------------
        private void UpdateAmounts()
        {
            if (!float.TryParse(amountInput.text, out enteredAmount) || enteredAmount <= 0)
            {
                amountEnteredText.text = $"0 {config?.gameCurrencyName}";
                serviceFeeText.text = $"-0 {config?.gameCurrencyName}";
                receiverGetsText.text = $"0 {config?.gameCurrencyName}";
                return;
            }

            serviceFee = enteredAmount * serviceFeePercent;
            float net = enteredAmount - serviceFee;

            amountEnteredText.text = $"{enteredAmount:F2} {config.gameCurrencyName}";
            serviceFeeText.text = $"-{serviceFee:F2} {config.gameCurrencyName}";
            receiverGetsText.text = $"{net:F2} {config.gameCurrencyName}";
        }

        // ------------------------------------------------------------------
        private void OnStep1Next()
        {
            if (!float.TryParse(amountInput.text, out enteredAmount) || enteredAmount <= 0)
            {
                Debug.LogWarning("[InvoSDK] Invalid amount entered.");
                return;
            }

            serviceFee = enteredAmount * serviceFeePercent;
            float receiverGets = enteredAmount - serviceFee;

            reviewFromPhone.text = FormatPhoneNumber(senderPhoneInput.text);
            reviewToPhone.text = FormatPhoneNumber(receiverPhoneInput.text);
            reviewFromGame.text = config.gameName;
            reviewToGame.text = selectedToGame?.game_name ?? "N/A";
            reviewSendAmount.text = $"{enteredAmount:F2}";
            reviewServiceFee.text = $"-{serviceFee:F2}";
            reviewReceiverGets.text = $"{receiverGets:F2}";

            ShowStep(2);
        }

        // ------------------------------------------------------------------
        private async Task OnConfirmSendAsync()
        {
            Debug.Log("[InvoSDK] Sending initiate-send...");
            await APIManager.Instance.InitiateSendAsync(
                senderName: APIManager.Instance.GetPlayerName(),
                senderEmail: APIManager.Instance.GetPlayerEmail(),
                senderPhone: FormatPhoneNumber(senderPhoneInput.text),
                receiverPhone: FormatPhoneNumber(receiverPhoneInput.text),
                receivingGameId: selectedToGame?.game_id,
                amount: enteredAmount.ToString("F2"),
                onSuccess: resp =>
                {
                    Debug.Log(resp);
                    currentTransactionId = resp.transaction_id;
                    Debug.Log($"[InvoSDK] InitiateSend success. TxID: {currentTransactionId}");

                    ShowStep(3);
                },
                onError: err => Debug.LogError($"[InvoSDK] InitiateSend failed: {err}")
            );
        }

        // ------------------------------------------------------------------
        private async void OnVerificationCodeEntered(string code)
        {
            if (string.IsNullOrEmpty(currentTransactionId))
            {
                Debug.LogError("[InvoSDK] No transaction ID for verification.");
                return;
            }

            await APIManager.Instance.VerifySmsAsync(
                currentTransactionId, code,
                onSuccess: resp =>
                {
                    currentClaimCode = resp.claim_code;
                   // confirmationTitle.text = "✅ Transfer Verified";
                   // confirmationSubtitle.text = "Transfer confirmed successfully.";
                    Debug.Log( $"Claim Code: {currentClaimCode}");
                    ShowStep(4);
                },
                onError: err => Debug.LogError($"[InvoSDK] Verify SMS failed: {err}")
            );
        }

        // ------------------------------------------------------------------
        private async Task OnClaimCurrencyAsync()
        {
            if (string.IsNullOrEmpty(currentClaimCode))
            {
                ResetPanel();
                return;
            }

            await APIManager.Instance.ClaimCurrencyAsync(
                claimCode: currentClaimCode,
                playerName: APIManager.Instance.GetPlayerName(),
                playerPhone: FormatPhoneNumber(receiverPhoneInput.text),
                onSuccess: resp =>
                {
                    confirmationSubtitle.text = "Funds successfully claimed!";
                    Invoke(nameof(ResetPanel), 2f); 
                },
                onError: err =>
                {
                    Debug.LogError($"[InvoSDK] Claim failed: {err}");
                    ResetPanel();
                });
        }

        // ------------------------------------------------------------------
        private void ResetPanel()
        {
            senderPhoneInput.text = "";
            receiverPhoneInput.text = "";
            amountInput.text = "";
            amountEnteredText.text = serviceFeeText.text = receiverGetsText.text = "";
            currentTransactionId = currentClaimCode = "";
            ShowStep(1);
        }

        private string FormatPhoneNumber(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            string digits = Regex.Replace(raw, @"[^\d+]", "");
            if (!digits.StartsWith("+")) digits = "+" + digits;
            return digits.Length > 13 ? digits.Substring(0, 13) : digits;
        }

        private async Task LoadSpriteFromUrl(string url, Image target)
        {
            if (string.IsNullOrEmpty(url)) return;
            using var req = UnityWebRequestTexture.GetTexture(url);
            await req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                target.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
            }
        }
    }
}
