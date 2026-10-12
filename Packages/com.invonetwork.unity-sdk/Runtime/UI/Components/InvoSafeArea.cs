using UnityEngine;

namespace InvoSDK.UI
{
    /// <summary>
    /// Keeps this RectTransform inside <see cref="Screen.safeArea"/>: clear of notches, rounded corners
    /// and home indicators on phones, and of TV overscan on consoles that report it. Put it on a full-screen
    /// child of a Screen Space canvas; the SDK window's Content has one.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InvoSafeArea : MonoBehaviour
    {
        private Rect applied;
        private Vector2Int appliedScreen;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != applied || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y)
                Apply();
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            applied = safe;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
                return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
