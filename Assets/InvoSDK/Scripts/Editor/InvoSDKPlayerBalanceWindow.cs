using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace InvoSDK.Editor
{
    /// <summary>
    /// Editor-only balance lookup. It issues its own request rather than going through
    /// APIManager.Instance: that singleton is assigned in Awake(), so it is null outside
    /// Play Mode and calling it from an editor window throws a NullReferenceException.
    /// </summary>
    public class InvoSDKPlayerBalanceWindow : EditorWindow
    {
        private string playerEmail = "";

        private bool isSending;
        private string resultTitle;
        private string resultBody;
        private MessageType resultType = MessageType.Info;
        private Vector2 resultScroll;

        [MenuItem("InvoSDK/Check Player Balance", priority = 11)]
        public static void ShowWindow()
        {
            var window = GetWindow<InvoSDKPlayerBalanceWindow>("InvoSDK - Player Balance");
            window.minSize = new Vector2(500, 300);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Check Player Balance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Editor-only tool. Works in Edit Mode: it calls the API directly with your game " +
                "secret instead of using the runtime APIManager, which only exists in Play Mode.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(isSending))
            {
                playerEmail = EditorGUILayout.TextField("Player Email", playerEmail);

                GUILayout.Space(10);

                if (GUILayout.Button(isSending ? "Fetching..." : "Get Balance"))
                    _ = GetBalanceAsync();
            }

            if (!string.IsNullOrEmpty(resultTitle))
            {
                GUILayout.Space(10);
                GUILayout.Label(resultTitle, EditorStyles.boldLabel);
                resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.MinHeight(100));
                EditorGUILayout.HelpBox(resultBody, resultType);
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>Mirrors APIManager's environment routing; the singleton is unavailable in edit mode.</summary>
        private static string GetApiBase(InvoSDKConfig cfg) =>
            cfg != null && cfg.useProduction
                ? "https://invo.network/api"
                : "https://sandbox.invo.network/sandbox/api";

        private static InvoSDKConfig LoadConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(InvoSDKSetupWizard.ConfigAssetPath);
            return cfg != null ? cfg : Resources.Load<InvoSDKConfig>("InvoSDKConfig");
        }

        private async Task GetBalanceAsync()
        {
            if (isSending) return;

            var cfg = LoadConfig();
            string sdkKey = cfg != null ? cfg.sdkKey : null;

            if (string.IsNullOrEmpty(sdkKey))
            {
                SetResult("Not configured", "SDK Key not set. Configure it in InvoSDK > Setup Wizard.", MessageType.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(playerEmail))
            {
                SetResult("Missing email", "Enter the player's email address.", MessageType.Error);
                return;
            }

            string url = $"{GetApiBase(cfg)}/player-balances/player/by-email/{UnityWebRequest.EscapeURL(playerEmail.Trim())}";

            isSending = true;
            SetResult("Fetching", "GET " + url, MessageType.Info);

            try
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.SetRequestHeader("X-Game-Secret-Key", sdkKey);

                    var op = request.SendWebRequest();
                    while (!op.isDone) await Task.Yield();

                    string body = request.downloadHandler != null ? request.downloadHandler.text : "";

                    if (request.result != UnityWebRequest.Result.Success)
                        SetResult($"Failed (HTTP {request.responseCode})", $"{request.error}\n\n{body}", MessageType.Error);
                    else
                        SetResult("Balance retrieved", body, MessageType.Info);
                }
            }
            catch (System.Exception ex)
            {
                SetResult("Request error", ex.Message, MessageType.Error);
            }
            finally
            {
                isSending = false;
                Repaint();
            }
        }

        private void SetResult(string title, string body, MessageType type)
        {
            resultTitle = title;
            resultBody = body;
            resultType = type;
            Repaint();
        }
    }
}
