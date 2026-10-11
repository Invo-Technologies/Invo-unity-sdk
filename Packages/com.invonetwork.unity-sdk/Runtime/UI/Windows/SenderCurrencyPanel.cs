using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
using static InvoSDK.TransferResponse;

namespace InvoSDK.UI
{
    /// <summary>
    /// Four-step SEND flow, driven by the player who is giving currency away.
    ///
    ///   Step 1  Recipient + amount
    ///   Step 2  Review (authoritative fees arrive only after initiate-send)
    ///   Step 3  Sender approval on the phone: a QR to scan (desktop, Steam, console) or the
    ///           INVO page in the system browser (iOS, Android). No SMS code. A guardian hold
    ///           can come first.
    ///   Step 4  Sent - the receiver collects it in the receiving game
    ///
    /// The sender NEVER claims. Collecting happens in the RECEIVING game, on the
    /// receiver's device, with the receiver's own phone approval (claim code as fallback).
    /// The claim UI in this file is a separate, self-contained panel for a player
    /// who is RECEIVING currency - see the "Claim Currency Panel" region.
    /// </summary>
    public class SenderCurrencyPanel : MonoBehaviour
    {
        [Header("Step Panels")]
        public GameObject step1Panel; // Recipient
        public GameObject step2Panel; // Review
        public GameObject step3Panel; // Verification
        public GameObject step4Panel; // Sent (share claim code)

        [Header("Step Indicators")]
        public TMP_Text step1Label;
        public TMP_Text step2Label;
        public TMP_Text step3Label;
        public TMP_Text step4Label;

        [Header("Step 1 - Recipient & Amount")]
        public TMP_InputField senderPhoneInput;
        public TMP_InputField receiverPhoneInput;
        public TMP_InputField receiverEmailInput;   // optional: routing is phone-based
        public TMP_Dropdown toGameDropdown;
        public TMP_InputField amountInput;
        public TMP_Text availableBalanceText;
        public TMP_Text currencyNameText;
        public Image fromGameIcon;
        public TMP_Text fromGameNameText;
        public TMP_Text fromCurrencyText;
        public Image fromCurrencyIcon;
        public Button nextButtonStep1;

        [Header("Step 1 - Validation Messages")]
        public TMP_Text senderPhoneErrorText;
        public TMP_Text receiverPhoneErrorText;
        public TMP_Text amountErrorText;
        public TMP_Text amountLimitsText;

        [Header("Amount Summary Display")]
        public TMP_Text amountEnteredText;
        public TMP_Text serviceFeeText;
        public TMP_Text receiverGetsText;
        public TMP_Text feeEstimateNoticeText;
        [Tooltip("Local ESTIMATE only. Per-tenant fee overrides mean the real fee " +
                 "comes back in the initiate-send response (fees_preview).")]
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

        [Header("Step 3 - Phone Approval (replaces the SMS code)")]
        [Tooltip("The QR / approval view inside step3Panel. Leave empty to use the built-in overlay.")]
        public InvoDeviceApprovalPanelView approvalView;
        [Tooltip("Shown after a decline, an expired code or a cancel: shows a fresh QR for the same send.")]
        public Button retryApprovalButton;
        [Tooltip("Instruction line above the QR.")]
        public TMP_Text verificationTargetText;
        public Button backButtonStep3;

        [Header("Step 3 - LEGACY SMS widgets (hidden at runtime; remove from your prefab)")]
        [SerializeField] private VerificationCodeInput verificationCodeInput;
        public TMP_Text verificationExpiresText;
        public Button resendPinButton;
        public TMP_Text resendPinStatusText;

        [Header("Step 3 - Hold while the recipient's details are confirmed")]
        public GameObject inAppApprovalPanel;
        public TMP_Text inAppApprovalStatusText;
        public Button inAppApprovalCancelButton;

        [Header("Step 3 - Guardian Approval Hold")]
        public GameObject guardianApprovalPanel;
        public TMP_Text guardianApprovalStatusText;
        public Button guardianApprovalCancelButton;

        [Header("Step 4 - Sent / Share Claim Code")]
        public TMP_Text confirmationTitle;
        public TMP_Text confirmationSubtitle;
        public TMP_Text claimCodeLabel;
        public TMP_Text claimCodeExpiryLabel;
        public Button copyClaimCodeButton;
        public Button doneButton;

        [Header("Claim Currency Panel (RECEIVER side - separate flow)")]
        public GameObject claimPanel;
        public Button openClaimPanelButton;
        public Button closeClaimPanelButton;
        public TMP_InputField claimCodeInput;
        public TMP_InputField claimNameInput;
        public TMP_InputField claimEmailInput;
        public TMP_InputField claimPhoneInput;
        public Button claimSubmitButton;
        public TMP_Text claimStatusText;

        [Header("Claim - Collect with phone approval (preferred over the claim code)")]
        [Tooltip("Shown when a send to the active player is waiting in this game.")]
        public Button collectPendingButton;
        public TMP_Text collectPendingText;
        [Tooltip("QR view for the receiver's collect, inside claimPanel. Leave empty to use the built-in overlay. " +
                 "Do not reuse approvalView: it lives in step 3, which is hidden while the claim panel is open.")]
        public InvoDeviceApprovalPanelView collectApprovalView;

        [Header("Claim - Account Picker (status == needs_account_selection)")]
        public GameObject accountSelectionPanel;
        public TMP_Dropdown accountSelectionDropdown;
        public Button accountSelectionConfirmButton;
        public Button accountSelectionCancelButton;

        [Header("Shared")]
        public TMP_Text statusText;
        public GameObject busyIndicator;

        // ------------------------------------------------------------------
        // A destination the player can send to. Wraps AvailableGame so the
        // synthetic "this game" (peer-to-peer) entry can sit in the same list.
        private class SendDestination
        {
            public string gameId;
            public string gameName;
            public string currencyName;
            public decimal? minimumTransfer;
            public decimal? maximumTransfer;
            public bool isSameGame;
        }

        private readonly List<SendDestination> destinations = new();
        private SendDestination selectedDestination;

        private List<AvailableGame> availableGames = new();
        private string sourceGameId;
        private InvoSDKConfig config;

        private decimal enteredAmount;
        private decimal serviceFee;
        private int currentStep = 1;

        private string currentTransactionId;
        private string currentClaimCode;
        private string currentClaimCodeExpiresAt;
        private string currentClientRequestId;      // minted once, reused on retry
        private string normalizedSenderPhone;
        private string normalizedReceiverPhone;
        private string receiverEmailValue;
        private string destinationCurrencyName;
        private string authoritativeNetAmount;

