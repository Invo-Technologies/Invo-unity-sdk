using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Unity.Plastic.Newtonsoft.Json;

namespace InvoSDK.Editor
{
    /// <summary>
    /// Editor-only purchase probe. It sends the game secret in <c>X-Game-Secret-Key</c>, which is
    /// correct here because this window never ships — it exists only inside the Unity Editor.
    /// Runtime code must not copy this pattern for real-money flows.
    /// </summary>
    public class InvoSDKTestPurchaseWindow : EditorWindow
    {
        private string playerEmail = "";
        private string playerName = "";
        private string playerPhone = "";
        private string usdAmount = "10.00";
        private string purchaseReference = "";
        private int selectedPaymentIndex = 0;

        // Request state. OnGUI only renders these; it never awaits.
        private bool isSending;
        private string resultTitle;
        private string resultBody;
        private MessageType resultType = MessageType.Info;
        private Vector2 resultScroll;

        // Common Stripe test methods for convenience
        private readonly string[] paymentMethods = new string[]
        {
            "pm_card_visa",
            "pm_card_mastercard",
            "pm_card_amex",
            "pm_card_discover"
        };

        [MenuItem("InvoSDK/Test Purchase", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<InvoSDKTestPurchaseWindow>("InvoSDK Test Purchase");
            window.minSize = new Vector2(500, 420);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Test Currency Purchase", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Editor-only tool. It calls Invo directly with your game secret, which is safe " +
                "here because this code never ships in a build.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(isSending))
            {
                playerEmail = EditorGUILayout.TextField("Player Email", playerEmail);
                playerName = EditorGUILayout.TextField("Player Name", playerName);
                playerPhone = EditorGUILayout.TextField("Player Phone", playerPhone);
                usdAmount = EditorGUILayout.TextField("USD Amount", usdAmount);

                GUILayout.Space(5);

                // Purchase Reference with auto-generate option. The backend REQUIRES this field.
                EditorGUILayout.BeginHorizontal();
                purchaseReference = EditorGUILayout.TextField("Purchase Reference", purchaseReference);
                if (GUILayout.Button("Generate", GUILayout.MaxWidth(100)))
                    purchaseReference = NewReference();
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                selectedPaymentIndex = EditorGUILayout.Popup("Payment Method", selectedPaymentIndex, paymentMethods);

                GUILayout.Space(15);

                if (GUILayout.Button(isSending ? "Sending..." : "Send Test Purchase", GUILayout.Height(30)))
                {
                    // Fire and forget: the task writes back into the fields above and repaints.
                    _ = SendTestPurchaseAsync();
                }
            }

            if (!string.IsNullOrEmpty(resultTitle))
            {
                GUILayout.Space(10);
                GUILayout.Label(resultTitle, EditorStyles.boldLabel);
                resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.MinHeight(120));
                EditorGUILayout.HelpBox(resultBody, resultType);
                EditorGUILayout.EndScrollView();
            }
        }

        private static string NewReference() =>
            "purchase_" + System.Guid.NewGuid().ToString("N").Substring(0, 12);

        /// <summary>
        /// Mirrors APIManager's environment routing. The runtime singleton does not exist in
        /// edit mode, so the two literals are duplicated here deliberately and in one place only.
        /// </summary>
        private static string GetApiBase(InvoSDKConfig cfg) =>
            cfg != null && cfg.useProduction
                ? "https://invo.network/api"
                : "https://sandbox.invo.network/sandbox/api";

        private static InvoSDKConfig LoadConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(InvoSDKSetupWizard.ConfigAssetPath);
            return cfg != null ? cfg : Resources.Load<InvoSDKConfig>("InvoSDKConfig");
        }

        private async Task SendTestPurchaseAsync()
        {
            if (isSending) return;

            var cfg = LoadConfig();
            string sdkKey = cfg != null ? cfg.sdkKey : null;

            if (string.IsNullOrEmpty(sdkKey))
            {
                SetResult("Not configured", "SDK Key not set. Configure it in InvoSDK > Setup Wizard.", MessageType.Error);
                return;
            }

            if (!InvoFormat.TryParseAmount(usdAmount, out decimal amount) || amount <= 0m)
            {
                SetResult("Invalid amount", $"'{usdAmount}' is not a valid USD amount.", MessageType.Error);
                return;
            }

            string url = $"{GetApiBase(cfg)}/currency-purchases/purchase-currency";
            string finalReference = string.IsNullOrEmpty(purchaseReference) ? NewReference() : purchaseReference;

            var payload = new
            {
                player_email = playerEmail,
                player_name = playerName,
                player_phone = playerPhone,
                usd_amount = InvoFormat.Amount(amount),
                purchase_reference = finalReference,
                payment_method_id = paymentMethods[selectedPaymentIndex],
                save_card = true
            };

            string json = JsonConvert.SerializeObject(payload);

            isSending = true;
            SetResult("Sending", $"POST {url}", MessageType.Info);

            try
            {
                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("X-Game-Secret-Key", sdkKey);
                    request.SetRequestHeader("Content-Type", "application/json");

                    var op = request.SendWebRequest();
                    while (!op.isDone) await Task.Yield();

                    string body = request.downloadHandler != null ? request.downloadHandler.text : "";

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        SetResult($"Failed (HTTP {request.responseCode})",
                                  $"{request.error}\n\nReference: {finalReference}\n\n{body}",
                                  MessageType.Error);
                        return;
                    }

                    // A 200 does not mean the money moved: "requires_action" means Stripe wants
                    // 3D Secure and NOTHING has been charged yet. Reporting that as success is
                    // how a test run ends up believing a purchase completed when it did not.
                    string status = ReadStatus(body);
                    if (status == "requires_action")
                    {
                        SetResult("Action required — NOT charged",
                                  "The payment needs 3D Secure authentication. No money has been " +
                                  "charged and no currency has been credited.\n\n" +
                                  "Complete the authentication in a real checkout flow, or pick a " +
                                  "test card that does not trigger 3DS (pm_card_visa).\n\n" +
                                  $"Reference: {finalReference}\n\n{body}",
                                  MessageType.Warning);
                        return;
                    }

                    SetResult($"Success (status: {status ?? "n/a"})",
                              $"Reference: {finalReference}\n\n{body}",
                              MessageType.Info);
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

        /// <summary>Reads the top-level "status" field, tolerating any other response shape.</summary>
        private static string ReadStatus(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                var parsed = JsonConvert.DeserializeObject<StatusEnvelope>(body);
                return parsed != null ? parsed.status : null;
            }
            catch
            {
                return null;
            }
        }

        private void SetResult(string title, string body, MessageType type)
        {
            resultTitle = title;
            resultBody = body;
            resultType = type;
            Repaint();
        }

        [System.Serializable]
        private class StatusEnvelope
        {
            public string status;
        }
    }
}
