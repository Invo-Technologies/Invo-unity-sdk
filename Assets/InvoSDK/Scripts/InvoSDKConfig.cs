using UnityEngine;

namespace InvoSDK
{
    [CreateAssetMenu(fileName = "InvoSDKConfig", menuName = "InvoSDK/Config", order = 1)]
    public class InvoSDKConfig : ScriptableObject
    {
        [Header("Credentials — SANDBOX ONLY")]
        [Tooltip("Your game secret key. Anything stored here is compiled into the player build " +
                 "and can be extracted from it, so only ever put a SANDBOX key in this field. " +
                 "A production key belongs on your own server, behind a proxy. " +
                 "See the 'Server-side proxy' section of the README.")]
        public string sdkKey;

        public string gameId;

        [Header("Presentation")]
        public string gameName;
        public string playerName;
        public string gameIconUrl;
        public string gameCurrencyName;
        public string gameCurrencyUrl;
        public string gameCurrencyID;
        public string gameVersion;

        [Header("Player")]
        public string playerEmail;
        public string playerPhone;

        [Header("Environment")]
        [Tooltip("When enabled the SDK talks to production. Never enable this in a build that " +
                 "carries an sdkKey — that ships a production secret to every player.")]
        public bool useProduction;

        [Header("Hosted Checkout")]
        [Tooltip("URL of YOUR OWN server endpoint that mints a hosted-checkout session.\n\n" +
                 "Real-money purchases cannot be made from the client: the Invo secret must stay " +
                 "server-side. The SDK POSTs { player_email, usd_amount } to this URL and expects " +
                 "{ checkout_url } back, then opens that URL. Your endpoint is the only place that " +
                 "should ever hold a production key.")]
        public string checkoutSessionEndpoint;
    }
}
