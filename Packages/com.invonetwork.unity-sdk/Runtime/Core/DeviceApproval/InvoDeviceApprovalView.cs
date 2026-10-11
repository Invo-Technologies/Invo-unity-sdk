using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK
{
    /// <summary>
    /// What <see cref="InvoDeviceApproval.RunAsync"/> draws while the player approves on their phone.
    /// Implement it on your own UI, or drop <see cref="InvoDeviceApprovalPanelView"/> into a panel.
    /// With no view assigned the SDK falls back to <see cref="InvoDeviceApprovalOverlay"/> (IMGUI).
    /// The first-time-phone match-code prompt is separate: <see cref="InvoHostedApproval.PromptView"/>.
    /// </summary>
    public interface IInvoDeviceApprovalView
    {
        /// <summary>
        /// Desktop, Steam, console: show <paramref name="qr"/> (point-filtered, quiet zone included —
        /// draw it square and do not crop it), and print <paramref name="userCode"/> and
        /// <paramref name="verificationUri"/> beneath it for a player who types the address instead.
        /// The texture belongs to the SDK and is destroyed after <see cref="Hide"/>.
        /// </summary>
        void ShowQr(Texture2D qr, string userCode, string verificationUri, string headline, Action onCancel);

        /// <summary>Phones: the page opened in the system browser. Offer to reopen it.</summary>
        void ShowBrowserWaiting(string userCode, string headline, Action onReopen, Action onCancel);

        /// <summary>A one-line progress message, e.g. "Approved on your phone. Finishing…".</summary>
        void ShowStatus(string message);

        void Hide();
    }

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

    /// <summary>
    /// The fallback view: an IMGUI box with the QR, the manual-entry line, a status line and Cancel.
    /// Drawn under the match-code prompt. Good enough to ship a sandbox build; replace it with
    /// <see cref="InvoDeviceApprovalPanelView"/> for a production look.
    /// </summary>
    public sealed class InvoDeviceApprovalOverlay : MonoBehaviour, IInvoDeviceApprovalView
    {
        private const int BaseWidth = 440;
        private const int BasePadding = 20;
        private const int BaseFont = 16;

        private static InvoDeviceApprovalOverlay instance;

        private bool visible;
        private Texture2D qrTexture;
        private string headline = string.Empty;
        private string detail = string.Empty;
        private string status = string.Empty;
        private Action cancelAction;
        private Action reopenAction;

        /// <summary>The shared overlay, created on first use and kept across scenes.</summary>
        public static InvoDeviceApprovalOverlay Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("InvoDeviceApprovalOverlay");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<InvoDeviceApprovalOverlay>();
                }
                return instance;
            }
        }

        public void ShowQr(Texture2D qr, string userCode, string verificationUri, string headlineText, Action onCancel)
        {
            qrTexture = qr;
            headline = headlineText ?? string.Empty;
            detail = InvoDeviceApproval.ManualEntryText(verificationUri, userCode);
            status = string.Empty;
            cancelAction = onCancel;
            reopenAction = null;
            visible = true;
        }

        public void ShowBrowserWaiting(string userCode, string headlineText, Action onReopen, Action onCancel)
        {
            qrTexture = null;
            headline = headlineText ?? string.Empty;
            detail = string.IsNullOrEmpty(userCode) ? string.Empty : "Code: " + userCode;
            status = string.Empty;
            cancelAction = onCancel;
            reopenAction = onReopen;
            visible = true;
        }

        public void ShowStatus(string message)
        {
            status = message ?? string.Empty;
        }

        public void Hide()
        {
            visible = false;
            qrTexture = null;
            cancelAction = null;
            reopenAction = null;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            // Behind the match-code prompt (depth 0), which must win when both are up.
            GUI.depth = 1;

            float scale = Screen.dpi > 0 ? Mathf.Max(1f, Screen.dpi / 160f) : 1f;
            int padding = (int)(BasePadding * scale);
            int fontSize = (int)(BaseFont * scale);
            int width = (int)Mathf.Min(BaseWidth * scale, Screen.width - 2 * padding);
            float inner = width - 2 * padding;

            var titleStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = (int)(fontSize * 1.15f), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            var textStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = fontSize, alignment = TextAnchor.UpperCenter };
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = fontSize };

            float titleH = titleStyle.CalcHeight(new GUIContent(headline), inner);
            float qrSide = qrTexture != null ? Mathf.Min(inner, Screen.height * 0.5f) : 0f;
            float detailH = string.IsNullOrEmpty(detail) ? 0f : textStyle.CalcHeight(new GUIContent(detail), inner);
            float statusH = string.IsNullOrEmpty(status) ? 0f : textStyle.CalcHeight(new GUIContent(status), inner);
            float buttonH = fontSize * 2.5f;
            float height = padding + titleH + padding + (qrSide > 0 ? qrSide + padding : 0) + detailH +
                           (statusH > 0 ? padding / 2f + statusH : 0) + padding + buttonH + padding;
            var box = new Rect((Screen.width - width) / 2f, Mathf.Max(0, (Screen.height - height) / 2f), width, height);

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.Box(box, GUIContent.none);

            float y = box.y + padding;
            GUI.Label(new Rect(box.x + padding, y, inner, titleH), headline, titleStyle);
            y += titleH + padding;

            if (qrTexture != null)
            {
                // White backing so the quiet zone stays light on a dark skin.
                var qrRect = new Rect(box.x + (width - qrSide) / 2f, y, qrSide, qrSide);
                GUI.DrawTexture(qrRect, Texture2D.whiteTexture);
                GUI.DrawTexture(qrRect, qrTexture, ScaleMode.ScaleToFit, false);
                y += qrSide + padding;
            }

            if (detailH > 0)
            {
                GUI.Label(new Rect(box.x + padding, y, inner, detailH), detail, textStyle);
                y += detailH;
            }
            if (statusH > 0)
            {
                y += padding / 2f;
                GUI.Label(new Rect(box.x + padding, y, inner, statusH), status, textStyle);
                y += statusH;
            }
            y += padding;

            if (reopenAction != null)
            {
                float half = (inner - padding) / 2f;
                if (GUI.Button(new Rect(box.x + padding, y, half, buttonH), "Open again", buttonStyle))
                    reopenAction();
                if (GUI.Button(new Rect(box.x + 2 * padding + half, y, half, buttonH), "Cancel", buttonStyle))
                    Fire(cancelAction);
            }
            else if (GUI.Button(new Rect(box.x + padding, y, inner, buttonH), "Cancel", buttonStyle))
            {
                Fire(cancelAction);
            }
        }

        private static void Fire(Action action)
        {
            if (action != null)
                action();
        }
    }
}
