using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK
{
    /// <summary>
    /// uGUI implementation of <see cref="IInvoDeviceApprovalView"/>. Add it to the panel that used to
    /// hold the SMS code boxes, wire the fields, and assign it to the panel's <c>approvalView</c>.
    /// Every field is optional; unassigned ones are skipped.
    /// </summary>
    public class InvoDeviceApprovalPanelView : MonoBehaviour, IInvoDeviceApprovalView
    {
        [Tooltip("Root shown while approving. Defaults to this GameObject.")]
        public GameObject root;
        [Tooltip("Square RawImage for the QR. Keep its aspect 1:1.")]
        public RawImage qrImage;
        public TMP_Text headlineText;
        [Tooltip("\"Or go to <uri> and enter <code>\"")]
        public TMP_Text manualEntryText;
        public TMP_Text statusText;
        public Button cancelButton;
        [Tooltip("Phones only: reopens the approval page.")]
        public Button reopenButton;

        private Action cancelAction;
        private Action reopenAction;

        private void Awake()
        {
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
            if (reopenButton != null) reopenButton.onClick.AddListener(OnReopen);
        }

        private void OnDestroy()
        {
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancel);
            if (reopenButton != null) reopenButton.onClick.RemoveListener(OnReopen);
        }

        public void ShowQr(Texture2D qr, string userCode, string verificationUri, string headline, Action onCancel)
        {
            Root.SetActive(true);
            cancelAction = onCancel;
            reopenAction = null;
            if (qrImage != null)
            {
                qrImage.texture = qr;
                qrImage.gameObject.SetActive(qr != null);
            }
            if (reopenButton != null) reopenButton.gameObject.SetActive(false);
            Set(headlineText, headline);
            Set(manualEntryText, InvoDeviceApproval.ManualEntryText(verificationUri, userCode));
            Set(statusText, string.Empty);
        }

        public void ShowBrowserWaiting(string userCode, string headline, Action onReopen, Action onCancel)
        {
            Root.SetActive(true);
            cancelAction = onCancel;
            reopenAction = onReopen;
            if (qrImage != null)
            {
                qrImage.texture = null;
                qrImage.gameObject.SetActive(false);
            }
            if (reopenButton != null) reopenButton.gameObject.SetActive(onReopen != null);
            Set(headlineText, headline);
            Set(manualEntryText, string.IsNullOrEmpty(userCode) ? string.Empty : "Code: " + userCode);
            Set(statusText, string.Empty);
        }

        public void ShowStatus(string message)
        {
            Set(statusText, message);
        }

        public void Hide()
        {
            if (qrImage != null) qrImage.texture = null;
            cancelAction = null;
            reopenAction = null;
            Root.SetActive(false);
        }

        private GameObject Root => root != null ? root : gameObject;

        private void OnCancel()
        {
            Action cb = cancelAction;
            if (cb != null) cb();
        }

        private void OnReopen()
        {
            Action cb = reopenAction;
            if (cb != null) cb();
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }
    }
}
