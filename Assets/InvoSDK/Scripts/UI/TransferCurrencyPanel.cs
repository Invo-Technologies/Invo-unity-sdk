using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using static InvoSDK.TransferResponse;

namespace InvoSDK.UI
{
    /// <summary>
    /// Four-step TRANSFER flow: the player moves their own balance to another game.
    ///   Step 1  Destination + amount    Step 2  Review
    ///   Step 3  Approval on the phone: a QR (desktop, Steam, console) or the INVO page in the
    ///           system browser (iOS, Android). No SMS code.
    ///   Step 4  Approved - collect it in the destination game
    /// </summary>
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
        public TMP_InputField senderPhoneInput;    // the local player: SOURCE of the transfer
        public TMP_InputField receiverPhoneInput;  // the recipient: TARGET of the transfer
        public TMP_Dropdown toGameDropdown;
        public Image toGameIcon;
        public TMP_Text toGameNameText;
        public TMP_Text toCurrencyNameText;
        public Image toCurrencyIcon;

        [Header("Step 1 - Validation (optional)")]
        public TMP_Text senderPhoneErrorText;
        public TMP_Text receiverPhoneErrorText;
        public TMP_Text amountErrorText;
        public TMP_Text destinationsStatusText;

        [Header("From Game Info (Auto)")]
        public Image fromGameIcon;
        public TMP_Text fromGameNameText;
        public TMP_Text fromCurrencyNameText;
        public Image fromCurrencyIcon;

        [Header("Amount Inputs")]
        public TMP_InputField amountInputField;
        public TMP_Text serviceFeeText;
        public TMP_Text receiverGetsText;
        [Tooltip("Local pre-initiate ESTIMATE only. Invo applies per-tenant rates; the real fee " +
                 "comes back on the initiate response and replaces this on screen.")]
        [Range(0f, 1f)] public float serviceFeePercent = 0.1f;
        public TMP_Text feeEstimateNoticeText;

        [Header("Step 2 - Review Summary")]
        public TMP_Text reviewFromGameText;
        public TMP_Text reviewToGameText;
        public TMP_Text reviewAmountText;
        public TMP_Text reviewFeeText;
        public TMP_Text reviewReceiverGetsText;

        [Header("Step 3 - Phone Approval (replaces the SMS code)")]
        [Tooltip("The QR / approval view, ideally inside inAppApprovalGroup. Leave empty to use the built-in overlay.")]
        public InvoDeviceApprovalPanelView approvalView;
        [Tooltip("Shown after a decline, an expired code or a cancel: shows a fresh QR for the same transfer.")]
        public Button retryApprovalButton;
        public TMP_Text verificationStatusText;
        [Tooltip("Container shown while the player approves on their phone.")]
        public GameObject inAppApprovalGroup;
        [Tooltip("Instruction line for the approval.")]
        public TMP_Text inAppApprovalText;

        [Header("Step 3 - LEGACY SMS widgets (hidden at runtime; remove from your prefab)")]
        public VerificationCodeInput verificationCodeInput;
        public GameObject smsVerificationGroup;
        public Button resendPinButton;
        public TMP_Text resendPinLabelText;

        [Header("Step 3 - Guardian Approval (optional)")]
        public GameObject guardianApprovalGroup;
        public TMP_Text guardianApprovalText;

        [Header("Step 4 - Confirmation")]
        public TMP_Text confirmationTitleText;
        public TMP_Text confirmationSubtitleText;
        public Image confirmationGameIcon;
        public TMP_Text claimCodeText;
        public TMP_Text claimCodeExpiryText;

        [Header("Buttons")]
        public Button step1NextButton;
        public Button step2PrevButton;
        public Button step2NextButton;
        public Button step3PrevButton;
        [Tooltip("LEGACY: submitted the SMS code. Hidden at runtime.")]
        public Button step3VerifyButton;
        public Button step4CloseButton;

        // ---------------- CONSTANTS ----------------
        private const string ReadmeHint = "See the InvoSDK README (\"Flows not implemented in the plugin\").";

        private List<TransferResponse.AvailableGame> availableGames = new();
        private TransferResponse.AvailableGame selectedToGame;
        private InvoSDKConfig config;
        private string sourceGameId;
        private int currentStep = 1;

