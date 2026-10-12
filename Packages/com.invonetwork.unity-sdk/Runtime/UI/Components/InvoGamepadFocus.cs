using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// Controller and keyboard support: when this panel (or step) becomes visible and nothing inside it
    /// is selected, selects <see cref="firstSelected"/>, or the first interactable control. Unity's
    /// automatic navigation then moves between buttons, fields and rows with a d-pad or arrow keys.
    /// Mouse and touch are unaffected.
    /// </summary>
    public class InvoGamepadFocus : MonoBehaviour
    {
        [Tooltip("Optional. Selected when the panel opens; empty = the first interactable control.")]
        public Selectable firstSelected;

        private int framesLeft;

        private void OnEnable() => framesLeft = 2; // wait for layout and for sibling steps to toggle

        private void LateUpdate()
        {
            if (framesLeft <= 0 || --framesLeft > 0)
                return;
            // Phones and tablets are touch-first: selecting for them would only draw focus rings.
            if (Application.isMobilePlatform)
                return;
            EventSystem es = EventSystem.current;
            if (es == null)
                return;
            GameObject current = es.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy && current.transform.IsChildOf(transform))
                return;
            Selectable target = firstSelected != null && firstSelected.IsInteractable() && firstSelected.gameObject.activeInHierarchy
                ? firstSelected
                : FirstInteractable();
            if (target != null)
                es.SetSelectedGameObject(target.gameObject);
        }

        private Selectable FirstInteractable()
        {
            // Text fields are skipped: selecting one opens the on-screen keyboard.
            foreach (Selectable s in GetComponentsInChildren<Selectable>(false))
                if (s.IsInteractable() && s.navigation.mode != Navigation.Mode.None && !(s is TMPro.TMP_InputField))
                    return s;
            return null;
        }
    }
}