        private bool listenersBound;
        private bool sendInFlight;
        private bool approvalInFlight;
        private bool waitingInFlight;
        private bool claimInFlight;
        private bool claimCompleted;
        private List<AccountCandidate> claimCandidates = new();
        private PendingAction collectable;

        private CancellationTokenSource pollCts;

        // ==================================================================
        // Lifecycle
        // ==================================================================

        private void Awake()
        {
            // Bug 7: bind exactly once. OnEnable used to add a new listener on
            // every open, so N opens meant N initiate-send calls per tap - each
            // with its own client_request_id, which the server cannot dedupe.
            BindListeners();
        }

        private void OnEnable()
        {
            pollCts = new CancellationTokenSource();
            _ = RunGuarded(InitializeAsync);
        }

        private void OnDisable()
        {
            CancelPolling();
        }

        private void OnDestroy()
        {
            UnbindListeners();
            CancelPolling();
        }

        private void CancelPolling()
        {
            if (pollCts == null) return;
            try { pollCts.Cancel(); } catch (ObjectDisposedException) { }
            pollCts.Dispose();
            pollCts = null;
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            listenersBound = true;

            // Phone fields are validated, never rewritten - see bug 4. Rewriting
            // the field mid-typing used to truncate real numbers into different,
            // still-valid numbers, sending the claim SMS to a stranger.
            if (senderPhoneInput != null)
                senderPhoneInput.onValueChanged.AddListener(OnSenderPhoneChanged);
            if (receiverPhoneInput != null)
                receiverPhoneInput.onValueChanged.AddListener(OnReceiverPhoneChanged);
            if (amountInput != null)
                amountInput.onValueChanged.AddListener(OnAmountChanged);

            if (nextButtonStep1 != null) nextButtonStep1.onClick.AddListener(OnStep1Next);
            if (confirmButtonStep2 != null) confirmButtonStep2.onClick.AddListener(OnConfirmSendClicked);
            if (backButtonStep2 != null) backButtonStep2.onClick.AddListener(OnBackToStep1);
            if (backButtonStep3 != null) backButtonStep3.onClick.AddListener(OnBackToStep2);
            if (retryApprovalButton != null) retryApprovalButton.onClick.AddListener(OnRetryApprovalClicked);

            // Bug 2: "Done" completes the SENDER's flow. It does not claim.
            if (doneButton != null) doneButton.onClick.AddListener(OnDoneClicked);
            if (copyClaimCodeButton != null) copyClaimCodeButton.onClick.AddListener(OnCopyClaimCodeClicked);

            if (inAppApprovalCancelButton != null) inAppApprovalCancelButton.onClick.AddListener(OnAbandonWaiting);
            if (guardianApprovalCancelButton != null) guardianApprovalCancelButton.onClick.AddListener(OnAbandonWaiting);

            if (openClaimPanelButton != null) openClaimPanelButton.onClick.AddListener(OnOpenClaimPanel);
            if (closeClaimPanelButton != null) closeClaimPanelButton.onClick.AddListener(OnCloseClaimPanel);
            if (claimSubmitButton != null) claimSubmitButton.onClick.AddListener(OnClaimSubmitClicked);
            if (collectPendingButton != null) collectPendingButton.onClick.AddListener(OnCollectPendingClicked);
            if (accountSelectionConfirmButton != null)
                accountSelectionConfirmButton.onClick.AddListener(OnAccountSelectionConfirmClicked);
            if (accountSelectionCancelButton != null)
                accountSelectionCancelButton.onClick.AddListener(OnAccountSelectionCancelClicked);
        }

        private void UnbindListeners()
        {
            if (!listenersBound) return;
            listenersBound = false;

            if (senderPhoneInput != null) senderPhoneInput.onValueChanged.RemoveListener(OnSenderPhoneChanged);
            if (receiverPhoneInput != null) receiverPhoneInput.onValueChanged.RemoveListener(OnReceiverPhoneChanged);
            if (amountInput != null) amountInput.onValueChanged.RemoveListener(OnAmountChanged);

            RemoveClick(nextButtonStep1, OnStep1Next);
            RemoveClick(confirmButtonStep2, OnConfirmSendClicked);
            RemoveClick(backButtonStep2, OnBackToStep1);
            RemoveClick(backButtonStep3, OnBackToStep2);
            RemoveClick(retryApprovalButton, OnRetryApprovalClicked);
            RemoveClick(doneButton, OnDoneClicked);
            RemoveClick(copyClaimCodeButton, OnCopyClaimCodeClicked);
            RemoveClick(inAppApprovalCancelButton, OnAbandonWaiting);
            RemoveClick(guardianApprovalCancelButton, OnAbandonWaiting);
            RemoveClick(openClaimPanelButton, OnOpenClaimPanel);
            RemoveClick(closeClaimPanelButton, OnCloseClaimPanel);
            RemoveClick(claimSubmitButton, OnClaimSubmitClicked);
            RemoveClick(collectPendingButton, OnCollectPendingClicked);
            RemoveClick(accountSelectionConfirmButton, OnAccountSelectionConfirmClicked);
            RemoveClick(accountSelectionCancelButton, OnAccountSelectionCancelClicked);

            if (toGameDropdown != null) toGameDropdown.onValueChanged.RemoveListener(OnDestinationChanged);
        }

        private static void RemoveClick(Button button, UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }

        // ------------------------------------------------------------------
        private async Task InitializeAsync()
        {
            config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            if (config == null)
            {
                ShowError("The Invo SDK is not configured. Run the Setup Wizard.");
                return;
            }

            SetActiveSafe(claimPanel, false);
            SetActiveSafe(accountSelectionPanel, false);
            ShowStep(1);
            await LoadAvailableGames();
            UpdateAmounts();
        }

        private void ShowStep(int step)
        {
            currentStep = step;
            SetActiveSafe(step1Panel, step == 1);
            SetActiveSafe(step2Panel, step == 2);
            SetActiveSafe(step3Panel, step == 3);
            SetActiveSafe(step4Panel, step == 4);

            // Waiting panels are alternatives to the approval screen, never additions.
            SetActiveSafe(inAppApprovalPanel, false);
            SetActiveSafe(guardianApprovalPanel, false);
            HideLegacySmsWidgets();

            SetStepAlpha(step);
        }

        /// <summary>Shows a waiting panel in place of the numbered step panels.</summary>
        private void ShowWaitingPanel(GameObject panel, int highlightStep)
        {
            currentStep = highlightStep;
            SetActiveSafe(step1Panel, false);
            SetActiveSafe(step2Panel, false);
            SetActiveSafe(step3Panel, false);
            SetActiveSafe(step4Panel, false);
            SetActiveSafe(inAppApprovalPanel, panel == inAppApprovalPanel);
            SetActiveSafe(guardianApprovalPanel, panel == guardianApprovalPanel);
            SetStepAlpha(highlightStep);
        }

