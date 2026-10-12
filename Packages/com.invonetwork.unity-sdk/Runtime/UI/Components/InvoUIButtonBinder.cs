using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI.Components
{
    [RequireComponent(typeof(Button))]
    public class InvoUIButtonBinder : MonoBehaviour
    {
        public string panelId;
        public InvoSDK.UI.Core.InvoSDKWindowManager windowManager;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (windowManager != null && !string.IsNullOrEmpty(panelId))
            {
                windowManager.ShowPanel(panelId);
            }
        }
    }
}