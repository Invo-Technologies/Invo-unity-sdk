using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>One recent recipient: a circle with the number's last two digits and the masked number.</summary>
    public class InvoRecipientChip : MonoBehaviour
    {
        public Button button;
        public TMP_Text badgeText;
        public TMP_Text label;
        [Tooltip("Outline shown on the selected chip.")]
        public GameObject selectedMark;

        public string Phone { get; private set; }

        public void Bind(string phone, bool selected, Action<string> onPick)
        {
            Phone = phone;
            string digits = phone ?? string.Empty;
            if (badgeText != null) badgeText.text = digits.Length >= 2 ? digits.Substring(digits.Length - 2) : digits;
            if (label != null) label.text = InvoPhone.MaskShort(phone);
            SetSelected(selected);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onPick?.Invoke(phone));
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedMark != null) selectedMark.SetActive(selected);
        }
    }
}
