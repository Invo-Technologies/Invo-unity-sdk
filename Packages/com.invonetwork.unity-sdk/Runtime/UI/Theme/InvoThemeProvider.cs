using UnityEngine;

namespace InvoSDK.UI
{
    /// <summary>
    /// Sets the <see cref="InvoTheme"/> for everything below it. The SDK window has one on its root:
    /// assign your theme here. Empty uses the SDK's dark default.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class InvoThemeProvider : MonoBehaviour
    {
        public InvoTheme theme;

        private InvoTheme applied;

        private void OnEnable() => ApplyAll();

        private void Update()
        {
            // Swapping the theme at runtime (light/dark toggle, per-player skin) restyles at once.
            if (applied != theme)
                ApplyAll();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (isActiveAndEnabled)
                ApplyAll();
        }
#endif

        /// <summary>Restyles every <see cref="InvoThemed"/> below this object, inactive ones included.</summary>
        public void ApplyAll()
        {
            applied = theme;
            InvoTheme active = theme != null ? theme : InvoTheme.Default;
            foreach (InvoThemed themed in GetComponentsInChildren<InvoThemed>(true))
                themed.Apply(active);
        }
    }
}
