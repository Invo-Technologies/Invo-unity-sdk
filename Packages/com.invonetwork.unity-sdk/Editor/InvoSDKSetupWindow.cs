using UnityEditor;
using UnityEngine;

namespace InvoSDK.Editor
{
    public class InvoSDKSetupWizard : EditorWindow
    {
        /// <summary>
        /// The one and only location of the config asset. A second asset named
        /// InvoSDKConfig under any other Resources root makes Resources.Load resolution
        /// undefined, so the wizard always reads and writes exactly this path.
        /// </summary>
        public const string ConfigAssetPath = "Assets/InvoSDK/Resources/InvoSDKConfig.asset";
        private const string ConfigFolder = "Assets/InvoSDK/Resources";
        private const string StrayConfigAssetPath = "Assets/Resources/InvoSDKConfig.asset";

        private const string ReadmeProxySectionUrl =
            "https://github.com/Invo-Technologies/Invo-unity-sdk#server-side-proxy";

        private InvoSDKConfig config;
        private Vector2 scroll;

        [MenuItem("InvoSDK/Setup Wizard", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<InvoSDKSetupWizard>("InvoSDK Setup Wizard");
            window.minSize = new Vector2(520, 520);
            window.Show();
        }

        private void OnEnable()
        {
            config = LoadOrCreateConfig();
        }

        /// <summary>Loads the canonical config asset, creating it if it does not exist yet.</summary>
        public static InvoSDKConfig LoadOrCreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(ConfigAssetPath);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/InvoSDK"))
                AssetDatabase.CreateFolder("Assets", "InvoSDK");
            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder("Assets/InvoSDK", "Resources");

            var created = CreateInstance<InvoSDKConfig>();
            AssetDatabase.CreateAsset(created, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[InvoSDK] Created new InvoSDKConfig at " + ConfigAssetPath);
            return created;
        }

        private void OnGUI()
        {
            if (config == null)
                config = LoadOrCreateConfig();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            GUILayout.Label("Welcome to InvoSDK", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "The SDK key you enter below is stored in a ScriptableObject under Resources, " +
                "which means it IS compiled into every player build you make and can be " +
                "extracted from that build in minutes.\n\n" +
                "Only ever put a SANDBOX key here. A production key must live on your own " +
                "server, behind a proxy that your game calls instead of calling Invo directly.",
                MessageType.Warning);

            if (GUILayout.Button("Read: shipping keys safely (server-side proxy)"))
                Application.OpenURL(ReadmeProxySectionUrl);

            DrawStrayConfigWarning();

            GUILayout.Space(10);
            GUILayout.Label("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Asset", ConfigAssetPath, EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();

            config.sdkKey = EditorGUILayout.TextField(
                new GUIContent("SDK Key (sandbox)",
                    "Ships inside your build and is extractable. Sandbox keys only."),
                config.sdkKey);
            config.gameId = EditorGUILayout.TextField("Game ID", config.gameId);
            config.gameName = EditorGUILayout.TextField("Game Name", config.gameName);
            config.gameIconUrl = EditorGUILayout.TextField("Game Icon URL", config.gameIconUrl);
            config.gameCurrencyName = EditorGUILayout.TextField("Currency Name", config.gameCurrencyName);
            config.gameCurrencyUrl = EditorGUILayout.TextField("Currency Icon URL", config.gameCurrencyUrl);
            config.gameVersion = EditorGUILayout.TextField("Game Version", config.gameVersion);
            config.useProduction = EditorGUILayout.Toggle("Use Production", config.useProduction);

            GUILayout.Space(10);
            GUILayout.Label("Game Server", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Required for production. Calls that need the game secret (initiate send/transfer, " +
                "item purchase, catalog, balance, claims, player tokens) go to <this URL>/api/<Invo path>. " +
                "Your server checks the player's session, adds X-Game-Secret-Key and forwards to Invo. " +
                "Blank: the SDK calls Invo directly with the SDK Key, which it only allows in sandbox.",
                MessageType.Info);
            config.gameServerUrl = EditorGUILayout.TextField(
                new GUIContent("Game Server URL",
                    "Base URL of your server, e.g. https://api.mygame.com. See README, 'Server-side proxy'."),
                config.gameServerUrl);

            GUILayout.Space(10);
            GUILayout.Label("Hosted Checkout", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Real-money purchases cannot be made from the client. Point this at YOUR server " +
                "endpoint: the SDK POSTs { player_email, usd_amount } to it and expects " +
                "{ checkout_url } in the response, then opens that URL. Your endpoint holds the " +
                "production secret; the game never sees it.",
                MessageType.Info);
            config.checkoutSessionEndpoint = EditorGUILayout.TextField(
                new GUIContent("Checkout Session URL",
                    "Your server endpoint that mints a hosted-checkout session and returns { checkout_url }."),
                config.checkoutSessionEndpoint);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            DrawProductionKeyWarning();
            DrawMissingGameServerWarning();

            GUILayout.Space(20);

            if (GUILayout.Button("Open Documentation"))
                Application.OpenURL("https://docs.invo.network");

            if (GUILayout.Button("Open Invo Console"))
            {
                var consoleUrl = config != null && config.useProduction
                    ? "https://console.invo.network"
                    : "https://dev.console.invo.network";
                Application.OpenURL(consoleUrl);
            }

            GUILayout.Space(20);

            GUILayout.Label("Quick Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Make Test Purchase"))
                InvoSDKTestPurchaseWindow.ShowWindow();

            if (GUILayout.Button("Get Player Balances"))
                InvoSDKPlayerBalanceWindow.ShowWindow();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// A second InvoSDKConfig in another Resources root makes Resources.Load pick an
        /// arbitrary one — including, in a cloned repo, one that still carries someone
        /// else's key. Say so loudly and help the user find it.
        /// </summary>
        private void DrawStrayConfigWarning()
        {
            var stray = AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(StrayConfigAssetPath);
            if (stray == null)
                return;

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "DUPLICATE CONFIG DETECTED\n\n" +
                "A second InvoSDKConfig exists at " + StrayConfigAssetPath + ".\n" +
                "Two assets with the same name in two Resources roots means Resources.Load " +
                "picks one of them unpredictably — your game may run against the wrong key.\n\n" +
                "Delete the stray asset. The wizard only edits " + ConfigAssetPath + ".",
                MessageType.Error);

            if (GUILayout.Button("Select stray asset so I can delete it"))
            {
                Selection.activeObject = stray;
                EditorGUIUtility.PingObject(stray);
            }
        }

        /// <summary>Production with no game server: every game-secret call is refused at runtime.</summary>
        private void DrawMissingGameServerWarning()
        {
            if (!config.useProduction || !string.IsNullOrWhiteSpace(config.gameServerUrl))
                return;

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "PRODUCTION HAS NO GAME SERVER\n\n" +
                "The SDK refuses to send the game secret from a production build, so sends, transfers, " +
                "purchases and phone approvals will fail until Game Server URL is set.",
                MessageType.Warning);
        }

        /// <summary>Production key + client build is the exact failure this SDK must not enable.</summary>
        private void DrawProductionKeyWarning()
        {
            if (!config.useProduction || string.IsNullOrEmpty(config.sdkKey))
                return;

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "PRODUCTION IS ENABLED WITH A KEY SET\n\n" +
                "This configuration ships a production secret inside your player build, where " +
                "anyone can read it out of the built assets and spend against your account.\n\n" +
                "Either clear the SDK Key and route production traffic through your own server, " +
                "or turn Use Production off and keep a sandbox key here.",
                MessageType.Error);
        }
    }
}
