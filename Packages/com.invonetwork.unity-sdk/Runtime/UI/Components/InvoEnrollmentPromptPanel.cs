using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// uGUI match-code prompt ("Set up INVO on this phone? Code 1234" with Yes / No).
    ///
    /// Put this on an object that stays active (the SDK window's canvas root does) and point
    /// <see cref="dialog"/> at the dialog it shows and hides. On Awake it registers itself as
    /// <see cref="InvoHostedApproval.PromptView"/>, replacing the IMGUI
    /// <see cref="InvoEnrollmentPromptOverlay"/>, and unregisters on destroy.
    /// </summary>
    public class InvoEnrollmentPromptPanel : MonoBehaviour, IInvoEnrollmentPromptView
    {
        [Tooltip("The dialog shown while a prompt is up. Starts hidden.")]
        public GameObject dialog;
        public TMP_Text messageText;
        public Button primaryButton;
        public TMP_Text primaryLabel;
        public Button secondaryButton;
        public TMP_Text secondaryLabel;

        private Action<InvoEnrollmentDecision> pendingDecision;
        private Action pendingStop;

        private void Awake()
        {
            if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimary);
            if (secondaryButton != null) secondaryButton.onClick.AddListener(OnSecondary);
            if (dialog != null) dialog.SetActive(false);
            InvoHostedApproval.PromptView = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(InvoHostedApproval.PromptView, this))
                InvoHostedApproval.PromptView = null;
        }

        public void ShowPrompt(string message, string yesLabel, string noLabel, Action<InvoEnrollmentDecision> onDecision)
        {
            pendingDecision = onDecision;
            pendingStop = null;
            Show(message, yesLabel, noLabel);
        }

        public void ShowFinishing(string message, string stopLabel, Action onStop)
        {
            pendingDecision = null;
            pendingStop = onStop;
            Show(message, null, stopLabel);
        }

        public void Hide()
        {
            pendingDecision = null;
            pendingStop = null;
            if (dialog != null) dialog.SetActive(false);
        }

        private void Show(string message, string primary, string secondary)
        {
            if (messageText != null) messageText.text = message ?? string.Empty;
            if (primaryButton != null) primaryButton.gameObject.SetActive(!string.IsNullOrEmpty(primary));
            if (primaryLabel != null) primaryLabel.text = primary ?? string.Empty;
            if (secondaryLabel != null) secondaryLabel.text = secondary ?? string.Empty;
            if (dialog != null)
            {
                dialog.SetActive(true);
                dialog.transform.SetAsLastSibling();
            }
        }

        private void OnPrimary()
        {
            // Yes. Fire once, then hide, as the interface requires.
            Action<InvoEnrollmentDecision> decision = pendingDecision;
            Hide();
            decision?.Invoke(InvoEnrollmentDecision.Approve);
        }

        private void OnSecondary()
        {
            // No on the prompt; "Stop it (wrong code)" on the finishing state.
            Action<InvoEnrollmentDecision> decision = pendingDecision;
            Action stop = pendingStop;
            Hide();
            if (decision != null)
                decision(InvoEnrollmentDecision.Deny);
            else
                stop?.Invoke();
        }
    }
}
