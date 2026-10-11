using System.Collections.Generic;
using UnityEngine;

namespace InvoSDK
{
    [CreateAssetMenu(fileName = "InvoSDKItemCatalog", menuName = "InvoSDK/Item Catalog", order = 2)]
    public class InvoSDKItemCatalog : ScriptableObject
    {
        public List<InvoSDKItem> items = new List<InvoSDKItem>();
    }
}
