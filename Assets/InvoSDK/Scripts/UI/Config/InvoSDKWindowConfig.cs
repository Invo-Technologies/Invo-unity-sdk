using System;
using System.Collections.Generic;
using UnityEngine;

namespace InvoSDK
{
    [CreateAssetMenu(fileName = "InvoSDKWindowConfig", menuName = "InvoSDK/Window Config", order = 2)]
    public class InvoSDKWindowConfig : ScriptableObject
    {
        [Serializable]
        public class PanelEntry
        {
            public string panelId;
            public GameObject panelPrefab;
            public bool loadAtStartup = false;
        }

        public List<PanelEntry> panels = new();
        public string defaultPanelId = "TransferCurrency";
    }
}