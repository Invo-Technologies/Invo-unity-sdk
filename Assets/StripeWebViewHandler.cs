using UnityEngine;

public class StripeWebViewHandler : MonoBehaviour
{
    private WebViewObject webViewObject;
    private const string stripeWebPageUrl = "https://your-node-stripe-server.com/checkout.html";

    private void Start()
    {
        webViewObject = gameObject.AddComponent<WebViewObject>();
        webViewObject.Init(
            cb: OnWebMessageReceived,
            err: msg => Debug.LogError("[Stripe WebView] Error: " + msg),
            started: msg => Debug.Log("[Stripe WebView] Started: " + msg),
            ld: msg => Debug.Log("[Stripe WebView] Loaded: " + msg)
        );

        // Fill entire screen
        webViewObject.SetMargins(0, 0, 0, 0);
        webViewObject.SetVisibility(true);
        webViewObject.LoadURL(stripeWebPageUrl);
    }

    private void OnWebMessageReceived(string message)
    {
        Debug.Log($"[Stripe WebView] Message: {message}");
        if (message.StartsWith("pm_")) // Stripe payment method ID
        {
            StripePaymentManager.Instance.OnPaymentMethodReceived(message);
            CloseWebView();
        }
        else if (message == "cancel")
        {
            Debug.Log("[Stripe WebView] User cancelled payment.");
            CloseWebView();
        }
    }

    private void CloseWebView()
    {
        if (webViewObject != null)
        {
            webViewObject.SetVisibility(false);
            Destroy(webViewObject);
        }
        Destroy(this);
    }
}