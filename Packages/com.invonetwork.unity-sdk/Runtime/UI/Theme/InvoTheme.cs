using System;
using TMPro;
using UnityEngine;

namespace InvoSDK.UI
{
    /// <summary>What a themed element is, so the theme knows which colour to give it.</summary>
    public enum InvoThemeRole
    {
        None,
        Backdrop,
        Surface,
        SurfaceRaised,
        Field,
        Border,
        Accent,
        AccentSoft,
        OnAccent,
        TextPrimary,
        TextSecondary,
        TextMuted,
        Link,
        Success,
        SuccessSoft,
        Warning,
        Danger
    }

    /// <summary>The outline a themed image draws with.</summary>
    public enum InvoThemeShape
    {
        /// <summary>Keep whatever sprite the image has.</summary>
        Keep,
        Rectangle,
        Rounded,
        RoundedSmall,
        Pill,
        Circle,
        RoundedOutline,
        RoundedSmallOutline
    }

    /// <summary>Type sizes from the theme's scale.</summary>
    public enum InvoTextStyle
    {
        Keep,
        Display,
        Title,
        Heading,
        Body,
        BodyStrong,
        Caption,
        Overline
    }

    /// <summary>
    /// Every colour, the font, the corner radius and the spacing the Invo SDK panels use. Duplicate
    /// <c>InvoDefaultTheme</c>, change the values and assign it to the window's
    /// <see cref="InvoThemeProvider"/>: every panel restyles, in the editor too, without prefab edits,
    /// and SDK updates never overwrite your copy.
    /// </summary>
    [CreateAssetMenu(fileName = "InvoTheme", menuName = "InvoSDK/Theme", order = 3)]
    public class InvoTheme : ScriptableObject
    {
        [Header("Colours")]
        [Tooltip("Dims the game behind the SDK sheet.")]
        public Color backdrop = new Color32(5, 7, 11, 200);
        public Color surface = new Color32(20, 25, 34, 255);
        public Color surfaceRaised = new Color32(27, 33, 48, 255);
        public Color field = new Color32(15, 19, 26, 255);
        public Color border = new Color32(40, 48, 65, 255);
        public Color accent = new Color32(59, 111, 224, 255);
        [Tooltip("Selected rows and chips.")]
        public Color accentSoft = new Color32(26, 35, 54, 255);
        [Tooltip("Text and icons on the accent colour.")]
        public Color onAccent = Color.white;
        public Color textPrimary = new Color32(238, 241, 246, 255);
        public Color textSecondary = new Color32(163, 172, 186, 255);
        public Color textMuted = new Color32(125, 135, 151, 255);
        public Color link = new Color32(143, 179, 255, 255);
        public Color success = new Color32(111, 211, 168, 255);
        public Color successSoft = new Color32(19, 58, 44, 255);
        public Color warning = new Color32(242, 169, 59, 255);
        public Color danger = new Color32(240, 112, 104, 255);

        [Header("Type")]
        [Tooltip("Leave empty to keep the SDK's Inter font.")]
        public TMP_FontAsset font;
        [Range(0.75f, 1.5f)] public float textScale = 1f;

        [Header("Shape")]
        [Tooltip("Corner radius of cards and sheets, in pixels at the 1920x1080 reference resolution.")]
        [Range(0f, 40f)] public float cornerRadius = 20f;
        [Tooltip("Corner radius of buttons, fields and rows.")]
        [Range(0f, 40f)] public float smallCornerRadius = 12f;
        [Tooltip("Base spacing unit. Paddings and gaps are multiples of it.")]
        [Range(4f, 16f)] public float spacing = 8f;

        [Header("Shape sprites (leave empty to use the SDK's)")]
        [Tooltip("White 9-sliced rounded rectangle; its border is the radius at multiplier 1.")]
        public Sprite roundedSprite;
        public Sprite roundedOutlineSprite;
        public Sprite circleSprite;

        public Sprite RoundedSprite => roundedSprite != null ? roundedSprite : (this != Default ? Default.RoundedSprite : null);
        public Sprite RoundedOutlineSprite => roundedOutlineSprite != null ? roundedOutlineSprite : (this != Default ? Default.RoundedOutlineSprite : null);
        public Sprite CircleSprite => circleSprite != null ? circleSprite : (this != Default ? Default.CircleSprite : null);

        /// <summary>Raised when the asset changes in the editor, so open panels restyle live.</summary>
        public static event Action<InvoTheme> Changed;

        public Color Get(InvoThemeRole role)
        {
            switch (role)
            {
                case InvoThemeRole.Backdrop: return backdrop;
                case InvoThemeRole.Surface: return surface;
                case InvoThemeRole.SurfaceRaised: return surfaceRaised;
                case InvoThemeRole.Field: return field;
                case InvoThemeRole.Border: return border;
                case InvoThemeRole.Accent: return accent;
                case InvoThemeRole.AccentSoft: return accentSoft;
                case InvoThemeRole.OnAccent: return onAccent;
                case InvoThemeRole.TextPrimary: return textPrimary;
                case InvoThemeRole.TextSecondary: return textSecondary;
                case InvoThemeRole.TextMuted: return textMuted;
                case InvoThemeRole.Link: return link;
                case InvoThemeRole.Success: return success;
                case InvoThemeRole.SuccessSoft: return successSoft;
                case InvoThemeRole.Warning: return warning;
                case InvoThemeRole.Danger: return danger;
                default: return Color.white;
            }
        }

        public float FontSize(InvoTextStyle style)
        {
            float size;
            switch (style)
            {
                case InvoTextStyle.Display: size = 44f; break;
                case InvoTextStyle.Title: size = 26f; break;
                case InvoTextStyle.Heading: size = 18f; break;
                case InvoTextStyle.BodyStrong:
                case InvoTextStyle.Body: size = 16f; break;
                case InvoTextStyle.Caption: size = 14f; break;
                case InvoTextStyle.Overline: size = 13f; break;
                default: return 0f;
            }
            return Mathf.Round(size * textScale);
        }

        public static bool IsBold(InvoTextStyle style) =>
            style == InvoTextStyle.Display || style == InvoTextStyle.Title ||
            style == InvoTextStyle.Heading || style == InvoTextStyle.BodyStrong || style == InvoTextStyle.Overline;

        /// <summary>The SDK's built-in dark theme (Resources/InvoDefaultTheme).</summary>
        public static InvoTheme Default
        {
            get
            {
                if (cachedDefault == null)
                    cachedDefault = Resources.Load<InvoTheme>("InvoDefaultTheme");
                if (cachedDefault == null)
                    cachedDefault = CreateInstance<InvoTheme>();
                return cachedDefault;
            }
        }

        private static InvoTheme cachedDefault;

#if UNITY_EDITOR
        private void OnValidate() => Changed?.Invoke(this);
#endif
    }
}
