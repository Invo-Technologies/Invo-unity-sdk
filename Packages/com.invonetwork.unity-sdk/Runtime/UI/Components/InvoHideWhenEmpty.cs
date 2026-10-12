using TMPro;
using UnityEngine;

namespace InvoSDK.UI
{
    /// <summary>Fades this object out while <see cref="text"/> is empty (status pills, badges).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class InvoHideWhenEmpty : MonoBehaviour
    {
        public TMP_Text text;

        private CanvasGroup group;

        private void Awake() => group = GetComponent<CanvasGroup>();

        private void LateUpdate()
        {
            bool empty = text == null || string.IsNullOrWhiteSpace(text.text) || !text.gameObject.activeSelf;
            group.alpha = empty ? 0f : 1f;
        }
    }
}
