using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>One item in the Collect list.</summary>
    public class InvoPendingItemView : MonoBehaviour
    {
        public TMP_Text amountText;
        public TMP_Text detailText;
        public TMP_Text initialsText;
        public Button collectButton;
        [Tooltip("Shown instead of the Collect button while the item is held.")]
        public GameObject heldMark;
        public CanvasGroup canvasGroup;

        public void Bind(PendingAction item, string detail, Action<PendingAction> onCollect)
        {
            if (amountText != null) amountText.text = $"{item.amount} {item.currency}".Trim();
            if (detailText != null) detailText.text = detail;
            if (initialsText != null) initialsText.text = InvoGameSearch.Initials(item.counterparty_game);
            if (canvasGroup != null) canvasGroup.alpha = item.held ? 0.6f : 1f;
            if (heldMark != null) heldMark.SetActive(item.held);
            if (collectButton != null)
            {
                collectButton.gameObject.SetActive(!item.held);
                collectButton.onClick.RemoveAllListeners();
                collectButton.onClick.AddListener(() => onCollect?.Invoke(item));
            }
        }
    }
}