        private void SetStepAlpha(int step)
        {
            if (step1Label != null) step1Label.alpha = step == 1 ? 1f : 0.4f;
            if (step2Label != null) step2Label.alpha = step == 2 ? 1f : 0.4f;
            if (step3Label != null) step3Label.alpha = step == 3 ? 1f : 0.4f;
            if (step4Label != null) step4Label.alpha = step == 4 ? 1f : 0.4f;
        }

        // ==================================================================
        // Destinations
        // ==================================================================

        private async Task LoadAvailableGames()
        {
            AvailableDestinationsResponse resp;
            try
            {
                resp = await APIManager.Instance.GetSendDestinationsAsync();
            }
            catch (InvoApiException ex)
            {
                ShowError(DescribeError(ex));
                return;
            }

            if (resp == null)
            {
                ShowError("Could not load the list of games you can send to.");
                return;
            }

            availableGames = resp.available_games ?? new List<AvailableGame>();
            sourceGameId = resp.source_game_id;

            SetText(fromGameNameText, config.gameName);
            SetText(fromCurrencyText, config.gameCurrencyName);
            SetText(currencyNameText, config.gameCurrencyName);

            _ = LoadSpriteFromUrl(config.gameIconUrl, fromGameIcon);
            _ = LoadSpriteFromUrl(config.gameCurrencyUrl, fromCurrencyIcon);

            BuildDestinationList();
        }

        private void BuildDestinationList()
        {
            destinations.Clear();

            // Bug 3: peer-to-peer was unreachable. /available-destinations never
            // lists the source game (can_transfer_to_game defaults to
            // allow_same_game=False), and the panel additionally filtered it out,
            // so a same-game send could not be selected - even though
            // /initiate-send accepts one. Offer it explicitly.
            string sameGameId = APIManager.Instance != null ? APIManager.Instance.GameId : sourceGameId;
            if (!string.IsNullOrEmpty(sameGameId))
            {
                destinations.Add(new SendDestination
                {
                    gameId = sameGameId,
                    gameName = $"{config.gameName} (a player in this game)",
                    currencyName = config.gameCurrencyName,
                    isSameGame = true,
                    // The destinations endpoint does not describe the source game,
                    // so no client-side limits exist for a peer-to-peer send; the
                    // server still enforces the currency's min/max.
                    minimumTransfer = null,
                    maximumTransfer = null
                });
            }

            foreach (var game in availableGames)
            {
                if (game == null) continue;
                // No client-side exclusion of the source game any more; only guard
                // against a duplicate should the backend ever start listing it.
                if (!string.IsNullOrEmpty(sameGameId) && game.game_id == sameGameId) continue;

                destinations.Add(new SendDestination
                {
                    gameId = game.game_id,
                    gameName = game.game_name,
                    currencyName = string.IsNullOrEmpty(game.currency_name)
                        ? config.gameCurrencyName
                        : game.currency_name,
                    minimumTransfer = ParseLimit(game.minimum_transfer),
                    maximumTransfer = ParseLimit(game.maximum_transfer),
                    isSameGame = false
                });
            }

            if (toGameDropdown != null)
            {
                toGameDropdown.onValueChanged.RemoveListener(OnDestinationChanged);
                toGameDropdown.ClearOptions();
                toGameDropdown.AddOptions(destinations.ConvertAll(d => d.gameName));
                toGameDropdown.onValueChanged.AddListener(OnDestinationChanged);
                toGameDropdown.value = 0;
                toGameDropdown.RefreshShownValue();
            }

            selectedDestination = destinations.Count > 0 ? destinations[0] : null;
            OnDestinationSelected();
        }

        private static decimal? ParseLimit(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return InvoFormat.TryParseAmount(raw, out var value) ? value : (decimal?)null;
        }

        private void OnDestinationChanged(int index)
        {
            selectedDestination = (index >= 0 && index < destinations.Count) ? destinations[index] : null;
            OnDestinationSelected();
        }

        private void OnDestinationSelected()
        {
            // Bug 15: amounts the RECEIVER ends up with are denominated in the
            // destination's currency, which is not necessarily ours.
            destinationCurrencyName = selectedDestination?.currencyName ?? config?.gameCurrencyName;
            SetText(amountLimitsText, DescribeLimits());
            UpdateAmounts();
            RefreshStep1Interactable();
        }

        private string DescribeLimits()
        {
            if (selectedDestination == null) return string.Empty;
            var min = selectedDestination.minimumTransfer;
            var max = selectedDestination.maximumTransfer;
            if (min == null && max == null) return string.Empty;
            if (min != null && max != null)
                return $"Send between {InvoFormat.Amount(min.Value)} and {InvoFormat.Amount(max.Value)} {config.gameCurrencyName}.";
            if (min != null)
                return $"Minimum send is {InvoFormat.Amount(min.Value)} {config.gameCurrencyName}.";
            return $"Maximum send is {InvoFormat.Amount(max.Value)} {config.gameCurrencyName}.";
        }

        // ==================================================================
        // Step 1 - input handling and validation
        // ==================================================================

        private void OnSenderPhoneChanged(string value)
        {
            SetText(senderPhoneErrorText, string.IsNullOrEmpty(value) ? string.Empty : InvoPhone.DescribeProblem(value));
            RefreshStep1Interactable();
        }

        private void OnReceiverPhoneChanged(string value)
        {
            SetText(receiverPhoneErrorText, string.IsNullOrEmpty(value) ? string.Empty : InvoPhone.DescribeProblem(value));
            RefreshStep1Interactable();
        }

        private void OnAmountChanged(string _)
        {
            UpdateAmounts();
            RefreshStep1Interactable();
        }

        private void RefreshStep1Interactable()
        {
            if (nextButtonStep1 != null) nextButtonStep1.interactable = ValidateStep1(false);
        }

