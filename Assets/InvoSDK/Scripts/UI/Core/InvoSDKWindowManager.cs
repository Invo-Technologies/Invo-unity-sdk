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

        public void ShowPanel(string id)
        {
            foreach (var kv in panelLookup)
                kv.Value.SetActive(kv.Key == id);

            //Debug.Log($"[InvoSDK] Showing panel: {id}");
        }

        public void HideAll()
        {
            foreach (var kv in panelLookup)
                kv.Value.SetActive(false);
        }
    }
}