        private string _transactionId;
        private string _clientRequestId;          // minted once per confirmed intent, reused across retries
        private bool _isProcessing;               // initiate in flight
        private bool _isApproving;                // phone approval in flight
        private bool _isWaiting;                  // guardian / recipient hold in flight
        private bool _listenersBound;
        private CancellationTokenSource _pollCts;

        private void Awake()
        {
            config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            if (config == null)
                Debug.LogError("[InvoSDK] Missing InvoSDKConfig asset.");

            ValidateWiring();
            BindListeners();
        }

        // Bound once, here, on purpose: OnEnable used to re-add six onClick listeners every time the
        // panel opened, so on the Nth open a single tap fired N requests.
        private void BindListeners()
        {
            if (_listenersBound) return;
            _listenersBound = true;

            if (step1NextButton != null) step1NextButton.onClick.AddListener(OnStep1NextClicked);
            if (step2PrevButton != null) step2PrevButton.onClick.AddListener(() => ChangeStep(1));
            if (step2NextButton != null) step2NextButton.onClick.AddListener(() => { _ = OnInitiateTransferClicked(); });
            if (step3PrevButton != null) step3PrevButton.onClick.AddListener(OnStep3BackClicked);
            if (step4CloseButton != null) step4CloseButton.onClick.AddListener(OnCloseClicked);
            if (retryApprovalButton != null) retryApprovalButton.onClick.AddListener(OnRetryApprovalClicked);

            if (amountInputField != null) amountInputField.onValueChanged.AddListener(_ => UpdateAmounts());
            if (senderPhoneInput != null) senderPhoneInput.onValueChanged.AddListener(_ => ValidateStep1());
            if (receiverPhoneInput != null) receiverPhoneInput.onValueChanged.AddListener(_ => ValidateStep1());
        }

        // MainScene.unity ships with several of these unassigned ({fileID: 0}). Name them loudly so an
        // integrator gets a wiring message instead of a swallowed NullReferenceException.
        private void ValidateWiring()
        {
            RequireField(senderPhoneInput, nameof(senderPhoneInput));
            RequireField(receiverPhoneInput, nameof(receiverPhoneInput));
            RequireField(verificationStatusText, nameof(verificationStatusText));
            RequireField(amountInputField, nameof(amountInputField));
            RequireField(toGameDropdown, nameof(toGameDropdown));

            if (claimCodeText == null)
                Debug.LogWarning("[InvoSDK] TransferCurrencyPanel.claimCodeText is not assigned. " +
                                 "The fallback claim code for the destination game will not be shown.");
        }

        private static bool TryGetApi(out APIManager api)
        {
            api = APIManager.Instance;
            if (api != null) return true;

            Debug.LogError("[InvoSDK] APIManager.Instance is not available. Add the APIManager component to the " +
                           "scene before opening the transfer panel.");
            return false;
        }

        private static bool RequireField(UnityEngine.Object field, string fieldName)
        {
            if (field != null) return true;
            Debug.LogError($"[InvoSDK] TransferCurrencyPanel.{fieldName} is not assigned in the scene. " +
                           "Assign it on the TransferCurrencyPanel component or the transfer flow cannot run.");
            return false;
        }

        private void OnEnable() => ResetPanel();

        private void OnDisable() => CancelPolling();

        private void OnDestroy() => CancelPolling();

        // ---------------- STEP MANAGEMENT ----------------
        private void ShowStep(int step)
        {
            currentStep = Mathf.Clamp(step, 1, 4);

            if (step1Panel != null) step1Panel.SetActive(currentStep == 1);
            if (step2Panel != null) step2Panel.SetActive(currentStep == 2);
            if (step3Panel != null) step3Panel.SetActive(currentStep == 3);
            if (step4Panel != null) step4Panel.SetActive(currentStep == 4);

            if (step1Label != null) step1Label.alpha = currentStep == 1 ? 1f : 0.5f;
            if (step2Label != null) step2Label.alpha = currentStep == 2 ? 1f : 0.5f;
            if (step3Label != null) step3Label.alpha = currentStep == 3 ? 1f : 0.5f;
            if (step4Label != null) step4Label.alpha = currentStep == 4 ? 1f : 0.5f;
        }

        private void ChangeStep(int newStep) => ShowStep(newStep);

        private void OnStep1NextClicked()
        {
            if (!ValidateStep1()) return;
            PopulateReviewStep();
            ChangeStep(2);
        }

        private void OnStep3BackClicked()
        {
            // Leaving verification: stop approval polling, but keep the transaction and the
            // client_request_id so a retry stays the SAME transfer.
            CancelPolling();
            ChangeStep(2);
        }

