using System;
using UnityEngine;

namespace InvoSDK
{
    /// <summary>
    /// The surface <see cref="InvoHostedApproval"/> draws the match-code prompt on. Implement it
    /// on your own UI (uGUI, UI Toolkit, a TMP dialog) and assign
    /// <see cref="InvoHostedApproval.PromptView"/>; otherwise the IMGUI
    /// <see cref="InvoEnrollmentPromptOverlay"/> below is used so the flow works with no prefab.
    /// </summary>
    public interface IInvoEnrollmentPromptView
    {
        /// <summary>Two buttons. Fire <paramref name="onDecision"/> exactly once, then hide.</summary>
        void ShowPrompt(string message, string yesLabel, string noLabel, Action<InvoEnrollmentDecision> onDecision);

        /// <summary>One button ("Stop it (wrong code)"). Fire <paramref name="onStop"/> at most once.</summary>
        void ShowFinishing(string message, string stopLabel, Action onStop);

        void Hide();
    }

    /// <summary>
    /// Default prompt: a centred IMGUI box with the copy and one or two buttons. Toolkit-agnostic
    /// on purpose; it renders in any project without a prefab or a canvas. Replace it by assigning
    /// <see cref="InvoHostedApproval.PromptView"/>.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class InvoEnrollmentPromptOverlay : MonoBehaviour, IInvoEnrollmentPromptView
    {
        private const int BaseWidth = 420;
        private const int BasePadding = 20;
        private const int BaseFont = 16;

        private bool visible;
        private string message = string.Empty;
        private string primaryLabel;
        private string secondaryLabel = string.Empty;
        private Action<InvoEnrollmentDecision> pendingDecision;
        private Action pendingStop;

        public bool IsVisible { get { return visible; } }

        public void ShowPrompt(string text, string yesLabel, string noLabel, Action<InvoEnrollmentDecision> onDecision)
        {
            message = text ?? string.Empty;
            primaryLabel = yesLabel;
            secondaryLabel = noLabel;
            pendingDecision = onDecision;
            pendingStop = null;
            visible = true;
        }

        public void ShowFinishing(string text, string stopLabel, Action onStop)
        {
            message = text ?? string.Empty;
            primaryLabel = null;
            secondaryLabel = stopLabel;
            pendingDecision = null;
            pendingStop = onStop;
            visible = true;
        }

        public void Hide()
        {
            visible = false;
            pendingDecision = null;
            pendingStop = null;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            float scale = Screen.dpi > 0 ? Mathf.Max(1f, Screen.dpi / 160f) : 1f;
            int width = (int)(BaseWidth * scale);
            int padding = (int)(BasePadding * scale);
            int fontSize = (int)(BaseFont * scale);

            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            textStyle.wordWrap = true;
            textStyle.fontSize = fontSize;
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = fontSize;

            float textHeight = textStyle.CalcHeight(new GUIContent(message), width - 2 * padding);
            float buttonHeight = fontSize * 2.5f;
            float height = textHeight + buttonHeight + 3 * padding;
            Rect box = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

            // Visual dimmer only. IMGUI does not intercept uGUI, UI Toolkit or Input System
            // touches, so the game's own UI stays interactive underneath; a custom
            // IInvoEnrollmentPromptView is the place to block it if that matters.
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.Box(box, GUIContent.none);

            GUI.Label(new Rect(box.x + padding, box.y + padding, width - 2 * padding, textHeight), message, textStyle);

            float buttonsY = box.y + padding + textHeight + padding;
            if (primaryLabel != null)
            {
                float half = (width - 3 * padding) / 2f;
                if (GUI.Button(new Rect(box.x + padding, buttonsY, half, buttonHeight), primaryLabel, buttonStyle))
                    Decide(InvoEnrollmentDecision.Approve);
                if (GUI.Button(new Rect(box.x + 2 * padding + half, buttonsY, half, buttonHeight), secondaryLabel, buttonStyle))
                    Decide(InvoEnrollmentDecision.Deny);
            }
            else
            {
                if (GUI.Button(new Rect(box.x + padding, buttonsY, width - 2 * padding, buttonHeight), secondaryLabel, buttonStyle))
                    Stop();
            }
        }

        private void Decide(InvoEnrollmentDecision decision)
        {
            Action<InvoEnrollmentDecision> cb = pendingDecision;
            Hide();
            if (cb != null)
                cb(decision);
        }

        private void Stop()
        {
            Action cb = pendingStop;
            Hide();
            if (cb != null)
                cb();
        }
    }
}
