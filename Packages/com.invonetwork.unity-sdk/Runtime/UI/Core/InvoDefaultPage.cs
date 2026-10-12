using UnityEngine;

namespace InvoSDK.UI.Core
{
    /// <summary>Shows <see cref="pageId"/> on this object's <see cref="InvoSDKWindowManager"/> the first time it is enabled.</summary>
    [RequireComponent(typeof(InvoSDKWindowManager))]
    public class InvoDefaultPage : MonoBehaviour
    {
        public string pageId;

        private bool shown;

        private void OnEnable()
        {
            if (shown || string.IsNullOrEmpty(pageId))
                return;
            shown = true;
            GetComponent<InvoSDKWindowManager>().ShowPanel(pageId);
        }
    }
}
