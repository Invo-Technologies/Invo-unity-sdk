using System.Collections.Generic;
using UnityEngine;

namespace InvoSDK.UI.Core
{
    public class InvoSDKWindowManager : MonoBehaviour
    {
        [System.Serializable]
        public class PanelEntry
        {
            public string panelId;
            public GameObject panel;
        }

        [Header("Panels")]
        public List<PanelEntry> panels = new();

        private Dictionary<string, GameObject> panelLookup;

        private void Awake()
        {
            panelLookup = new Dictionary<string, GameObject>();
            foreach (var entry in panels)
            {
                if (!panelLookup.ContainsKey(entry.panelId))
                    panelLookup.Add(entry.panelId, entry.panel);
            }
        }

        /// <summary>Raised after <see cref="ShowPanel"/> with the panel's id (tab bars use it to highlight).</summary>
        public event System.Action<string> PanelShown;

        /// <summary>The id of the panel last shown, or null.</summary>
        public string CurrentPanelId { get; private set; }

        public void ShowPanel(string id)
        {
            if (panelLookup == null)
                Awake();
            foreach (var kv in panelLookup)
                kv.Value.SetActive(kv.Key == id);
            CurrentPanelId = id;
            PanelShown?.Invoke(id);
        }

        public void HideAll()
        {
            foreach (var kv in panelLookup)
                kv.Value.SetActive(false);
        }
    }
}
