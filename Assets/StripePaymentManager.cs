using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class StripePaymentManager : MonoBehaviour
{
    public static StripePaymentManager Instance;

    [Header("Stripe Keys")]
    [SerializeField] private string publishableKey = "pk_test_...";
    [SerializeField] private string backendUrl = "https://your-node-stripe-server.com";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartStripeFlow(float usdAmount, string playerEmail)
    {
#if UNITY_ANDROID
        StripeAndroidBridge.CreatePaymentMethod(publishableKey, usdAmount, playerEmail);
#elif UNITY_IOS
        StripeiOSBridge_CreatePaymentMethod(publishableKey, usdAmount.ToString(), playerEmail);
#elif UNITY_EDITOR || UNITY_STANDALONE
        Debug.Log("[Stripe] Opening web fallback...");
        gameObject.AddComponent<StripeWebViewHandler>();
#else
        Debug.LogWarning("Stripe unsupported platform");
#endif
    }

    // Called from native plugin once card token created
    public async void OnPaymentMethodReceived(string paymentMethodId)
    {
        Debug.Log($"[Stripe] Received Payment Method ID: {paymentMethodId}");
        bool success = await SendPaymentMethodToInvoSDK("player@example.com", paymentMethodId, 20.00m);
        Debug.Log(success ? "Payment successful" : "Payment failed");
    }

    public async Task<bool> SendPaymentMethodToInvoSDK(string playerEmail, string paymentMethodId, decimal usdAmount)
    {
        string invoUrl = "https://invo-api.example.com/api/currency-purchases/purchase-currency";
        var payload = JsonUtility.ToJson(new
        {
            player_email = playerEmail,
            usd_amount = usdAmount.ToString("F2"),
            payment_method_id = paymentMethodId,
            save_card = true
        });

        using var req = new UnityWebRequest(invoUrl, "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes(payload);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        await req.SendWebRequest();

        return req.result == UnityWebRequest.Result.Success;
    }

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void StripeiOSBridge_CreatePaymentMethod(string publishableKey, string amount, string email);
}