        /// <summary>
        /// Bug 13: enforce the destination's minimum_transfer / maximum_transfer
        /// before the player can move on, instead of deserializing and ignoring them.
        /// </summary>
        private bool ValidateStep1(bool showErrors)
        {
            bool ok = true;

            string sender = InvoPhone.Normalize(senderPhoneInput != null ? senderPhoneInput.text : null);
            if (sender == null)
            {
                ok = false;
                if (showErrors)
                    SetText(senderPhoneErrorText, InvoPhone.DescribeProblem(senderPhoneInput != null ? senderPhoneInput.text : null));
            }
            else if (showErrors) SetText(senderPhoneErrorText, string.Empty);

            string receiver = InvoPhone.Normalize(receiverPhoneInput != null ? receiverPhoneInput.text : null);
            if (receiver == null)
            {
                ok = false;
                if (showErrors)
                    SetText(receiverPhoneErrorText, InvoPhone.DescribeProblem(receiverPhoneInput != null ? receiverPhoneInput.text : null));
            }
            else if (showErrors) SetText(receiverPhoneErrorText, string.Empty);

            string amountError = null;
            if (!InvoFormat.TryParseAmount(amountInput != null ? amountInput.text : null, out var amount) || amount <= 0m)
            {
                ok = false;
                amountError = "Enter an amount greater than zero.";
            }
            else if (selectedDestination == null)
            {
                ok = false;
                amountError = "Choose where the currency is going.";
            }
            else
            {
                if (selectedDestination.minimumTransfer != null && amount < selectedDestination.minimumTransfer.Value)
                {
                    ok = false;
                    amountError = $"The minimum send to {selectedDestination.gameName} is " +
                                  $"{InvoFormat.Amount(selectedDestination.minimumTransfer.Value)} {config.gameCurrencyName}.";
                }
                else if (selectedDestination.maximumTransfer != null && amount > selectedDestination.maximumTransfer.Value)
                {
                    ok = false;
                    amountError = $"The maximum send to {selectedDestination.gameName} is " +
                                  $"{InvoFormat.Amount(selectedDestination.maximumTransfer.Value)} {config.gameCurrencyName}.";
                }
            }

            if (showErrors || string.IsNullOrEmpty(amountError))
                SetText(amountErrorText, amountError ?? string.Empty);

            if (ok)
            {
                // Bug 4: one normalisation, reused verbatim at claim time. The
                // backend compares the digits exactly and 403s on a mismatch.
                normalizedSenderPhone = sender;
                normalizedReceiverPhone = receiver;
                enteredAmount = amount;
            }

            return ok;
        }

        // ------------------------------------------------------------------
        private void UpdateAmounts()
        {
            string sourceCurrency = config != null ? config.gameCurrencyName : string.Empty;
            string destCurrency = string.IsNullOrEmpty(destinationCurrencyName) ? sourceCurrency : destinationCurrencyName;

            // Bug 12: culture-safe parsing - "1,50" on a German keyboard is 1.50.
            if (!InvoFormat.TryParseAmount(amountInput != null ? amountInput.text : null, out enteredAmount) || enteredAmount <= 0m)
            {
                SetText(amountEnteredText, $"0.00 {sourceCurrency}");
                SetText(serviceFeeText, $"-0.00 {sourceCurrency}");
                SetText(receiverGetsText, $"0.00 {destCurrency}");
                SetText(feeEstimateNoticeText, "Estimated fee. The exact fee is confirmed when you send.");
                return;
            }

            serviceFee = enteredAmount * (decimal)serviceFeePercent;
            decimal net = enteredAmount - serviceFee;

            SetText(amountEnteredText, $"{InvoFormat.Amount(enteredAmount)} {sourceCurrency}");
            SetText(serviceFeeText, $"-{InvoFormat.Amount(serviceFee)} {sourceCurrency}");
            SetText(receiverGetsText, $"{InvoFormat.Amount(net)} {destCurrency}");
            SetText(feeEstimateNoticeText, "Estimated fee. The exact fee is confirmed when you send.");
        }

        // ------------------------------------------------------------------
        private void OnStep1Next()
        {
            if (!ValidateStep1(true)) return;

            serviceFee = enteredAmount * (decimal)serviceFeePercent;
            decimal receiverGets = enteredAmount - serviceFee;
            string sourceCurrency = config.gameCurrencyName;
            string destCurrency = string.IsNullOrEmpty(destinationCurrencyName) ? sourceCurrency : destinationCurrencyName;

            receiverEmailValue = receiverEmailInput != null ? receiverEmailInput.text?.Trim() : null;

            // Masked for display: the review screen is shown on a shared screen
            // often enough that the full number should not linger there.
            SetText(reviewFromPhone, InvoPhone.Mask(normalizedSenderPhone));
            SetText(reviewToPhone, InvoPhone.Mask(normalizedReceiverPhone));
            SetText(reviewFromGame, config.gameName);
            SetText(reviewToGame, selectedDestination?.gameName ?? "N/A");
            SetText(reviewSendAmount, $"{InvoFormat.Amount(enteredAmount)} {sourceCurrency}");
            SetText(reviewServiceFee, $"-{InvoFormat.Amount(serviceFee)} {sourceCurrency} (estimate)");
            SetText(reviewReceiverGets, $"{InvoFormat.Amount(receiverGets)} {destCurrency} (estimate)");

            ShowStep(2);
        }

        private void OnBackToStep1()
        {
            // Bug 8: a reservation already exists on the server once initiate-send
            // has succeeded, so edits made here can no longer change this send.
            // Navigation is still allowed; OnConfirmSendAsync refuses to mint a
            // second reservation.
            if (!string.IsNullOrEmpty(currentTransactionId))
                ShowError("This send is already reserved. Finish the verification step - " +
                          "changes made here will not apply to it.");
            else
                ClearStatus();
            ShowStep(1);
        }

        private void OnBackToStep2()
        {
            if (!string.IsNullOrEmpty(currentTransactionId))
                ShowError("Your send is reserved and waiting for verification. " +
                          "Confirming again will take you back to the code screen, not start a second send.");
            else
                ClearStatus();
            ShowStep(2);
        }

        // ==================================================================
        // Step 2 -> initiate-send
        // ==================================================================

        private void OnConfirmSendClicked() => _ = RunGuarded(OnConfirmSendAsync);

        private async Task OnConfirmSendAsync()
        {
            if (sendInFlight) return;

            // Bug 8: never mint a second reservation for the same intent.
            if (!string.IsNullOrEmpty(currentTransactionId))
            {
                if (waitingInFlight)
                {
                    ShowInfo("This send is still on hold. You can approve it as soon as the hold clears.");
                    return;
                }
                // Bug 8 still holds: never a second reservation. Confirm again re-shows the
                // approval for the SAME transaction.
                StartApproval();
                return;
            }

            if (!ValidateStep1(true))
            {
                ShowStep(1);
                return;
            }

            if (string.IsNullOrEmpty(currentClientRequestId))
                currentClientRequestId = APIManager.NewClientRequestId();

            sendInFlight = true;
            SetBusy(true);
            ClearStatus();
            try
            {
                var resp = await APIManager.Instance.InitiateSendAsync(
                    clientRequestId: currentClientRequestId,
                    senderName: APIManager.Instance.GetPlayerName(),
                    senderEmail: APIManager.Instance.GetPlayerEmail(),
                    senderPhone: normalizedSenderPhone,
                    receiverPhone: normalizedReceiverPhone,
                    receiverEmail: receiverEmailValue,
                    receivingGameId: selectedDestination?.gameId,
                    amount: InvoFormat.Amount(enteredAmount));

                HandleInitiateResponse(resp);
            }
            catch (InvoApiException ex)
            {
                // The same client_request_id is kept so a retry is idempotent.
                ShowError(DescribeError(ex));
            }
            catch (Exception ex)
            {
                ShowError("We could not start this send. Please try again.");
                Debug.LogError($"[InvoSDK] InitiateSend failed: {ex.GetType().Name}");
            }
            finally
            {
                sendInFlight = false;
                SetBusy(false);
            }
        }

