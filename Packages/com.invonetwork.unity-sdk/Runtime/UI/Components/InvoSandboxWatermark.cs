using TMPro;
using UnityEngine;

namespace InvoSDK.UI
{
    /// <summary>
    /// A small SANDBOX label, shown only in the editor and development builds while the SDK talks to
    /// sandbox, and only when <see cref="InvoSDKConfig.showSandboxWatermark"/> is on. Never in release builds.
    /// </summary>
    public class InvoSandboxWatermark : MonoBehaviour
    {
        public GameObject label;
        public TMP_Text labelText;

        private void Start()
        {
            var config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            bool show = (Application.isEditor || Debug.isDebugBuild) && config != null &&
                        !config.useProduction && config.showSandboxWatermark;
            if (label != null) label.SetActive(show);
            if (show && labelText != null)
                labelText.text = "SANDBOX · " + InvoStorefrontResolver.DisplayName(config.ActiveStorefront);
        }
    }
}
