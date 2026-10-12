using UnityEngine;

namespace InvoSDK.UI.Core
{
    /// <summary>
    /// Opens and closes the Invo SDK window. Call <see cref="Open"/> from your own menu button
    /// (for example "Wallet" or "Store") and the window shows the panel you name.
    /// </summary>
    public class InvoSDKWindowController : MonoBehaviour
    {
        public InvoSDKWindowManager windowManager;
        [Tooltip("Shown when the window opens without a panel id.")]
        public string defaultPanelId = "Store";
        [Tooltip("The sheet and backdrop. Defaults to this GameObject.")]
        public GameObject content;
        public bool openOnStart = true;

        public static InvoSDKWindowController Instance { get; private set; }

        public bool IsOpen => Content.activeSelf;

        private GameObject Content => content != null ? content : gameObject;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        private void Start()
        {
            if (openOnStart)
                Open(null);
            else
                Close();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Open(string panelId)
        {
            Content.SetActive(true);
            if (windowManager != null)
                windowManager.ShowPanel(string.IsNullOrEmpty(panelId) ? defaultPanelId : panelId);
        }

        /// <summary>Inspector-friendly overload for UnityEvents.</summary>
        public void OpenDefault() => Open(null);

        public void Close() => Content.SetActive(false);

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open(null);
        }
    }
}