        /// <summary>
        /// Bug 5: a 2xx is not a success. The response status string decides what
        /// happens next, and two of the possible outcomes are holds where the sender
        /// cannot approve yet.
        /// </summary>
        private void HandleInitiateResponse(InitiateSendResponse resp)
        {
            if (resp == null)
            {
                ShowError("The server did not confirm this send. Nothing was deducted.");
                return;
            }

            currentTransactionId = resp.transaction_id;
            ApplyAuthoritativeFees(resp);

            if (IsStatus(resp.status, InvoStatus.PendingGuardianApproval))
            {
                ShowGuardianWaiting(resp.guardian_approval);
                return;
            }

            if (IsStatus(resp.status, InvoStatus.PendingConfirmation))
            {
                ShowWaitingPanel(inAppApprovalPanel, 3);
                SetText(inAppApprovalStatusText,
                    "We are confirming the recipient's details. You can approve the send as soon as that finishes.");
                StartWaitThenApprove();
                return;
            }

            if (!IsStatus(resp.status, InvoStatus.Success))
            {
                // Unknown status: never claim success, never advance.
                ShowError("This send needs another step before it can continue. Please try again shortly.");
                return;
            }

            // verification_method is "in_app" or "sms". Both are approved on the phone now;
            // "sms" only means Invo also texted a legacy PIN, which this panel no longer asks for.
            StartApproval();
        }

        /// <summary>Bug 12: replace the local estimate with the server's fee breakdown.</summary>
        private void ApplyAuthoritativeFees(InitiateSendResponse resp)
        {
            var details = resp.send_details;
            var fees = details?.fees_preview;
            string sourceCurrency = string.IsNullOrEmpty(resp.currency_name) ? config.gameCurrencyName : resp.currency_name;
            string destCurrency = string.IsNullOrEmpty(destinationCurrencyName) ? sourceCurrency : destinationCurrencyName;

            if (details != null && !string.IsNullOrEmpty(details.amount_initiated))
                SetText(reviewSendAmount, $"{details.amount_initiated} {sourceCurrency}");

            if (fees != null)
            {
                authoritativeNetAmount = fees.net_amount;
                SetText(reviewServiceFee, $"-{fees.total_fee} {sourceCurrency}");
                SetText(reviewReceiverGets, $"{fees.net_amount} {destCurrency}");
                SetText(serviceFeeText, $"-{fees.total_fee} {sourceCurrency}");
                SetText(receiverGetsText, $"{fees.net_amount} {destCurrency}");
                SetText(feeEstimateNoticeText, "Confirmed fee.");
            }

            if (!string.IsNullOrEmpty(resp.new_balance))
                SetText(availableBalanceText, $"{resp.new_balance} {sourceCurrency}");
        }

        // ==================================================================
        // Step 3 - phone approval (QR / system browser). Replaces the SMS PIN.
        // ==================================================================

        private void OnRetryApprovalClicked() => StartApproval();

        private void StartApproval()
        {
            if (approvalInFlight || string.IsNullOrEmpty(currentTransactionId)) return;
            string transactionId = currentTransactionId;
            _ = RunGuarded(() => RunApprovalAsync(transactionId));
        }

        private async Task RunApprovalAsync(string transactionId)
        {
            if (approvalInFlight) return;
            approvalInFlight = true;
            ShowStep(3);
            SetRetryVisible(false);
            ClearStatus();
            SetText(verificationTargetText, Application.isMobilePlatform
                ? "Approve this send on the INVO page that opens. No code is texted to you."
                : "Scan the QR code with your phone and approve with your passkey. No code is texted to you.");
            try
            {
                var token = pollCts != null ? pollCts.Token : CancellationToken.None;
                var result = await InvoApprovalStep.ApproveAsync(
                    transactionId, InvoApprovalFlow.Send, approvalView,
                    id => APIManager.Instance.GetSendStatusAsync(id), ShowInfo, token);
                if (this == null || currentTransactionId != transactionId) return;
                ApplyApprovalResult(result);
            }
            finally
            {
                approvalInFlight = false;
            }
        }

        private void ApplyApprovalResult(InvoApprovalStepResult result)
        {
            if (result.Approved)
            {
                ShowSentScreen();
                return;
            }

            if (result.Cancelled)
            {
                if (!isActiveAndEnabled) return;
                ShowStep(3);
                SetRetryVisible(true);
                ShowInfo("Approval stopped. Nothing was sent. You can show the code again.");
                return;
            }

            ShowError(result.Message);
            if (result.CanRetry)
            {
                ShowStep(3);
                SetRetryVisible(true);
            }
            else
            {
                ResetSendState();
                ShowStep(1);
            }
        }

        private void SetRetryVisible(bool visible)
        {
            if (retryApprovalButton != null)
                SetActiveSafe(retryApprovalButton.gameObject, visible);
        }

        /// <summary>The SMS code boxes may still be in an older prefab; keep them out of sight.</summary>
        private void HideLegacySmsWidgets()
        {
            if (verificationCodeInput != null) SetActiveSafe(verificationCodeInput.gameObject, false);
            if (resendPinButton != null) SetActiveSafe(resendPinButton.gameObject, false);
            if (resendPinStatusText != null) SetActiveSafe(resendPinStatusText.gameObject, false);
            if (verificationExpiresText != null) SetActiveSafe(verificationExpiresText.gameObject, false);
        }

        // ==================================================================
        // Waiting states (guardian approval / recipient confirmation)
        // ==================================================================

        private void ShowGuardianWaiting(GuardianApproval approval)
        {
            ShowWaitingPanel(guardianApprovalPanel, 3);
            string expiry = approval != null ? DescribeExpiry(approval.expires_at, "Approval expires") : string.Empty;
            SetText(guardianApprovalStatusText,
                "This send is held until the guardian on file approves it. " +
                "Once they do, you approve it on your phone. " + expiry);
            StartWaitThenApprove();
        }

        private void StartWaitThenApprove()
        {
            if (waitingInFlight || string.IsNullOrEmpty(currentTransactionId)) return;
            string transactionId = currentTransactionId;
            _ = RunGuarded(() => WaitThenApproveAsync(transactionId));
        }

