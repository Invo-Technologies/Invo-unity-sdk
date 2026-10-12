using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// Switches the CanvasScaler's reference resolution between landscape (PC, consoles, landscape
    /// phones) and portrait (phones held upright, tablets), so the SDK sheet fills most of a phone's
    /// width instead of shrinking to a landscape-sized card. Runs before CanvasScaler each frame and
    /// only touches it when the orientation changes.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CanvasScaler))]
    public class InvoAdaptiveScaler : MonoBehaviour
    {
        public Vector2 landscapeReference = new Vector2(1920, 1080);
        public Vector2 portraitReference = new Vector2(640, 1000);
        [Range(0f, 1f)] public float match = 0.5f;

        private CanvasScaler scaler;
        private Canvas canvas;
        private int appliedPortrait = -1;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            canvas = GetComponent<Canvas>();
        }

        private void Update()
        {
            // The canvas' own pixel size: right for screen and render-texture (capture) cameras alike.
            Rect px = canvas != null && canvas.pixelRect.width > 0 ? canvas.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
            int portrait = px.height > px.width ? 1 : 0;
            if (portrait == appliedPortrait)
                return;
            appliedPortrait = portrait;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = portrait == 1 ? portraitReference : landscapeReference;
            scaler.matchWidthOrHeight = match;
        }
    }
}
