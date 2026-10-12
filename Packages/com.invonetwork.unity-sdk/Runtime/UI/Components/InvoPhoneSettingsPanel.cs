using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// Shows the player's phone (masked) and lets them change it. An unsecured account switches at
    /// once; a secured one gets <c>409 PHONE_CHANGE_REQUIRES_VERIFICATION</c>, so the panel creates a
    /// change request and INVO emails the player a link to confirm on INVO's page (no text message).
    /// The panel polls the request and switches the SDK to the new number when it is confirmed.
    /// Your game should also save the new number in its own account system: listen to
    /// <see cref="APIManager.PlayerPhoneChanged"/>.
    /// </summary>
    public class InvoPhoneSettingsPanel : MonoBehaviour
    {
        [Header("Edit")]
        public GameObject editSection;
        public TMP_Text currentPhoneText;
        public TMP_InputField newPhoneInput;
        public TMP_Text errorText;
        public Button updateButton;

        [Header("Waiting for the email confirmation")]
        public GameObject pendingSection;
        public TMP_Text pendingText;
        public TMP_Text pendingStatusText;
        public Button resendButton;
        public Button cancelButton;

        [Header("Done")]
        public GameObject doneSection;
        public TMP_Text doneText;
        public Button doneButton;

        [Min(3f)] public float pollSeconds = 5f;

        private string requestId;
        private string requestedPhone;
        private bool busy;
        private CancellationTokenSource cts;

        private void Awake()
        {
            if (updateButton != null) updateButton.onClick.AddListener(() => _ = UpdateAsync());
            if (resendButton != null) resendButton.onClick.AddListener(() => _ = StartRequestAsync(requestedPhone));
            if (cancelButton != null) cancelButton.onClick.AddListener(CancelWaiting);
            if (doneButton != null) doneButton.onClick.AddListener(() => Show(editSection));
            if (newPhoneInput != null) newPhoneInput.onValueChanged.AddListener(_ => Validate(false));
        }

        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            APIManager.PlayerPhoneChanged += OnPhoneChanged;
            RefreshCurrent();
            if (string.IsNullOrEmpty(requestId))
                Show(editSection);
            else
                _ = PollAsync(cts.Token);
        }

        private void OnDisable()
        {
            APIManager.PlayerPhoneChanged -= OnPhoneChanged;
            try { cts?.Cancel(); } catch (ObjectDisposedException) { }
            cts?.Dispose();
            cts = null;
        }

        private void OnPhoneChanged(string _) => RefreshCurrent();

        private void RefreshCurrent()
        {
            string phone = APIManager.Instance != null ? APIManager.Instance.GetPlayerPhone() : null;
            if (currentPhoneText != null)
                currentPhoneText.text = string.IsNullOrEmpty(phone) ? "Not set" : InvoPhone.MaskShort(phone);
            if (newPhoneInput != null) newPhoneInput.text = string.Empty;
            SetText(errorText, string.Empty);
            Validate(false);
        }

        private bool Validate(bool showErrors)
        {
            string raw = newPhoneInput != null ? newPhoneInput.text : null;
            bool ok = InvoPhone.Normalize(raw) != null;
            if (updateButton != null) updateButton.interactable = ok && !busy;
            if (showErrors && !ok) SetText(errorText, InvoPhone.DescribeProblem(raw));
            return ok;
        }

        private async Task UpdateAsync()
        {
            if (busy || !Validate(true) || APIManager.Instance == null)
                return;
            string phone = InvoPhone.Normalize(newPhoneInput.text);
            busy = true;
            SetText(errorText, string.Empty);
            try
            {
                PhoneChangeResponse resp = await APIManager.Instance.ChangePlayerPhoneAsync(phone);
                if (this == null) return;
                ShowDone(resp != null && resp.status == "unchanged"
                    ? "That's already your number."
                    : $"Done. Your sends are now approved on {InvoPhone.MaskShort(phone)}.");
            }
            catch (InvoApiException ex) when (ex.ErrorCode == "PHONE_CHANGE_REQUIRES_VERIFICATION")
            {
                await StartRequestAsync(phone);
            }
            catch (InvoApiException ex)
            {
                if (this != null) SetText(errorText, DescribeError(ex));
            }
            finally
            {
                busy = false;
                if (this != null) Validate(false);
            }
        }

        private async Task StartRequestAsync(string phone)
        {
            if (string.IsNullOrEmpty(phone) || APIManager.Instance == null)
                return;
            try
            {
                PhoneChangeRequest request = await APIManager.Instance.CreatePhoneChangeRequestAsync(phone);
                if (this == null || request == null) return;
                requestId = request.change_request_id;
                requestedPhone = phone;
                SetText(pendingText,
                    $"We sent a link to your email. Open it and confirm on INVO's page to switch to {InvoPhone.MaskShort(phone)}.");
                SetText(pendingStatusText, DescribeStatus(request));
                Show(pendingSection);
                if (cts != null) _ = PollAsync(cts.Token);
            }
            catch (InvoApiException ex)
            {
                if (this == null) return;
                Show(editSection);
                SetText(errorText, DescribeError(ex));
            }
        }

        private async Task PollAsync(CancellationToken token)
        {
            string id = requestId;
            while (!token.IsCancellationRequested && id == requestId && !string.IsNullOrEmpty(id))
            {
                try
                {
                    await InvoDelay.Seconds(pollSeconds, token);
                    PhoneChangeRequest request = await APIManager.Instance.GetPhoneChangeRequestAsync(id);
                    if (this == null || token.IsCancellationRequested || id != requestId || request == null) return;
                    SetText(pendingStatusText, DescribeStatus(request));
                    if (request.IsPending)
                        continue;
                    requestId = null;
                    if (request.IsConfirmed)
                    {
                        APIManager.Instance.ApplyConfirmedPhone(requestedPhone);
                        ShowDone($"Confirmed. Your sends are now approved on {InvoPhone.MaskShort(requestedPhone)}.");
                    }
                    else
                    {
                        Show(editSection);
                        SetText(errorText, DescribeStatus(request));
                    }
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (InvoApiException)
                {
                    // Keep waiting; a dropped poll changes nothing on INVO's side.
                }
            }
        }

        private void CancelWaiting()
        {
            // INVO has no cancel endpoint; an unconfirmed request simply expires.
            requestId = null;
            Show(editSection);
            SetText(errorText, "Change not made. The emailed link stops working when it expires.");
        }

        private void ShowDone(string message)
        {
            SetText(doneText, message);
            Show(doneSection != null ? doneSection : editSection);
        }

        private void Show(GameObject section)
        {
            if (editSection != null) editSection.SetActive(section == editSection);
            if (pendingSection != null) pendingSection.SetActive(section == pendingSection);
            if (doneSection != null) doneSection.SetActive(section == doneSection);
        }

        private static string DescribeStatus(PhoneChangeRequest request)
        {
            switch (request.status)
            {
                case "pending":
                case "pending_confirmation":
                    string expiry = InvoCollectPanel.DescribeExpiry(request.expires_at);
                    return string.IsNullOrEmpty(expiry) ? "Waiting for you to confirm" : $"Waiting for you to confirm · {expiry}";
                case "confirmed": return "Confirmed";
                case "expired": return "The link expired before it was confirmed. Nothing changed.";
                case "cancelled": return "A newer request replaced this one.";
                case "refused":
                case "denied": return "The change was declined. Your number is unchanged.";
                default: return request.status;
            }
        }

        private static string DescribeError(InvoApiException ex)
        {
            switch (ex.ErrorCode)
            {
                case "INVALID_PHONE":
                case "INVALID_INPUT": return "That doesn't look like a valid phone number. Include the country code.";
                case "PHONE_SHARE_APPROVAL_REQUIRED": return "That number belongs to another INVO account.";
                case "PENDING_TRANSFERS_TO_OLD_PHONE": return "Collect what's waiting for your current number first, then change it.";
                case "PENDING_TRANSFERS_TO_NEW_PHONE": return "Currency is waiting to be collected on that number. Collect it first.";
                case "PHONE_UPDATE_UNAVAILABLE": return "Changing numbers is unavailable right now. Try again later.";
                case "player_not_found": return "Make a send or transfer first; then you can change your number.";
                case APIManager.GameServerRequiredCode: return "Changing your number needs the game server. Check Tools > Invo SDK > Settings.";
            }
            return "We could not change your number. Try again in a moment.";
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }
    }
}
