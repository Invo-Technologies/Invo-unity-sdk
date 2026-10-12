using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI.Core
{
    /// <summary>
    /// Segmented tabs over an <see cref="InvoSDKWindowManager"/>. Highlights the tab of whatever panel
    /// is showing, including panels opened from code.
    /// </summary>
    public class InvoTabBar : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public string panelId;
            public Button button;
            [Tooltip("The tab's background; its InvoThemed role switches between selected and not.")]
            public InvoThemed background;
            public InvoThemed label;
        }

        public InvoSDKWindowManager windowManager;
        public List<Tab> tabs = new List<Tab>();

        private void Awake()
        {
            foreach (Tab tab in tabs)
            {
                Tab captured = tab;
                if (tab.button != null)
                    tab.button.onClick.AddListener(() => windowManager.ShowPanel(captured.panelId));
            }
        }

        private void OnEnable()
        {
            if (windowManager != null)
            {
                windowManager.PanelShown += Highlight;
                Highlight(windowManager.CurrentPanelId);
            }
        }

        private void OnDisable()
        {
            if (windowManager != null)
                windowManager.PanelShown -= Highlight;
        }

        public void Highlight(string panelId)
        {
            foreach (Tab tab in tabs)
            {
                bool on = tab.panelId == panelId;
                if (tab.background != null)
                {
                    tab.background.role = on ? InvoThemeRole.SurfaceRaised : InvoThemeRole.Field;
                    tab.background.alpha = on ? 1f : 0f;
                    tab.background.Apply();
                }
                if (tab.label != null)
                {
                    tab.label.role = on ? InvoThemeRole.TextPrimary : InvoThemeRole.TextSecondary;
                    tab.label.Apply();
                }
            }
        }
    }
}
