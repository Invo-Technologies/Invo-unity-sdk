using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>One game in the picker: a tile in "Games you play" or a row in the list.</summary>
    public class InvoGameEntryView : MonoBehaviour
    {
        public Button button;
        public Image icon;
        [Tooltip("Shown with initials while there is no icon.")]
        public Image initialsBackground;
        public TMP_Text initialsText;
        public TMP_Text nameText;
        public TMP_Text subtitleText;
        [Tooltip("Optional: the player's balance in that game.")]
        public TMP_Text detailText;
        [Tooltip("Optional: shown on the selected entry.")]
        public GameObject selectedMark;
        public CanvasGroup canvasGroup;

        private static readonly Color32[] InitialsPalette =
        {
            new Color32(90, 59, 140, 255), new Color32(46, 107, 87, 255), new Color32(140, 90, 46, 255),
            new Color32(46, 78, 140, 255), new Color32(122, 46, 78, 255), new Color32(58, 74, 92, 255)
        };

        public InvoGameOption Option { get; private set; }

        public void Bind(InvoGameOption option, bool selected, Action<InvoGameOption> onPick)
        {
            Option = option;
            if (nameText != null) nameText.text = option.name;
            if (subtitleText != null)
                subtitleText.text = option.available ? option.subtitle : (option.unavailableReason ?? "Not accepting currency right now");
            if (detailText != null)
            {
                detailText.text = option.detail ?? string.Empty;
                detailText.gameObject.SetActive(!string.IsNullOrEmpty(option.detail));
            }
            if (initialsText != null) initialsText.text = InvoGameSearch.Initials(option.name);
            if (initialsBackground != null)
                initialsBackground.color = InitialsPalette[Mathf.Abs((option.name ?? string.Empty).GetHashCode()) % InitialsPalette.Length];
            ShowIcon(false);
            if (!string.IsNullOrEmpty(option.iconUrl) && icon != null)
                _ = LoadIconAsync(option.iconUrl);

            if (selectedMark != null) selectedMark.SetActive(selected);
            if (canvasGroup != null) canvasGroup.alpha = option.available ? 1f : 0.45f;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = option.available;
                button.onClick.AddListener(() => onPick?.Invoke(option));
            }
        }

        private async System.Threading.Tasks.Task LoadIconAsync(string url)
        {
            Sprite sprite = await APIManager.LoadSpriteAsync(url, null);
            if (this == null || icon == null || sprite == null || Option == null || Option.iconUrl != url)
                return;
            icon.sprite = sprite;
            icon.preserveAspect = true;
            ShowIcon(true);
        }

        private void ShowIcon(bool show)
        {
            if (icon != null) icon.gameObject.SetActive(show);
            if (initialsBackground != null) initialsBackground.gameObject.SetActive(!show);
        }
    }
}
