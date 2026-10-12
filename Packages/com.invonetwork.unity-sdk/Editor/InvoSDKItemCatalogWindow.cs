using UnityEditor;
using UnityEngine;
using System.Linq;

namespace InvoSDK.Editor
{
    public class InvoSDKItemCatalogWindow : EditorWindow
    {
        private InvoSDKItemCatalog catalog;
        private Vector2 scrollPos;
        private int selectedIndex = -1;

        [MenuItem("InvoSDK/Item Catalog", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<InvoSDKItemCatalogWindow>("InvoSDK Item Catalog");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        private void OnEnable()
        {
            catalog = Resources.Load<InvoSDKItemCatalog>("InvoSDKItemCatalog");

            if (catalog == null)
            {
                catalog = CreateInstance<InvoSDKItemCatalog>();
                string path = "Assets/InvoSDK/Resources/InvoSDKItemCatalog.asset";
                AssetDatabase.CreateAsset(catalog, path);
                AssetDatabase.SaveAssets();
                Debug.Log("[InvoSDK] Created new Item Catalog asset at: " + path);
            }
        }

        private void OnGUI()
        {
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("No catalog found!", MessageType.Error);
                return;
            }

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Add Item", EditorStyles.toolbarButton))
            {
                catalog.items.Add(new InvoSDKItem { itemName = "New Item" });
                selectedIndex = catalog.items.Count - 1;
                EditorUtility.SetDirty(catalog);
            }

            if (GUILayout.Button("Remove Selected", EditorStyles.toolbarButton) && selectedIndex >= 0)
            {
                catalog.items.RemoveAt(selectedIndex);
                selectedIndex = -1;
                EditorUtility.SetDirty(catalog);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
            {
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Item Catalog", EditorStyles.boldLabel);
            GUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < catalog.items.Count; i++)
            {
                var item = catalog.items[i];

                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                item.itemName = EditorGUILayout.TextField("Name", item.itemName);
                if (GUILayout.Button("Select", GUILayout.Width(80)))
                    selectedIndex = i;
                EditorGUILayout.EndHorizontal();

                item.itemId = EditorGUILayout.TextField("Item ID", item.itemId);
                item.itemDescription = EditorGUILayout.TextField("Description", item.itemDescription);
                item.priceUSD = EditorGUILayout.FloatField("Price (USD)", item.priceUSD);
                item.originalPriceUSD = EditorGUILayout.FloatField("Original Price (USD)", item.originalPriceUSD ?? 0);

                GUILayout.Space(3);
                item.itemSprite = (Sprite)EditorGUILayout.ObjectField("Item Sprite", item.itemSprite, typeof(Sprite), false);
                item.imageUrl = EditorGUILayout.TextField("Image URL", item.imageUrl);

                GUILayout.Space(3);
                item.category = EditorGUILayout.TextField("Category", item.category);
                item.rarity = EditorGUILayout.TextField("Rarity", item.rarity);
                item.tag = EditorGUILayout.TextField("Tag", item.tag);

                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
        }
#if UNITY_EDITOR
        private void DrawItemPreview(InvoSDKItem item)
        {
            GUILayout.Space(5);
            GUILayout.Label("Preview", EditorStyles.boldLabel);

            if (item.itemSprite != null)
            {
                GUILayout.Label(item.itemSprite.texture, GUILayout.Height(100));
            }
            else if (!string.IsNullOrEmpty(item.imageUrl))
            {
                GUILayout.Label($"Remote: {item.imageUrl}", EditorStyles.miniLabel);
            }
        }
#endif
    }


}