        private async Task WaitThenApproveAsync(string transactionId)
        {
            waitingInFlight = true;
            try
            {
                var token = pollCts != null ? pollCts.Token : CancellationToken.None;
                var waited = await InvoApprovalStep.WaitUntilApprovableAsync(
                    transactionId, id => APIManager.Instance.GetSendStatusAsync(id), token);
                if (this == null || currentTransactionId != transactionId) return;
                if (waited == null)
                {
                    await RunApprovalAsync(transactionId);
                    return;
                }
                ApplyApprovalResult(waited);
            }
            finally
            {
                waitingInFlight = false;
            }
        }

        private void OnAbandonWaiting()
        {
            // Leaves the server-side reservation alone (only the sender's own
            // approval can release it) but returns the player to a usable screen.
            CancelPolling();
            pollCts = new CancellationTokenSource();
            ShowStep(3);
            SetRetryVisible(true);
            ShowError("You can come back to this send once it has been approved.");
        }

        // ==================================================================
        // Step 4 - sent. The receiver collects it; the sender never claims.
        // ==================================================================

        private void ShowSentScreen()
        {
            string destCurrency = string.IsNullOrEmpty(destinationCurrencyName)
                ? config.gameCurrencyName
                : destinationCurrencyName;
            string netText = string.IsNullOrEmpty(authoritativeNetAmount)
                ? InvoFormat.Amount(enteredAmount - serviceFee)
                : authoritativeNetAmount;

            bool sameGame = selectedDestination != null && selectedDestination.isSameGame;
            string whereCollected = sameGame
                ? "They collect it from this game's Collect screen, on their own device."
                : "They collect it inside that game, on their own device.";

            SetText(confirmationTitle, "Sent");
            SetText(confirmationSubtitle,
                $"{netText} {destCurrency} is waiting for {InvoPhone.Mask(normalizedReceiverPhone)} in " +
                $"{selectedDestination?.gameName ?? "the destination game"}. " + whereCollected);

            // A send's claim code goes to the RECEIVER, never back to the sender, so the
            // claim-code row only shows when a code is actually known.
            bool hasCode = !string.IsNullOrEmpty(currentClaimCode);
            if (claimCodeLabel != null) SetActiveSafe(claimCodeLabel.gameObject, hasCode);
            if (claimCodeExpiryLabel != null) SetActiveSafe(claimCodeExpiryLabel.gameObject, hasCode);
            if (copyClaimCodeButton != null) SetActiveSafe(copyClaimCodeButton.gameObject, hasCode);
            SetText(claimCodeLabel, hasCode ? currentClaimCode : string.Empty);
            SetText(claimCodeExpiryLabel, DescribeExpiry(currentClaimCodeExpiresAt, "Claim code expires"));

            ShowStep(4);
            APIManager.Instance.StartBalancePolling();
        }

        private void OnCopyClaimCodeClicked()
        {
            if (string.IsNullOrEmpty(currentClaimCode)) return;
            GUIUtility.systemCopyBuffer = currentClaimCode;
            ShowInfo("Claim code copied.");
        }

        private void OnDoneClicked()
        {
            // Bug 2: this used to call claim-currency with the SENDER as the
            // recipient. The sender's device cannot claim - the claim must be
            // made by the receiver, in the receiving game, with that game's
            // secret key. Done simply ends the sender's flow.
            ResetPanel();
        }

        #region ClaimCurrencyPanel
        // ==================================================================
        // Claim Currency Panel  (RECEIVER side - a separate flow, not step 4)
        //
        // LIMITATION: /currency-sends/claim-currency resolves the destination
        // tenant from the X-Game-Secret-Key header, and looks the claim code up
        // by (claim_code, to_game_id). A build of THIS plugin carries exactly one
        // game's key, so it can only claim sends addressed to THIS game
        // (peer-to-peer, or inbound cross-game sends where this game is the
        // destination). Trying to claim a send addressed to another game returns
        // 404 "Invalid claim code or currency send not intended for this game."
        // Documented in the README.
        // ==================================================================

        private void OnOpenClaimPanel()
        {
            SetActiveSafe(claimPanel, true);
            SetActiveSafe(accountSelectionPanel, false);
            SetText(claimStatusText, string.Empty);
            claimCompleted = false;
            if (claimSubmitButton != null) claimSubmitButton.interactable = true;
            _ = RunGuarded(RefreshCollectableAsync);
        }

        /// <summary>
        /// Looks for a send addressed to the ACTIVE player in this game. The pending list is
        /// scoped by the player token, so nothing has to be typed in.
        /// </summary>
        private async Task RefreshCollectableAsync()
        {
            collectable = null;
            if (collectPendingButton == null) return;
            SetActiveSafe(collectPendingButton.gameObject, false);

            PendingActionsResponse resp;
            try
            {
                resp = await APIManager.Instance.GetPendingActionsAsync();
            }
            catch (InvoApiException ex)
            {
                // A player with no account in this game yet has no token: the claim code is the way in.
                SetText(collectPendingText, string.Equals(ex.ErrorCode, "player_not_found", StringComparison.OrdinalIgnoreCase)
                    ? "Use the claim code you received to collect."
                    : string.Empty);
                return;
            }
            if (this == null) return;

            var item = resp?.pending?.Find(p => p.kind == PendingAction.KindReceivingConfirm &&
                                                p.flow == InvoApprovalFlow.Send && !p.held);
            if (item == null)
            {
                SetText(collectPendingText, "Nothing is waiting to collect right now.");
                return;
            }

            collectable = item;
            SetText(collectPendingText,
                $"{item.amount} {item.currency} from {item.counterparty_game} is waiting for you.");
            SetActiveSafe(collectPendingButton.gameObject, true);
        }

        private void OnCollectPendingClicked() => _ = RunGuarded(CollectPendingAsync);

        /// <summary>The receiver's phone approval (send_receipt) - the same QR flow the sender used.</summary>
        private async Task CollectPendingAsync()
        {
            var item = collectable;
            if (item == null || claimInFlight) return;

            claimInFlight = true;
            SetBusy(true);
            SetText(claimStatusText, string.Empty);
            try
            {
                var token = pollCts != null ? pollCts.Token : CancellationToken.None;
                InvoDeviceApprovalResult run;
                try
                {
                    run = await InvoDeviceApproval.RunAsync(item.transfer_id, InvoApprovalFlow.SendReceipt, collectApprovalView, token);
                }
                catch (InvoApiException ex)
                {
                    // The failure may have come after the credit committed; read before reporting.
                    if (await IsSendCompletedAsync(item.transfer_id))
                    {
                        ShowCollected(null, item);
                        return;
                    }
                    SetText(claimStatusText, DescribeCollectError(ex));
                    return;
                }

                switch (run.Outcome)
                {
                    case InvoDeviceApprovalOutcome.Settled:
                    case InvoDeviceApprovalOutcome.AlreadySettled:
                        ShowCollected(run.Settlement != null ? run.Settlement.amount_received : null, item);
                        break;
                    case InvoDeviceApprovalOutcome.Held:
                        SetText(claimStatusText, "Approved. One more check is running; it lands in your balance when it clears.");
                        break;
                    case InvoDeviceApprovalOutcome.Denied:
                        SetText(claimStatusText, "Collection was declined. Nothing changed; you can try again.");
                        break;
                    case InvoDeviceApprovalOutcome.Expired:
                        SetText(claimStatusText, "The QR code expired. Tap Collect to show a new one.");
                        break;
                    case InvoDeviceApprovalOutcome.NotPending:
                        SetText(claimStatusText, "This is no longer waiting - it may have been collected or expired.");
                        await RefreshCollectableAsync();
                        break;
                }
            }
            finally
            {
                claimInFlight = false;
                SetBusy(false);
            }
        }

