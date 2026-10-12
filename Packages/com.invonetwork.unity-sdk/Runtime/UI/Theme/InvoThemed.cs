using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// Gives a Graphic its colour, shape and (for text) font and size from the nearest
    /// <see cref="InvoThemeProvider"/>'s <see cref="InvoTheme"/>. Every graphic in the SDK prefabs has
    /// one. To restyle, change the theme, not the prefab; to opt one element out, remove the component.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class InvoThemed : MonoBehaviour
    {
        public InvoThemeRole role = InvoThemeRole.Surface;
        public InvoThemeShape shape = InvoThemeShape.Keep;
        public InvoTextStyle textStyle = InvoTextStyle.Keep;
        [Range(0f, 1f)] public float alpha = 1f;

        private void OnEnable()
        {
            InvoTheme.Changed += OnThemeChanged;
            Apply();
        }

        private void OnDisable() => InvoTheme.Changed -= OnThemeChanged;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (isActiveAndEnabled)
                Apply();
        }
#endif

        private void OnThemeChanged(InvoTheme _) => Apply();

        /// <summary>The theme this element uses: the nearest provider's, else the SDK default.</summary>
        public InvoTheme Theme
        {
            get
            {
                var provider = GetComponentInParent<InvoThemeProvider>(true);
                return provider != null && provider.theme != null ? provider.theme : InvoTheme.Default;
            }
        }

        public void Apply() => Apply(Theme);

        public void Apply(InvoTheme theme)
        {
            if (theme == null)
                return;
            var graphic = GetComponent<Graphic>();
            if (graphic == null)
                return;

            if (role != InvoThemeRole.None)
            {
                Color c = theme.Get(role);
                c.a *= alpha;
                graphic.color = c;
            }

            if (graphic is Image image)
                ApplyShape(image, theme);

            if (graphic is TMP_Text text)
            {
                if (theme.font != null)
                    text.font = theme.font;
                if (textStyle != InvoTextStyle.Keep)
                {
                    text.fontSize = theme.FontSize(textStyle);
                    text.fontStyle = InvoTheme.IsBold(textStyle) ? FontStyles.Bold : FontStyles.Normal;
                    if (textStyle == InvoTextStyle.Overline)
                    {
                        text.characterSpacing = 6f;
                        text.fontStyle |= FontStyles.UpperCase;
                    }
                }
            }
        }

        private void ApplyShape(Image image, InvoTheme theme)
        {
            switch (shape)
            {
                case InvoThemeShape.Keep:
                    return;
                case InvoThemeShape.Rectangle:
                    image.sprite = null;
                    image.type = Image.Type.Simple;
                    return;
                case InvoThemeShape.Circle:
                    image.sprite = theme.CircleSprite;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    return;
            }

            bool outline = shape == InvoThemeShape.RoundedOutline || shape == InvoThemeShape.RoundedSmallOutline;
            image.sprite = outline ? theme.RoundedOutlineSprite : theme.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;

            Sprite sprite = image.sprite;
            float border = sprite != null ? Mathf.Max(1f, sprite.border.x) : 32f;
            float radius;
            switch (shape)
            {
                case InvoThemeShape.Rounded:
                case InvoThemeShape.RoundedOutline:
                    radius = theme.cornerRadius;
                    break;
                case InvoThemeShape.Pill:
                    // Larger than any half-height: Unity scales overlapping slice borders down to fit,
                    // which gives fully round ends at any size.
                    radius = 1000f;
                    break;
                default:
                    radius = theme.smallCornerRadius;
                    break;
            }
            // A sliced image's corners are drawn at border / multiplier pixels.
            image.pixelsPerUnitMultiplier = radius <= 0.01f ? 1000f : border / radius;
        }
    }
}
