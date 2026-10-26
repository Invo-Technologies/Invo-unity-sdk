using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace InvoSDK.Editor
{
    /// <summary>
    /// Tools > Invo SDK > Settings. Sandbox and Production are separate INVO environments with separate Game IDs
    /// and separate keys, so each has its own tab. Only the Sandbox tab holds keys: INVO's docs say a
    /// key must never ship in a Unity build, so production calls go through your game server, which
    /// holds one production key per storefront. <see cref="InvoBuildGuard"/> enforces that at build time.
    /// </summary>
    public class InvoSDKSetupWizard : EditorWindow
    {
        /// <summary>
        /// The one and only location of the config asset. A second asset named
        /// InvoSDKConfig under any other Resources root makes Resources.Load resolution
        /// undefined, so the window always reads and writes exactly this path.
        /// </summary>
        public const string ConfigAssetPath = "Assets/InvoSDK/Resources/InvoSDKConfig.asset";
        private const string ConfigFolder = "Assets/InvoSDK/Resources";
        private const string StrayConfigAssetPath = "Assets/Resources/InvoSDKConfig.asset";

        private const string ReadmeProxySectionUrl =
            "https://github.com/Invo-Technologies/Invo-unity-sdk#server-side-proxy";
        private const string SandboxConsoleUrl = "https://console.invo.network/test";
        private const string ProductionConsoleUrl = "https://console.invo.network";

        private static readonly InvoStorefront[] DefaultStorefronts =
        {
            InvoStorefront.Steam, InvoStorefront.GooglePlay, InvoStorefront.AppStore,
            InvoStorefront.PlayStation, InvoStorefront.Xbox, InvoStorefront.NintendoSwitch
        };

        private static readonly string[] Tabs = { "Sandbox", "Production", "Game & display" };

        private InvoSDKConfig config;
        private Vector2 scroll;
        private int tab;
        private bool revealKeys;
        private readonly Dictionary<string, string> testResults = new Dictionary<string, string>();
        private UnityWebRequest pendingTest;
        private string pendingTestKey;

        [MenuItem("Tools/Invo SDK/Settings", priority = 0)]
        [MenuItem("Tools/Invo SDK/Setup Wizard", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<InvoSDKSetupWizard>("Invo SDK Settings");
            window.minSize = new Vector2(640, 560);
            window.Show();
        }

        private void OnEnable()
        {
            config = LoadOrCreateConfig();
            tab = config != null && config.useProduction ? 1 : 0;
            EnsureDefaultStorefronts();
        }

        private void OnDisable()
        {
            pendingTest?.Abort();
            pendingTest?.Dispose();
            pendingTest = null;
        }

        /// <summary>Loads the canonical config asset, creating it if it does not exist yet.</summary>
        public static InvoSDKConfig LoadOrCreateConfig()
        {
            var existing = FindConfig();
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

        /// <summary>The canonical config asset, or null. Never creates one.</summary>
        public static InvoSDKConfig FindConfig() => AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(ConfigAssetPath);

        private void EnsureDefaultStorefronts()
        {
            if (config == null)
                return;
            if (config.sandboxKeys == null)
                config.sandboxKeys = new List<InvoStorefrontKey>();
            bool changed = false;
            foreach (InvoStorefront store in DefaultStorefronts)
            {
                if (config.sandboxKeys.Any(k => k != null && k.storefront == store))
                    continue;
                config.sandboxKeys.Add(new InvoStorefrontKey { storefront = store });
                changed = true;
            }
            if (config.storefrontMap == null || config.storefrontMap.Count == 0)
            {
                config.storefrontMap = InvoSDKConfig.DefaultStorefrontMap();
                changed = true;
            }
            if (changed)
                Save();
        }

        // ------------------------------------------------------------------ layout

        private void OnGUI()
        {
            if (config == null)
                config = LoadOrCreateConfig();

            DrawHeader();
            DrawStrayConfigWarning();
            tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(26));
            GUILayout.Space(8);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            switch (tab)
            {
                case 0: DrawSandboxTab(); break;
                case 1: DrawProductionTab(); break;
                default: DrawDisplayTab(); break;
            }
            if (EditorGUI.EndChangeCheck())
                Save();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Play Mode and builds use", EditorStyles.miniLabel, GUILayout.Width(140));
                int env = config.useProduction ? 1 : 0;
                int picked = EditorGUILayout.Popup(env, new[] { "Sandbox", "Production" }, EditorStyles.toolbarPopup, GUILayout.Width(110));
                if (picked != env)
                {
                    config.useProduction = picked == 1;
                    tab = picked;
                    Save();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Docs", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    Application.OpenURL("https://docs.invo.network/docs/unity-sdk");
            }
        }

        // ------------------------------------------------------------------ sandbox

        private void DrawSandboxTab()
        {
            EditorGUILayout.HelpBox(
                "Sandbox keys are used in Play Mode and Development builds only. The build check strips " +
                "them, and the test player, from every production build and every build without " +
                "Development Build ticked.",
                MessageType.Info);

            DrawMigration();

            GUILayout.Label("Sandbox game", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                config.sandboxGameId = EditorGUILayout.TextField("Sandbox Game ID", config.sandboxGameId);
                using (new EditorGUI.DisabledScope(pendingTest != null || string.IsNullOrWhiteSpace(config.sandboxGameId)))
                {
                    if (GUILayout.Button("Test connection", GUILayout.Width(120)))
                        StartTest("sandbox", APIManager.SandboxApiBase, config.sandboxGameId,
                                  config.SandboxKeyFor(config.ResolveStorefront(InvoBuildPlatform.Desktop)), null);
                }
            }
            DrawTestResult("sandbox");

            GUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Sandbox keys, one per storefront", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                revealKeys = GUILayout.Toggle(revealKeys, "Show keys", GUILayout.Width(90));
                if (GUILayout.Button("Get keys", GUILayout.Width(80)))
                    Application.OpenURL(SandboxConsoleUrl);
            }

            InvoStorefrontKey remove = null;
            foreach (InvoStorefrontKey entry in config.sandboxKeys)
            {
                if (entry == null)
                    continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(InvoStorefrontResolver.DisplayName(entry.storefront), GUILayout.Width(130));
                    entry.sdkKey = revealKeys
                        ? EditorGUILayout.TextField(entry.sdkKey)
                        : EditorGUILayout.PasswordField(entry.sdkKey);
                    GUILayout.Label(new GUIContent(KeyStatus(entry.sdkKey),
                        "Sandbox keys look like ivsdk_test_...; older ivsdk_... keys still work."), GUILayout.Width(90));
                    string testId = "key:" + entry.storefront;
                    using (new EditorGUI.DisabledScope(pendingTest != null || string.IsNullOrWhiteSpace(entry.sdkKey) ||
                                                       string.IsNullOrWhiteSpace(config.sandboxGameId)))
                    {
                        if (GUILayout.Button("Test", GUILayout.Width(50)))
                            StartTest(testId, APIManager.SandboxApiBase, config.sandboxGameId, entry.sdkKey.Trim(), null);
                    }
                    if (!DefaultStorefronts.Contains(entry.storefront) && GUILayout.Button("−", GUILayout.Width(22)))
                        remove = entry;
                }
                if (IsProductionKey(entry.sdkKey))
                    EditorGUILayout.HelpBox(
                        "This is a production key (ivsdk_live_). Sandbox keys start with ivsdk_test_. Production keys " +
                        "belong on your game server only, never in the project.",
                        MessageType.Error);
                DrawTestResult("key:" + entry.storefront);
            }
            if (remove != null)
            {
                config.sandboxKeys.Remove(remove);
                GUI.changed = true;
            }
            if (GUILayout.Button("+ Add storefront", GUILayout.Width(140)))
                ShowAddStorefrontMenu();

            GUILayout.Space(10);
            DrawStorefrontMap();

            GUILayout.Space(10);
            GUILayout.Label("Test player (sandbox only)", EditorStyles.boldLabel);
            config.playerEmail = EditorGUILayout.TextField("Email", config.playerEmail);
            config.playerPhone = EditorGUILayout.TextField(new GUIContent("Phone", "International format, e.g. +15555550111."), config.playerPhone);
            if (!string.IsNullOrWhiteSpace(config.playerPhone) && InvoPhone.Normalize(config.playerPhone) == null)
                EditorGUILayout.HelpBox(InvoPhone.DescribeProblem(config.playerPhone), MessageType.Warning);
            config.playerName = EditorGUILayout.TextField("Name", config.playerName);
            config.showSandboxWatermark = EditorGUILayout.Toggle(
                new GUIContent("SANDBOX watermark", "Shows a small SANDBOX label in development builds."),
                config.showSandboxWatermark);

            GUILayout.Space(10);
            GUILayout.Label("Quick actions", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Make test purchase"))
                    InvoSDKTestPurchaseWindow.ShowWindow();
                if (GUILayout.Button("Get player balances"))
                    InvoSDKPlayerBalanceWindow.ShowWindow();
            }
        }

        private void DrawMigration()
        {
            bool legacyKey = !string.IsNullOrWhiteSpace(config.sdkKey) &&
                             !config.sandboxKeys.Any(k => k != null && !string.IsNullOrWhiteSpace(k.sdkKey));
            bool legacyId = !string.IsNullOrWhiteSpace(config.gameId) && string.IsNullOrWhiteSpace(config.sandboxGameId);
            if (!legacyKey && !legacyId)
                return;
            EditorGUILayout.HelpBox(
                "This config comes from SDK 3.0, which had one key and one Game ID. They still work. " +
                "Move them into the Sandbox tab so each storefront can have its own key.",
                MessageType.Warning);
            if (GUILayout.Button("Move the 3.0 key and Game ID into Sandbox"))
            {
                if (legacyId)
                    config.sandboxGameId = config.gameId;
                if (legacyKey)
                {
                    InvoStorefront store = config.ResolveStorefront(InvoBuildPlatform.Desktop);
                    InvoStorefrontKey entry = config.sandboxKeys.FirstOrDefault(k => k != null && k.storefront == store);
                    if (entry == null)
                        config.sandboxKeys.Add(entry = new InvoStorefrontKey { storefront = store });
                    entry.sdkKey = config.sdkKey;
                    config.sdkKey = string.Empty;
                }
                GUI.changed = true;
            }
        }

        private void DrawStorefrontMap()
        {
            GUILayout.Label("Which storefront each build target uses", EditorStyles.boldLabel);
            foreach (InvoStorefrontMapping row in config.storefrontMap)
            {
                if (row == null)
                    continue;
                row.storefront = (InvoStorefront)EditorGUILayout.EnumPopup(PlatformLabel(row.platform), row.storefront);
            }
            EditorGUILayout.HelpBox(
                "The SDK picks the key for the platform it runs on, and tells your game server the storefront " +
                "in production. Selling Windows on both Steam and Epic? Add the scripting define " +
                "INVO_STOREFRONT_EPICGAMESSTORE to the Epic build.",
                MessageType.None);
        }

        private void ShowAddStorefrontMenu()
        {
            var menu = new GenericMenu();
            foreach (InvoStorefront store in Enum.GetValues(typeof(InvoStorefront)))
            {
                if (config.sandboxKeys.Any(k => k != null && k.storefront == store))
                    continue;
                InvoStorefront captured = store;
                menu.AddItem(new GUIContent(InvoStorefrontResolver.DisplayName(store)), false, () =>
                {
                    config.sandboxKeys.Add(new InvoStorefrontKey { storefront = captured });
                    Save();
                });
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("Every storefront is listed"));
            menu.ShowAsContext();
        }

        // ------------------------------------------------------------------ production

        private void DrawProductionTab()
        {
            EditorGUILayout.HelpBox(
                "No keys on this tab, by design. INVO's docs say an SDK key must never go into a Unity build. " +
                "Your game server holds one production key per storefront and attaches it; the game only " +
                "knows your server's address.",
                MessageType.Info);
            if (GUILayout.Button("Read: setting up the server-side proxy", GUILayout.Width(280)))
                Application.OpenURL(ReadmeProxySectionUrl);

            GUILayout.Space(6);
            config.productionGameId = EditorGUILayout.TextField("Production Game ID", config.productionGameId);
            using (new EditorGUILayout.HorizontalScope())
            {
                config.gameServerUrl = EditorGUILayout.TextField(
                    new GUIContent("Game Server URL", "Base URL of your server, e.g. https://api.yourgame.com."),
                    config.gameServerUrl);
                using (new EditorGUI.DisabledScope(pendingTest != null || string.IsNullOrWhiteSpace(config.gameServerUrl)))
                {
                    if (GUILayout.Button("Test connection", GUILayout.Width(120)))
                        StartTest("production", config.gameServerUrl.TrimEnd('/') + "/api",
                                  config.productionGameId, null, InvoStorefrontResolver.WireName(config.ResolveStorefront(InvoBuildPlatform.Desktop)));
                }
            }
            DrawTestResult("production");
            config.checkoutSessionEndpoint = EditorGUILayout.TextField(
                new GUIContent("Checkout session endpoint", "Your endpoint that mints a hosted-checkout session and returns { checkout_url }."),
                config.checkoutSessionEndpoint);

            EditorGUILayout.HelpBox(
                "Every request to your server carries X-Invo-Storefront (steam, google_play, app_store, playstation, " +
                "xbox, nintendo_switch, ...) so it can attach that storefront's production key. It uses the same " +
                "build-target mapping as the Sandbox tab.",
                MessageType.None);

            GUILayout.Space(10);
            GUILayout.Label("Production build check", EditorStyles.boldLabel);
            List<string> problems = InvoBuildGuard.ProductionProblems(config);
            if (problems.Count == 0)
                EditorGUILayout.HelpBox("Ready. Production builds will also strip every sandbox key and the test player.", MessageType.Info);
            foreach (string problem in problems)
                EditorGUILayout.HelpBox(problem, MessageType.Error);

            GUILayout.Space(6);
            if (GUILayout.Button("Manage and rotate production keys in the console", GUILayout.Width(340)))
                Application.OpenURL(ProductionConsoleUrl);
        }

        // ------------------------------------------------------------------ display

        private void DrawDisplayTab()
        {
            EditorGUILayout.HelpBox("Shown in the SDK panels. Test connection on the Sandbox tab fills the icon in for you.", MessageType.None);
            config.gameName = EditorGUILayout.TextField("Game name", config.gameName);
            config.gameIconUrl = EditorGUILayout.TextField("Game icon URL", config.gameIconUrl);
            config.gameCurrencyName = EditorGUILayout.TextField("Currency name", config.gameCurrencyName);
            config.gameCurrencyUrl = EditorGUILayout.TextField("Currency icon URL", config.gameCurrencyUrl);
            config.gameVersion = EditorGUILayout.TextField("Game version", config.gameVersion);
        }

        // ------------------------------------------------------------------ test connection

        /// <summary>
        /// Calls available-destinations, the cheapest call that proves the Game ID and key (sandbox) or
        /// your server's proxy (production) work. Nothing is written to INVO.
        /// </summary>
        private void StartTest(string id, string apiBase, string gameId, string key, string storefrontHeader)
        {
            testResults[id] = "Testing...";
            string body = new JObject { ["source_game_id"] = gameId?.Trim() ?? string.Empty }.ToString();
            var req = new UnityWebRequest(apiBase + "/transfers/available-destinations", "POST")
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 20
            };
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(key))
                req.SetRequestHeader("X-Game-Secret-Key", key);
            if (!string.IsNullOrEmpty(storefrontHeader))
                req.SetRequestHeader("X-Invo-Storefront", storefrontHeader);
            pendingTest = req;
            pendingTestKey = id;
            req.SendWebRequest();
            EditorApplication.update += PollTest;
        }

        private void PollTest()
        {
            if (pendingTest == null)
            {
                EditorApplication.update -= PollTest;
                return;
            }
            if (!pendingTest.isDone)
                return;
            EditorApplication.update -= PollTest;

            string result;
            if (pendingTest.result == UnityWebRequest.Result.Success)
            {
                int count = 0;
                try
                {
                    JObject json = JObject.Parse(pendingTest.downloadHandler.text);
                    count = json["available_games"] is JArray games ? games.Count : 0;
                    string icon = (string)json["source_game_icon"];
                    if (!string.IsNullOrEmpty(icon) && string.IsNullOrWhiteSpace(config.gameIconUrl))
                    {
                        config.gameIconUrl = icon;
                        Save();
                    }
                }
                catch (Exception)
                {
                    // A 2xx with an unexpected body still proves the route and key.
                }
                result = $"Connected. {count} game(s) can receive transfers.";
            }
            else
            {
                result = $"Failed: HTTP {pendingTest.responseCode}. " +
                         DescribeFailure(pendingTest.responseCode, pendingTest.downloadHandler?.text);
            }
            testResults[pendingTestKey] = result;
            pendingTest.Dispose();
            pendingTest = null;
            Repaint();
        }

        internal static string DescribeFailure(long code, string body)
        {
            if (ErrorCodeOf(body) == InvoApiException.KeyForOtherEnvironmentCode)
                return InvoApiException.KeyForOtherEnvironmentMessage;
            switch (code)
            {
                case 0: return "No response: check the URL and your network.";
                case 401: return "The key was refused. Check it is a key for this environment and storefront.";
                case 403: return "This title may not send transfers. Check the console.";
                case 404: return "Route not found. For production, check your server forwards /api/* to INVO.";
                default: return "See the INVO console for this title.";
            }
        }

        private void DrawTestResult(string id)
        {
            if (!testResults.TryGetValue(id, out string text))
                return;
            var style = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            style.normal.textColor = text.StartsWith("Connected") ? new Color(0.45f, 0.8f, 0.6f)
                : text.StartsWith("Failed") ? new Color(0.95f, 0.5f, 0.45f) : style.normal.textColor;
            EditorGUILayout.LabelField(text, style);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The <c>error_code</c> (or <c>code</c>) of an error body; null when there is none or it is not JSON.</summary>
        private static string ErrorCodeOf(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                JObject json = JObject.Parse(body);
                JToken code = json["error_code"] ?? json["code"];
                return code != null && code.Type == JTokenType.String ? (string)code : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Format check for a sandbox key slot. New keys are ivsdk_test_ (sandbox) or ivsdk_live_
        /// (production); older ivsdk_ keys have no environment in the prefix and keep working.
        /// </summary>
        internal static string KeyStatus(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "Not set";
            string trimmed = key.Trim();
            if (trimmed != key) return "Has spaces";
            if (IsProductionKey(trimmed)) return "Live key";
            return trimmed.StartsWith("ivsdk_", StringComparison.Ordinal) ? "Looks valid" : "Not ivsdk_";
        }

        internal static bool IsProductionKey(string key)
        {
            return key != null && key.Trim().StartsWith("ivsdk_live_", StringComparison.Ordinal);
        }

        private static string PlatformLabel(InvoBuildPlatform platform)
        {
            switch (platform)
            {
                case InvoBuildPlatform.Desktop: return "Windows, macOS, Linux";
                case InvoBuildPlatform.PlayStation: return "PlayStation 4/5";
                case InvoBuildPlatform.Xbox: return "Xbox";
                case InvoBuildPlatform.Switch: return "Nintendo Switch";
                default: return platform.ToString();
            }
        }

        private void Save()
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// A second InvoSDKConfig in another Resources root makes Resources.Load pick an
        /// arbitrary one, including, in a cloned repo, one that still carries someone
        /// else's key. Say so loudly and help the user find it.
        /// </summary>
        private void DrawStrayConfigWarning()
        {
            var stray = AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(StrayConfigAssetPath);
            if (stray == null)
                return;

            EditorGUILayout.HelpBox(
                "DUPLICATE CONFIG DETECTED\n\n" +
                "A second InvoSDKConfig exists at " + StrayConfigAssetPath + ". Resources.Load picks one of the two " +
                "unpredictably, so your game may run against the wrong key. Delete the stray asset; this window " +
                "only edits " + ConfigAssetPath + ".",
                MessageType.Error);
            if (GUILayout.Button("Select stray asset so I can delete it"))
            {
                Selection.activeObject = stray;
                EditorGUIUtility.PingObject(stray);
            }
        }
    }
}