        private void ShowCollected(string amountReceived, PendingAction item)
        {
            string amount = string.IsNullOrEmpty(amountReceived) ? item.amount : amountReceived;
            SetText(claimStatusText, $"Collected {amount} {item.currency}.");
            SetText(collectPendingText, string.Empty);
            collectable = null;
            claimCompleted = true;
            if (collectPendingButton != null) SetActiveSafe(collectPendingButton.gameObject, false);
            APIManager.Instance.StartBalancePolling();
        }

        private static async Task<bool> IsSendCompletedAsync(string transactionId)
        {
            try
            {
                var status = await APIManager.Instance.GetSendStatusAsync(transactionId);
                return status != null && string.Equals(status.verification_state, "completed", StringComparison.OrdinalIgnoreCase);
            }
            catch (InvoApiException)
            {
                return false;
            }
        }

        private string DescribeCollectError(InvoApiException ex)
        {
            switch (ex.ErrorCode)
            {
                case "receiver_not_enrolled_use_claim_code":
                    return "Collect this one with the claim code you received.";
                case "not_intended_receiver":
                    return "This send is addressed to a different player or game.";
                case "RECIPIENT_IDENTITY_DECLINED":
                    return "The recipient check was declined, so nothing was collected.";
            }
            return DescribeError(ex);
        }

        private void OnCloseClaimPanel()
        {
            SetActiveSafe(claimPanel, false);
            SetActiveSafe(accountSelectionPanel, false);
        }

        private void OnClaimSubmitClicked() => _ = RunGuarded(() => OnClaimCurrencyAsync(null));

        /// <summary>
        /// Bug 1: the request body must carry claim_code, receiver_player_name,
        /// receiver_player_email and receiver_player_phone. The old call sent
        /// target_* names and no email at all, so every claim 400'd before the
        /// backend even looked the code up.
        /// </summary>
        private async Task OnClaimCurrencyAsync(int? receiverPlayerId)
        {
            if (claimInFlight) return;

            string code = claimCodeInput != null ? claimCodeInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(code))
            {
                SetText(claimStatusText, "Enter the claim code you received.");
                return;
            }

            string name = claimNameInput != null ? claimNameInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(name))
            {
                SetText(claimStatusText, "Enter the name on your account.");
                return;
            }

