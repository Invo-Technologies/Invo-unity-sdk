using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Unity.Plastic.Newtonsoft.Json;

namespace InvoSDK.Editor
{
    public class InvoSDKTestPurchaseWindow : EditorWindow
    {
        private string playerEmail = "";
        private string playerName = "";
        private string playerPhone = "";
        private string usdAmount = "10.00";
        private string purchaseReference = "";
        private int selectedPaymentIndex = 0;

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
            window.minSize = new Vector2(500, 350);
            window.Show();
        }

        private async void OnGUI()
        {
            GUILayout.Label("Test Currency Purchase", EditorStyles.boldLabel);

            playerEmail = EditorGUILayout.TextField("Player Email", playerEmail);
            playerName = EditorGUILayout.TextField("Player Name", playerName);
            playerPhone = EditorGUILayout.TextField("Player Phone", playerPhone);
            usdAmount = EditorGUILayout.TextField("USD Amount", usdAmount);

            GUILayout.Space(5);

            // Purchase Reference with auto-generate option
            EditorGUILayout.BeginHorizontal();
            purchaseReference = EditorGUILayout.TextField("Purchase Reference", purchaseReference);
            if (GUILayout.Button("Generate", GUILayout.MaxWidth(100)))
            {
                purchaseReference = "purchase_" + System.Guid.NewGuid().ToString("N").Substring(0, 12);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Payment Method dropdown
            selectedPaymentIndex = EditorGUILayout.Popup("Payment Method", selectedPaymentIndex, paymentMethods);

            GUILayout.Space(15);

            if (GUILayout.Button("Send Test Purchase", GUILayout.Height(30)))
            {
                await SendTestPurchase();
            }
        }

        private async Task SendTestPurchase()
        {
            // Prefer configuration from InvoSDKConfig asset; fall back to EditorPrefs if not available
            var cfg = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            string sdkKey = cfg != null && !string.IsNullOrEmpty(cfg.sdkKey)
                ? cfg.sdkKey
                : EditorPrefs.GetString("InvoSDK_SDKKey", "");

            if (string.IsNullOrEmpty(sdkKey))
            {
                EditorUtility.DisplayDialog("Error", "SDK Key not set. Please configure in Setup Wizard.", "OK");
                return;
            }

            // Choose API base depending on environment. Sandbox uses /sandbox prefix per updated routing.
            string apiBase = cfg != null && cfg.useProduction
                ? "https://invo.network/api"
                : "https://sandbox.invo.network/sandbox/api";

            string url = $"{apiBase}/currency-purchases/purchase-currency";

            string finalReference = string.IsNullOrEmpty(purchaseReference)
                ? "purchase_" + System.Guid.NewGuid().ToString("N").Substring(0, 12)
                : purchaseReference;

            var payload = new
            {
                player_email = playerEmail,
                player_name = playerName,
                player_phone = playerPhone,
                usd_amount = usdAmount,
                purchase_reference = finalReference,
                payment_method_id = paymentMethods[selectedPaymentIndex],
                save_card = true
            };

            string json = JsonConvert.SerializeObject(payload);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("X-Game-Secret-Key", sdkKey);
                request.SetRequestHeader("Content-Type", "application/json");

                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    EditorUtility.DisplayDialog("Success", "Response: " + request.downloadHandler.text, "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Error: " + request.error + "\nResponse: " + request.downloadHandler.text, "OK");
                }
            }
        }
    }
}
