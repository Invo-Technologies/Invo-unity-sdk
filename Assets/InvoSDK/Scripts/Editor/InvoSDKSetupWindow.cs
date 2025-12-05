using UnityEditor;
using UnityEngine;

namespace InvoSDK.Editor
{
    public class InvoSDKSetupWizard : EditorWindow
    {
        private InvoSDKConfig config;

        [MenuItem("InvoSDK/Setup Wizard", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<InvoSDKSetupWizard>("InvoSDK Setup Wizard");
            window.minSize = new Vector2(500, 450);
            window.Show();
        }

        private void OnEnable()
        {
            // Try to load config
            config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");

            if (config == null)
            {
                // Auto-create if missing
                config = CreateInstance<InvoSDKConfig>();
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                AssetDatabase.CreateAsset(config, "Assets/Resources/InvoSDKConfig.asset");
                AssetDatabase.SaveAssets();
                Debug.Log("[InvoSDK] Created new InvoSDKConfig at Assets/Resources/InvoSDKConfig.asset");
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Welcome to InvoSDK", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "InvoSDK enables secure currency, item, and cross-game transfers.\n\n" +
                "⚠️ Your SDK key should NEVER be shipped in client builds.\n" +
                "Store it server-side only.",
                MessageType.Warning);

            GUILayout.Space(10);

            GUILayout.Label("Configuration", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            config.sdkKey = EditorGUILayout.TextField("SDK Key", config.sdkKey);
            config.gameId = EditorGUILayout.TextField("Game ID", config.gameId);
            config.gameName = EditorGUILayout.TextField("Game Name", config.gameName);
            config.gameIconUrl = EditorGUILayout.TextField("Game Icon URL", config.gameIconUrl);
            config.gameCurrencyName = EditorGUILayout.TextField("Currency Name", config.gameCurrencyName);
            config.gameCurrencyUrl = EditorGUILayout.TextField("Currency Icon URL", config.gameCurrencyUrl);
            config.gameVersion = EditorGUILayout.TextField("Game Version", config.gameVersion);
            config.useProduction = EditorGUILayout.Toggle("Use Production", config.useProduction);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            GUILayout.Space(20);

            if (GUILayout.Button("📖 Open Documentation"))
            {
                Application.OpenURL("https://docs.invo.network");
            }
            if (GUILayout.Button("🌐 Open Invo Console"))
            {
                var consoleUrl = config != null && config.useProduction
                    ? "https://console.invo.network"
                    : "https://dev.console.invo.network";
                Application.OpenURL(consoleUrl);
            }

            GUILayout.Space(20);

            GUILayout.Label("Quick Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("➡ Make Test Purchase"))
            {
                InvoSDKTestPurchaseWindow.ShowWindow();
            }

            if (GUILayout.Button("➡ Get Player Balances"))
            {
                InvoSDKPlayerBalanceWindow.ShowWindow();
            }
        }
    }
}