            string email = claimEmailInput != null ? claimEmailInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                SetText(claimStatusText, "Enter the email address on your account.");
                return;
            }

            // Bug 4: identical normalisation to the initiate call - the backend
            // compares the digits exactly and refuses a mismatch.
            string phone = InvoPhone.Normalize(claimPhoneInput != null ? claimPhoneInput.text : null);
            if (phone == null)
            {
                SetText(claimStatusText, InvoPhone.DescribeProblem(claimPhoneInput != null ? claimPhoneInput.text : null));
                return;
            }

            claimInFlight = true;
            SetBusy(true);
            if (claimSubmitButton != null) claimSubmitButton.interactable = false;
            SetText(claimStatusText, "Claiming...");
            try
            {
                var resp = await APIManager.Instance.ClaimCurrencyAsync(
                    claimCode: code,
                    receiverName: name,
                    receiverEmail: email,
                    receiverPhone: phone,
                    receiverPlayerId: receiverPlayerId);

                HandleClaimResponse(resp);
            }
            catch (InvoApiException ex)
            {
                // Bug 11: the claim code is preserved so a transient failure or a
                // rate-limit lockout does not destroy the only copy of it.
                SetText(claimStatusText, ex.StatusCode == 404
                    ? "That claim code is not valid for this game. Claim it in the game it was sent to."
                    : DescribeError(ex));
            }
            catch (Exception ex)
            {
                SetText(claimStatusText, "We could not claim that code. Your code is still valid - please try again.");
                Debug.LogError($"[InvoSDK] ClaimCurrency failed: {ex.GetType().Name}");
            }
            finally
            {
                claimInFlight = false;
                SetBusy(false);
                // Bug 11: the submit button comes back on after a failure so the
                // player can retry with the same, still-valid claim code.
                if (claimSubmitButton != null) claimSubmitButton.interactable = !claimCompleted;
            }
        }

        private void HandleClaimResponse(ClaimCurrencyResponse resp)
        {
            if (resp == null)
            {
                SetText(claimStatusText, "The server did not confirm that claim. Your code is still valid.");
                return;
            }

            // Bug 5: HTTP 200 with needs_account_selection means the session was
            // rolled back and NOTHING was credited. Showing "claimed" here was the
            // worst false-success in the old panel.
            if (IsStatus(resp.status, InvoStatus.NeedsAccountSelection))
            {
                ShowAccountPicker(resp.candidates);
                return;
            }

            if (IsStatus(resp.status, InvoStatus.PendingConfirmation))
            {
                SetActiveSafe(accountSelectionPanel, false);
                SetText(claimStatusText,
                    "We texted the number on file to confirm this claim. " +
                    "Once it is confirmed, submit the same code again to collect. Keep your code.");
                return;
            }

            if (IsStatus(resp.status, InvoStatus.PendingGuardianApproval))
            {
                SetActiveSafe(accountSelectionPanel, false);
                SetText(claimStatusText,
                    "This claim is held until the guardian on file approves it. Keep your code and try again shortly.");
                return;
            }

            if (!IsStatus(resp.status, InvoStatus.Success))
            {
                SetText(claimStatusText, "That claim did not go through. Your code is still valid - please try again.");
                return;
            }

            SetActiveSafe(accountSelectionPanel, false);
            claimCandidates = new List<AccountCandidate>();
            string currency = string.IsNullOrEmpty(resp.currency_name) ? config?.gameCurrencyName : resp.currency_name;
            string balance = string.IsNullOrEmpty(resp.new_balance) ? null : $" Your balance is now {resp.new_balance} {currency}.";
            SetText(claimStatusText, $"Claimed.{balance}");
            claimCompleted = true;
        }

        private void ShowAccountPicker(List<AccountCandidate> candidates)
        {
            claimCandidates = candidates ?? new List<AccountCandidate>();
            if (claimCandidates.Count == 0)
            {
                SetText(claimStatusText,
                    "More than one account here shares that phone number. Check the email address and try again.");
                return;
            }

            SetActiveSafe(accountSelectionPanel, true);
            if (accountSelectionDropdown != null)
            {
                accountSelectionDropdown.ClearOptions();
                accountSelectionDropdown.AddOptions(
                    claimCandidates.ConvertAll(c => string.IsNullOrEmpty(c.email_hint) ? $"Account {c.player_id}" : c.email_hint));
                accountSelectionDropdown.value = 0;
                accountSelectionDropdown.RefreshShownValue();
            }
            SetText(claimStatusText,
                "More than one account here uses that phone number. Nothing was claimed yet - pick your account to continue.");
        }

        private void OnAccountSelectionConfirmClicked()
        {
            if (claimCandidates == null || claimCandidates.Count == 0) return;
            int index = accountSelectionDropdown != null ? accountSelectionDropdown.value : 0;
            if (index < 0 || index >= claimCandidates.Count) return;
            int playerId = claimCandidates[index].player_id;
            SetActiveSafe(accountSelectionPanel, false);
            _ = RunGuarded(() => OnClaimCurrencyAsync(playerId));
        }

        private void OnAccountSelectionCancelClicked()
        {
            SetActiveSafe(accountSelectionPanel, false);
            SetText(claimStatusText, "Nothing was claimed. Your code is still valid.");
        }

        #endregion

        // ==================================================================
        // Reset / helpers
        // ==================================================================

        private void ResetSendState()
        {
            currentTransactionId = null;
            currentClaimCode = null;
            currentClaimCodeExpiresAt = null;
            currentClientRequestId = null;
            authoritativeNetAmount = null;
            CancelPolling();
            pollCts = new CancellationTokenSource();
        }

        private void ResetPanel()
        {
            if (senderPhoneInput != null) senderPhoneInput.text = "";
            if (receiverPhoneInput != null) receiverPhoneInput.text = "";
            if (receiverEmailInput != null) receiverEmailInput.text = "";
            if (amountInput != null) amountInput.text = "";
            SetText(amountEnteredText, "");
            SetText(serviceFeeText, "");
            SetText(receiverGetsText, "");
            SetText(claimCodeLabel, "");
            SetText(claimCodeExpiryLabel, "");
            SetText(senderPhoneErrorText, "");
            SetText(receiverPhoneErrorText, "");
            SetText(amountErrorText, "");
            ClearStatus();
            normalizedSenderPhone = normalizedReceiverPhone = null;
            receiverEmailValue = null;
            ResetSendState();
            ShowStep(1);
        }

        /// <summary>
        /// Bug 9: every API call runs through here, so a failure surfaces as a
        /// message instead of an unobserved Task exception that shows nothing.
        /// </summary>
        private async Task RunGuarded(Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (InvoApiException ex)
            {
                ShowError(DescribeError(ex));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ShowError("Something went wrong. Please try again.");
                Debug.LogError($"[InvoSDK] SenderCurrencyPanel: {ex.GetType().Name}");
            }
        }

        /// <summary>
        /// Turns an API failure into something a player can act on.
        /// NEVER renders ex.Body - the raw envelope can contain another player's
        /// phone number or email address.
        /// </summary>
        private string DescribeError(InvoApiException ex)
        {
            if (ex == null) return "Something went wrong. Please try again.";

            if (ex.IsNetworkError)
                return "No connection. Check your network and try again.";

            if (ex.IsRateLimited)
            {
                int wait = ex.RetryAfterSeconds ?? 60;
                return $"Too many attempts. Try again in {wait} second{(wait == 1 ? "" : "s")}.";
            }

            if (ex.IsDuplicate)
            {
                if (string.Equals(ex.ErrorCode, "PHONE_SHARE_APPROVAL_REQUIRED", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: the phone-share approval recovery endpoint is not
                    // implemented in this plugin. Documented in the README.
                    return "This phone number is shared with another account, so it needs approval " +
                           "before currency can move. The account holder must approve the number in " +
                           "the Invo app, then try again.";
                }
                return "That request was already processed. Nothing was charged twice.";
            }

            if (string.Equals(ex.ErrorCode, "TENANT_NOT_MIGRATED", StringComparison.OrdinalIgnoreCase))
                return "Phone approval is not enabled for this game yet. Contact Invo to enable it.";
            if (string.Equals(ex.ErrorCode, APIManager.GameServerRequiredCode, StringComparison.Ordinal))
                return "This build is not connected to its game server.";

            switch (ex.StatusCode)
            {
                case 400: return "Some details were not accepted. Check the phone numbers and amount.";
                case 401:
                case 403: return "This game is not allowed to complete that request.";
                case 404: return "We could not find that send. It may have expired or been claimed already.";
                case 410: return "That approval is no longer valid. Start the send again.";
                case 503: return "Invo is briefly unavailable. Please try again in a moment.";
            }

            if (ex.StatusCode >= 500)
                return string.IsNullOrEmpty(ex.ErrorId)
                    ? "Invo had a problem completing that. Please try again."
                    : $"Invo had a problem completing that. Please try again. (Ref {ex.ErrorId})";

            return "That request could not be completed. Please try again.";
        }

        private static bool IsStatus(string value, string expected) =>
            !string.IsNullOrEmpty(value) && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

        private static string DescribeExpiry(string isoTimestamp, string prefix)
        {
            if (string.IsNullOrEmpty(isoTimestamp) || isoTimestamp == "N/A") return string.Empty;
            return DateTimeOffset.TryParse(isoTimestamp, null,
                       System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                       out var parsed)
                ? $"{prefix} {parsed.ToLocalTime():g}."
                : string.Empty;
        }

        private void ShowError(string message) => SetText(statusText, message);

        private void ShowInfo(string message) => SetText(statusText, message);

        private void ClearStatus() => SetText(statusText, string.Empty);

        private void SetBusy(bool busy)
        {
            SetActiveSafe(busyIndicator, busy);
            if (confirmButtonStep2 != null) confirmButtonStep2.interactable = !busy;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private async Task LoadSpriteFromUrl(string url, Image target)
        {
            if (string.IsNullOrEmpty(url) || target == null) return;
            using var req = UnityWebRequestTexture.GetTexture(url);
            await req.SendWebRequest();
            if (this == null || target == null) return;
            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                target.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
            }
        }
    }
}