        // ---------------- STEP 1: LOAD & SELECTION ----------------
        private async Task LoadAvailableGames()
        {
            SetText(destinationsStatusText, "Loading destinations...");
            SetInteractable(step1NextButton, false);

            if (!TryGetApi(out var api))
            {
                ShowDestinationsFailure("Transfers aren't available in this build.");
                return;
            }

            try
            {
                var response = await api.GetTransferDestinationsAsync();

                availableGames = response?.available_games ?? new List<TransferResponse.AvailableGame>();
                sourceGameId = response?.source_game_id;

                UpdateFromGameInfo();
                PopulateGameDropdown();

                if (GetDestinations().Count == 0)
                {
                    SetText(destinationsStatusText, "No other games are available to transfer to right now.");
                    SetInteractable(step1NextButton, false);
                    return;
                }

                SetText(destinationsStatusText, string.Empty);
                ValidateStep1();
            }
            catch (InvoApiException ex)
            {
                Debug.LogError($"[InvoSDK] Failed to fetch transfer destinations: {ex}");
                ShowDestinationsFailure(DescribeApiError(ex, "Couldn't load transfer destinations. Try again."));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InvoSDK] Unexpected error loading transfer destinations: {ex.Message}");
                ShowDestinationsFailure("Couldn't load transfer destinations. Try again.");
            }
        }

        private void ShowDestinationsFailure(string message)
        {
            availableGames = new List<TransferResponse.AvailableGame>();
            selectedToGame = null;
            PopulateGameDropdown();
            SetText(destinationsStatusText, message);
            SetInteractable(step1NextButton, false);
        }

        private void UpdateFromGameInfo()
        {
            if (config == null) return;

            SetText(fromGameNameText, config.gameName);
            SetText(fromCurrencyNameText, config.gameCurrencyName);
            _ = LoadSpriteFromUrl(config.gameIconUrl, fromGameIcon);
            _ = LoadSpriteFromUrl(config.gameCurrencyUrl, fromCurrencyIcon);
        }

        private List<TransferResponse.AvailableGame> GetDestinations() =>
            availableGames.FindAll(g => g != null && g.game_id != sourceGameId);

        private void PopulateGameDropdown()
        {
            if (toGameDropdown == null) return;

            var filtered = GetDestinations();
            toGameDropdown.ClearOptions();
            toGameDropdown.AddOptions(filtered.ConvertAll(g => g.game_name));
            toGameDropdown.onValueChanged.RemoveAllListeners();
            toGameDropdown.onValueChanged.AddListener(OnToGameSelected);

            if (filtered.Count > 0)
            {
                OnToGameSelected(0);
                toGameDropdown.value = 0;
            }
            else
            {
                selectedToGame = null;
            }
        }

        private void OnToGameSelected(int index)
        {
            var filtered = GetDestinations();
            if (index < 0 || index >= filtered.Count) return;

            selectedToGame = filtered[index];
            SetText(toGameNameText, selectedToGame.game_name);
            SetText(toCurrencyNameText, selectedToGame.currency_name);
            _ = LoadSpriteFromUrl(selectedToGame.game_icon, toGameIcon);
            _ = LoadSpriteFromUrl(selectedToGame.currency_symbol_url, toCurrencyIcon);

            UpdateAmounts();
        }

        // ---------------- STEP 1: VALIDATION ----------------
        private bool ValidateStep1()
        {
            bool ok = true;

            string sourceProblem = InvoPhone.DescribeProblem(ReadSenderPhoneRaw());
            SetText(senderPhoneErrorText, sourceProblem);
            if (sourceProblem != null) ok = false;

            string targetProblem = InvoPhone.DescribeProblem(ReadReceiverPhoneRaw());
            SetText(receiverPhoneErrorText, targetProblem);
            if (targetProblem != null) ok = false;

            if (!TryGetAmount(out _, out string amountProblem))
            {
                SetText(amountErrorText, amountProblem);
                ok = false;
            }
            else
            {
                SetText(amountErrorText, string.Empty);
            }

            if (selectedToGame == null) ok = false;

            SetInteractable(step1NextButton, ok);
            return ok;
        }

        /// <summary>
        /// Parses the amount with <see cref="InvoFormat.TryParseAmount"/> (float.TryParse is culture
        /// sensitive) and checks it against the destination's minimum_transfer / maximum_transfer.
        /// </summary>
        private bool TryGetAmount(out decimal amount, out string problem)
        {
            problem = null;
            amount = 0m;

            string raw = amountInputField != null ? amountInputField.text : null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                problem = "Enter an amount.";
                return false;
            }

            if (!InvoFormat.TryParseAmount(raw, out amount))
            {
                problem = "That isn't a valid amount.";
                return false;
            }

            if (amount <= 0m)
            {
                problem = "Enter an amount greater than zero.";
                return false;
            }

            if (selectedToGame != null)
            {
                if (InvoFormat.TryParseAmount(selectedToGame.minimum_transfer, out decimal min) && min > 0m && amount < min)
                {
                    problem = $"{selectedToGame.game_name} accepts a minimum of {InvoFormat.Amount(min)}.";
                    return false;
                }

                if (InvoFormat.TryParseAmount(selectedToGame.maximum_transfer, out decimal max) && max > 0m && amount > max)
                {
                    problem = $"{selectedToGame.game_name} accepts a maximum of {InvoFormat.Amount(max)}.";
                    return false;
                }
            }

            return true;
        }

        private string ReadSenderPhoneRaw()
        {
            if (senderPhoneInput != null && !string.IsNullOrWhiteSpace(senderPhoneInput.text))
                return senderPhoneInput.text;

            // Scene fallback: senderPhoneInput is unassigned in MainScene.unity. The source phone
            // belongs to the local player, so InvoSDKConfig.playerPhone is the correct stand-in.
            return config != null ? config.playerPhone : null;
        }

        private string ReadReceiverPhoneRaw() => receiverPhoneInput != null ? receiverPhoneInput.text : null;

        /// <summary>
        /// Source = the local player (this matches source_player_name / source_player_email, which
        /// both come from the local config). Target = the recipient. These used to be swapped.
        /// </summary>
        private bool TryGetPhones(out string sourcePhone, out string targetPhone)
        {
            sourcePhone = InvoPhone.Normalize(ReadSenderPhoneRaw());
            targetPhone = InvoPhone.Normalize(ReadReceiverPhoneRaw());

            if (sourcePhone == null)
            {
                string problem = InvoPhone.DescribeProblem(ReadSenderPhoneRaw());
                SetText(senderPhoneErrorText, problem);
                if (senderPhoneInput == null)
                    Debug.LogError("[InvoSDK] TransferCurrencyPanel.senderPhoneInput is not assigned and " +
                                   "InvoSDKConfig.playerPhone is empty or invalid, so the sender's phone number is unknown.");
                SetVerificationStatus(problem ?? "Check your phone number.");
                return false;
            }

            if (targetPhone == null)
            {
                string problem = InvoPhone.DescribeProblem(ReadReceiverPhoneRaw());
                SetText(receiverPhoneErrorText, problem);
                if (receiverPhoneInput == null)
                    Debug.LogError("[InvoSDK] TransferCurrencyPanel.receiverPhoneInput is not assigned, so the " +
                                   "recipient's phone number is unknown.");
                SetVerificationStatus(problem ?? "Check the recipient's phone number.");
                return false;
            }

            return true;
        }

        // ---------------- AMOUNTS & FEES ----------------
        private void UpdateAmounts()
        {
            if (!TryGetAmount(out decimal amount, out string problem))
            {
                SetText(serviceFeeText, "-0.00");
                SetText(receiverGetsText, "0.00");
                SetText(amountErrorText, string.IsNullOrEmpty(amountInputField != null ? amountInputField.text : null)
                    ? string.Empty
                    : problem);
                SetText(feeEstimateNoticeText, string.Empty);
                SetInteractable(step1NextButton, false);
                return;
            }

            // Local estimate only: there is no quote endpoint, and per-tenant rate overrides mean this
            // can differ from what Invo actually charges. Replaced by fees_preview after initiate.
            decimal fee = amount * (decimal)serviceFeePercent;
            decimal receiverGets = amount - fee;

            SetText(serviceFeeText, $"-{InvoFormat.Amount(fee)}");
            SetText(receiverGetsText, InvoFormat.Amount(receiverGets));
            SetText(feeEstimateNoticeText, "Estimated fee. Invo confirms the exact amount before you verify.");

            ValidateStep1();
        }

        private void PopulateReviewStep()
        {
            SetText(reviewFromGameText, config != null ? config.gameName : string.Empty);
            SetText(reviewToGameText, selectedToGame?.game_name ?? "N/A");
            SetText(reviewAmountText, amountInputField != null ? amountInputField.text : string.Empty);
            SetText(reviewFeeText, $"{(serviceFeeText != null ? serviceFeeText.text : "-0.00")} (estimate)");
            SetText(reviewReceiverGetsText, $"{(receiverGetsText != null ? receiverGetsText.text : "0.00")} (estimate)");
            SetText(feeEstimateNoticeText, "Estimated fee. Invo confirms the exact amount before you verify.");
        }

        /// <summary>Replaces the local estimate with the authoritative fees from the initiate response.</summary>
        private void ApplyFeesPreview(FeesPreview fees)
        {
            if (fees == null) return;

            if (!string.IsNullOrEmpty(fees.total_fee))
            {
                SetText(serviceFeeText, $"-{fees.total_fee}");
                SetText(reviewFeeText, $"-{fees.total_fee}");
            }

            if (!string.IsNullOrEmpty(fees.net_amount))
            {
                SetText(receiverGetsText, fees.net_amount);
                SetText(reviewReceiverGetsText, fees.net_amount);
            }

            SetText(feeEstimateNoticeText, "Confirmed by Invo.");
        }

        // ---------------- STEP 2: INITIATE TRANSFER ----------------
        private async Task OnInitiateTransferClicked()
        {
            if (_isProcessing) return;

            // Never a second reservation for the same intent: confirming again re-shows the
            // approval for the transfer that already exists.
            if (!string.IsNullOrEmpty(_transactionId))
            {
                StartApproval();
                return;
            }

            if (config == null)
            {
                SetVerificationStatus("Invo isn't configured in this build.");
                return;
            }

            if (!TryGetApi(out var api))
            {
                SetVerificationStatus("Invo isn't available in this build.");
                return;
            }

            if (selectedToGame == null)
            {
                SetVerificationStatus("Pick a game to transfer to.");
                ChangeStep(1);
                return;
            }

            if (!TryGetAmount(out decimal amount, out string amountProblem))
            {
                SetText(amountErrorText, amountProblem);
                SetVerificationStatus(amountProblem);
                ChangeStep(1);
                return;
            }

            if (!TryGetPhones(out string sourcePhone, out string targetPhone))
            {
                ChangeStep(1);
                return;
            }

            _isProcessing = true;
            SetInteractable(step2NextButton, false);

            try
            {
                // Minted once per confirmed intent and reused for every retry of THAT intent, so a
                // timeout followed by a retry is the same transfer to the backend, not a second one.
                if (string.IsNullOrEmpty(_clientRequestId))
                    _clientRequestId = APIManager.NewClientRequestId();

                Debug.Log($"[InvoSDK] Initiating transfer to {selectedToGame.game_name} " +
                          $"for {InvoPhone.Mask(targetPhone)}...");

                var data = await api.InitiateTransferAsync(
                    _clientRequestId,
                    api.GetPlayerName(),
                    api.GetPlayerEmail(),
                    sourcePhone,
                    targetPhone,
                    null,                       // the recipient's email is not collected by this panel
                    selectedToGame.game_id,
                    InvoFormat.Amount(amount));

                HandleInitiateResponse(data);
            }
            catch (InvoApiException ex)
            {
                Debug.LogError($"[InvoSDK] Transfer initiation failed: {ex}");
                HandleInitiateError(ex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InvoSDK] Transfer initiation error: {ex.Message}");
                ChangeStep(3);
                SetVerificationStatus("Something went wrong starting the transfer. Try again.");
            }
            finally
            {
                _isProcessing = false;
                SetInteractable(step2NextButton, true);
            }
        }

        /// <summary>
        /// A 2xx is not success. The outcome lives in the status STRING: a 202 means the transfer is
        /// parked waiting on a guardian, and the player cannot approve until it clears.
        /// </summary>
        private void HandleInitiateResponse(InitiateTransferResponse data)
        {
            if (data == null)
            {
                ChangeStep(3);
                SetVerificationStatus("Invo didn't answer. Try again.");
                return;
            }

            _transactionId = data.transaction_id;

            ApplyFeesPreview(data.transfer_details?.fees_preview);
            ChangeStep(3);

            if (data.status == InvoStatus.PendingGuardianApproval)
            {
                ShowGuardianApprovalState(data.guardian_approval);
                _ = WaitThenApproveAsync(_transactionId);
                return;
            }

            if (data.status == InvoStatus.NeedsAccountSelection)
            {
                // TODO: account selection is not implemented in this plugin - see the README.
                ShowTerminalFailure("More than one Invo account uses that number, so we can't tell which " +
                                    "one to send to. " + ReadmeHint);
                return;
            }

            if (data.status == InvoStatus.PendingConfirmation)
            {
                ShowVerificationGroup(guardianApprovalGroup);
                SetText(guardianApprovalText, "We are confirming the recipient's details. You can approve as soon as that finishes.");
                _ = WaitThenApproveAsync(_transactionId);
                return;
            }

            // verification_method is "in_app" or "sms". Both are approved on the phone now; "sms"
            // only means Invo also texted a legacy PIN, which this panel no longer asks for.
            if (data.status == InvoStatus.Success)
            {
                StartApproval();
                return;
            }

            Debug.LogWarning($"[InvoSDK] Unhandled initiate status '{data.status}'.");
            ShowTerminalFailure("Invo couldn't start this transfer. Try again later.");
        }

        private void HandleInitiateError(InvoApiException ex)
        {
            ChangeStep(3);
            ShowVerificationGroup(null);

            // Undocumented: a minor with no usable guardian on file. Terminal - retrying never helps.
            if (ex.StatusCode == 403 && ex.ErrorCode == "GUARDIAN_REQUIRED")
            {
                ShowTerminalFailure("This account needs a parent or guardian linked in the Invo app before " +
                                    "it can transfer currency.");
                return;
            }

            if (ex.StatusCode == 409 && ex.ErrorCode == "PHONE_SHARE_APPROVAL_REQUIRED")
            {
                // TODO: the phone-share one-time-code recovery flow is not implemented in this plugin - see the README.
                ShowTerminalFailure("That number is already shared with another Invo account. The account " +
                                    "holder has to approve it with a one-time code first. " + ReadmeHint);
                return;
            }

            if (ex.IsDuplicate)
            {
                // Same client_request_id, already accepted: not a new failure. Keep the intent so the
                // player can carry on verifying the transfer that already exists.
                SetVerificationStatus("This transfer was already started. Close and check your balance " +
                                      "before trying again.");
                return;
            }

            SetVerificationStatus(DescribeApiError(ex, "Invo couldn't start this transfer. Try again."));
        }

        // ---------------- STEP 3: PHONE APPROVAL (replaces the SMS PIN) ----------------
        private void OnRetryApprovalClicked() => StartApproval();

        private void StartApproval()
        {
            if (_isApproving || string.IsNullOrEmpty(_transactionId)) return;
            if (_isWaiting)
            {
                ChangeStep(3);
                SetVerificationStatus("This transfer is still on hold. You can approve it as soon as the hold clears.");
                return;
            }
            string transactionId = _transactionId;
            _ = RunApprovalAsync(transactionId);
        }

        private async Task RunApprovalAsync(string transactionId)
        {
            if (_isApproving) return;
            _isApproving = true;
            ChangeStep(3);
            ShowVerificationGroup(inAppApprovalGroup);
            SetRetryVisible(false);
            SetText(inAppApprovalText, Application.isMobilePlatform
                ? "Approve this transfer on the INVO page that opens. No code is texted to you."
                : "Scan the QR code with your phone and approve with your passkey. No code is texted to you.");
            SetVerificationStatus(string.Empty);

            try
            {
                CancelPolling();
                _pollCts = new CancellationTokenSource();
                var result = await InvoApprovalStep.ApproveAsync(
                    transactionId, InvoApprovalFlow.Transfer, approvalView,
                    id => APIManager.Instance.GetTransferStatusAsync(id), SetVerificationStatus, _pollCts.Token);
                if (this == null || _transactionId != transactionId) return;
                ApplyApprovalResult(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InvoSDK] Transfer approval error: {ex.Message}");
                SetVerificationStatus("Something went wrong approving the transfer. Check your balance, then try again.");
                SetRetryVisible(true);
            }
            finally
            {
                _isApproving = false;
            }
        }

        private void ApplyApprovalResult(InvoApprovalStepResult result)
        {
            if (result.Approved)
            {
                ShowClaimCode(result.ClaimCode, result.ClaimCodeExpiresAt);
                ShowConfirmation("Transfer approved",
                    $"Collect it in {selectedToGame?.game_name ?? "the other game"}.");
                ClearTransferIntent();
                if (APIManager.Instance != null) APIManager.Instance.StartBalancePolling();
                return;
            }

            if (result.Cancelled)
            {
                if (!isActiveAndEnabled) return;
                SetVerificationStatus("Approval stopped. Nothing was transferred. You can show the code again.");
                SetRetryVisible(true);
                return;
            }

            if (result.CanRetry)
            {
                SetVerificationStatus(result.Message);
                SetRetryVisible(true);
            }
            else
            {
                ShowTerminalFailure(result.Message);
            }
        }

        private void SetRetryVisible(bool visible)
        {
            if (retryApprovalButton != null) retryApprovalButton.gameObject.SetActive(visible);
        }

        private void ShowGuardianApprovalState(GuardianApproval approval)
        {
            ShowVerificationGroup(guardianApprovalGroup);
            SetText(guardianApprovalText, "Waiting for a parent or guardian to approve this transfer. " +
                                          "Once they do, you approve it on your phone." + FormatExpiry(approval?.expires_at));
            SetVerificationStatus("Waiting for parent or guardian approval...");
        }

        /// <summary>A guardian or a recipient check holds the transfer; the sender approves once it clears.</summary>
        private async Task WaitThenApproveAsync(string transactionId)
        {
            if (_isWaiting) return;
            _isWaiting = true;
            CancelPolling();
            _pollCts = new CancellationTokenSource();
            try
            {
                var waited = await InvoApprovalStep.WaitUntilApprovableAsync(
                    transactionId, id => APIManager.Instance.GetTransferStatusAsync(id), _pollCts.Token);
                if (this == null || _transactionId != transactionId) return;
                if (waited == null)
                {
                    await RunApprovalAsync(transactionId);
                    return;
                }
                ApplyApprovalResult(waited);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InvoSDK] Transfer hold wait error: {ex.Message}");
                SetVerificationStatus("We lost track of this transfer. Check your balance before trying again.");
            }
            finally
            {
                _isWaiting = false;
            }
        }

        /// <summary>The SMS code boxes may still be in an older prefab; keep them out of sight.</summary>
        private void HideLegacySmsWidgets()
        {
            if (smsVerificationGroup != null) smsVerificationGroup.SetActive(false);
            if (verificationCodeInput != null) verificationCodeInput.gameObject.SetActive(false);
            if (resendPinButton != null) resendPinButton.gameObject.SetActive(false);
            if (step3VerifyButton != null) step3VerifyButton.gameObject.SetActive(false);
        }

        private void ShowVerificationGroup(GameObject active)
        {
            HideLegacySmsWidgets();
            if (inAppApprovalGroup != null) inAppApprovalGroup.SetActive(active == inAppApprovalGroup);
            if (guardianApprovalGroup != null) guardianApprovalGroup.SetActive(active == guardianApprovalGroup);
        }

        private void CancelPolling()
        {
            if (_pollCts == null) return;

            try { _pollCts.Cancel(); }
            catch (ObjectDisposedException) { }

            _pollCts.Dispose();
            _pollCts = null;
        }

        // ---------------- STEP 4: CONFIRMATION ----------------
        private void ShowConfirmation(string title, string subtitle)
        {
            CancelPolling();
            SetText(confirmationTitleText, title);
            SetText(confirmationSubtitleText, subtitle);
            _ = LoadSpriteFromUrl(selectedToGame?.game_icon, confirmationGameIcon);
            ChangeStep(4);
        }

        /// <summary>
        /// The self-claim code from the approve call, for a destination game that collects by code.
        /// Never logged: Debug.Log persists to Player.log and logcat.
        /// </summary>
        private void ShowClaimCode(string claimCode, string expiresAt)
        {
            if (string.IsNullOrEmpty(claimCode))
            {
                SetText(claimCodeText, string.Empty);
                SetText(claimCodeExpiryText, "Open the other game to collect it.");
                return;
            }

            // The destination game normally collects with its own phone approval; this code is the
            // fallback for a game that asks for one.
            SetText(claimCodeText, claimCode);
            SetText(claimCodeExpiryText, string.IsNullOrEmpty(expiresAt)
                ? "If the other game asks for a claim code, use this one."
                : $"If the other game asks for a claim code, use this one. It expires {expiresAt}.");
        }

        private void ShowTerminalFailure(string message)
        {
            CancelPolling();
            ShowVerificationGroup(null);
            SetVerificationStatus(message);
            ClearTransferIntent();
        }

        private void OnCloseClicked() => ResetPanel();

        // ---------------- UTILITIES ----------------
        private async Task LoadSpriteFromUrl(string url, Image targetImage)
        {
            if (string.IsNullOrEmpty(url) || targetImage == null) return;

            try
            {
                using var req = UnityWebRequestTexture.GetTexture(url);
                await req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success || targetImage == null) return;

                var tex = DownloadHandlerTexture.GetContent(req);
                targetImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InvoSDK] Couldn't load image: {ex.Message}");
            }
        }

        /// <summary>Terminal outcomes only: a retry of the same intent must reuse the same ids.</summary>
        private void ClearTransferIntent()
        {
            _transactionId = null;
            _clientRequestId = null;
        }

        private void ResetPanel()
        {
            CancelPolling();
            ClearTransferIntent();

            selectedToGame = null;
            availableGames = new List<TransferResponse.AvailableGame>();
            sourceGameId = null;

            if (amountInputField != null) amountInputField.text = string.Empty;
            if (receiverPhoneInput != null) receiverPhoneInput.text = string.Empty;
            if (senderPhoneInput != null)
                senderPhoneInput.text = config != null && config.playerPhone != null ? config.playerPhone : string.Empty;

            SetText(serviceFeeText, "-0.00");
            SetText(receiverGetsText, "0.00");
            SetText(feeEstimateNoticeText, string.Empty);
            SetText(senderPhoneErrorText, string.Empty);
            SetText(receiverPhoneErrorText, string.Empty);
            SetText(amountErrorText, string.Empty);
            SetText(claimCodeText, string.Empty);
            SetText(claimCodeExpiryText, string.Empty);
            SetVerificationStatus(string.Empty);
            ShowVerificationGroup(null);
            SetRetryVisible(false);

            ShowStep(1);
            SetInteractable(step1NextButton, false);
            _ = LoadAvailableGames();
        }

        private void SetVerificationStatus(string message)
        {
            if (verificationStatusText == null)
            {
                if (!string.IsNullOrEmpty(message))
                    Debug.LogError("[InvoSDK] TransferCurrencyPanel.verificationStatusText is not assigned, so the " +
                                   $"player cannot see: \"{message}\"");
                return;
            }

            verificationStatusText.text = message;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }

        private static string FormatExpiry(string expiresAt) =>
            string.IsNullOrEmpty(expiresAt) ? string.Empty : $" Expires {expiresAt}.";

        /// <summary>
        /// Player-facing copy for an API failure. Never renders <see cref="InvoApiException.Body"/> -
        /// the raw envelope can contain another player's phone number and email.
        /// </summary>
        private static string DescribeApiError(InvoApiException ex, string fallback)
        {
            if (ex == null) return fallback;

            string message = fallback;

            if (ex.IsNetworkError)
            {
                message = "No connection to Invo. Check your internet and try again.";
            }
            else if (ex.IsRateLimited)
            {
                int wait = ex.RetryAfterSeconds ?? 30;
                message = $"Too many attempts. Try again in {wait} second{(wait == 1 ? string.Empty : "s")}.";
            }
            else
            {
                switch (ex.ErrorCode)
                {
                    case "GUARDIAN_REQUIRED":
                        message = "This account needs a parent or guardian linked in the Invo app before it can " +
                                  "transfer currency.";
                        break;
                    case "PHONE_SHARE_APPROVAL_REQUIRED":
                        message = "That number is shared with another Invo account and has to be approved with a " +
                                  "one-time code first. " + ReadmeHint;
                        break;
                    case "TENANT_NOT_MIGRATED":
                        message = "Phone approval is not enabled for this game yet. Contact Invo to enable it.";
                        break;
                    case APIManager.GameServerRequiredCode:
                        message = "This build is not connected to its game server.";
                        break;
                    default:
                        if (ex.StatusCode == 403) message = "Invo declined this transfer for this account.";
                        else if (ex.IsDuplicate) message = "This transfer was already submitted. Check your balance " +
                                                           "before trying again.";
                        else if (ex.StatusCode >= 500) message = "Invo is having trouble right now. Try again in a " +
                                                                 "few minutes.";
                        break;
                }
            }

            return string.IsNullOrEmpty(ex.ErrorId) ? message : $"{message} (ref {ex.ErrorId})";
        }
    }
}
