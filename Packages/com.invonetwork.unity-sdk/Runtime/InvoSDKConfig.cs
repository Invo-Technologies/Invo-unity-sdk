using System;
using System.Collections.Generic;
using UnityEngine;

namespace InvoSDK
{
    /// <summary>A store the game is sold on. INVO issues one SDK key per storefront, per environment.</summary>
    public enum InvoStorefront
    {
        Steam,
        GooglePlay,
        AppStore,
        PlayStation,
        Xbox,
        NintendoSwitch,
        EpicGamesStore,
        Web,
        Platform
    }

    /// <summary>The kind of build a player is running, used to pick the storefront.</summary>
    public enum InvoBuildPlatform
    {
        Desktop,
        Android,
        iOS,
        PlayStation,
        Xbox,
        Switch,
        WebGL
    }

    [Serializable]
    public class InvoStorefrontKey
    {
        public InvoStorefront storefront;
        [Tooltip("SANDBOX key (ivsdk_...) for this storefront. Used in Play Mode and Development builds only; release and production builds never carry it.")]
        public string sdkKey;
    }

    [Serializable]
    public class InvoStorefrontMapping
    {
        public InvoBuildPlatform platform;
        public InvoStorefront storefront;
    }

    [CreateAssetMenu(fileName = "InvoSDKConfig", menuName = "InvoSDK/Config", order = 1)]
    public class InvoSDKConfig : ScriptableObject
    {
        // ------------------------------------------------------------------ 3.1 settings
        // Edited in Tools > Invo SDK > Settings. Sandbox and production are separate INVO environments with
        // separate Game IDs and separate keys. Only the sandbox side holds keys: INVO's docs say a key
        // must never ship in a production build, so production calls go through gameServerUrl.

        [Header("Sandbox")]
        public string sandboxGameId;
        [Tooltip("One sandbox key per storefront. The SDK uses the key for the storefront the build runs on.")]
        public List<InvoStorefrontKey> sandboxKeys = new List<InvoStorefrontKey>();

        [Header("Production (no keys, by design)")]
        public string productionGameId;

        [Header("Storefronts")]
        [Tooltip("Which storefront each kind of build belongs to. Override per build with a scripting define, " +
                 "for example INVO_STOREFRONT_EPICGAMESSTORE for an Epic build of a Steam game.")]
        public List<InvoStorefrontMapping> storefrontMap = DefaultStorefrontMap();

        [Header("Development builds")]
        [Tooltip("Shows a small SANDBOX label in development builds so nobody demos against the wrong environment.")]
        public bool showSandboxWatermark = true;

        // ------------------------------------------------------------------ fields kept from 3.0

        [Header("Legacy credentials (3.0) — migrated by Tools > Invo SDK > Settings")]
        [Tooltip("3.0 single sandbox key. Still honoured when no per-storefront key is set. " +
                 "Used in Play Mode and Development builds only; never put a production key here.")]
        public string sdkKey;

        [Tooltip("3.0 single Game ID. Used when sandboxGameId / productionGameId is empty.")]
        public string gameId;

        [Header("Presentation")]
        public string gameName;
        public string playerName;
        public string gameIconUrl;
        public string gameCurrencyName;
        public string gameCurrencyUrl;
        public string gameCurrencyID;
        public string gameVersion;

        [Header("Test player (sandbox)")]
        public string playerEmail;
        public string playerPhone;

        [Header("Environment")]
        [Tooltip("When enabled the SDK talks to production. A production build never carries a key: " +
                 "the build check strips sandbox keys and needs gameServerUrl.")]
        public bool useProduction;

        [Header("Game Server (required for production)")]
        [Tooltip("Base URL of YOUR server, e.g. https://api.mygame.com.\n\n" +
                 "Every call that needs the game secret (initiate send/transfer, item purchase, catalog, " +
                 "balance, claims, minting player tokens) is sent to <this URL>/api/<Invo path> instead " +
                 "of to Invo, with an X-Invo-Storefront header naming the storefront. Your server checks the " +
                 "player's session, adds that storefront's X-Game-Secret-Key and forwards it to Invo unchanged. " +
                 "Blank = call Invo directly with the sandbox key, which the SDK only allows in sandbox. " +
                 "See the README, 'Server-side proxy'.")]
        public string gameServerUrl;

        [Header("Hosted Checkout")]
        [Tooltip("URL of YOUR OWN server endpoint that mints a hosted-checkout session.\n\n" +
                 "Real-money purchases cannot be made from the client: the Invo secret must stay " +
                 "server-side. The SDK POSTs { player_email, usd_amount } to this URL and expects " +
                 "{ checkout_url } back, then opens that URL. Your endpoint is the only place that " +
                 "should ever hold a production key.")]
        public string checkoutSessionEndpoint;

        // ------------------------------------------------------------------ resolution

        /// <summary>The Game ID for the active environment, falling back to the 3.0 <see cref="gameId"/>.</summary>
        public string ActiveGameId
        {
            get
            {
                string specific = useProduction ? productionGameId : sandboxGameId;
                return string.IsNullOrWhiteSpace(specific) ? gameId : specific.Trim();
            }
        }

        /// <summary>The storefront this build belongs to.</summary>
        public InvoStorefront ActiveStorefront => ResolveStorefront(InvoStorefrontResolver.CurrentPlatform);

        /// <summary>
        /// The sandbox key for this build's storefront, in the Editor and Development builds only.
        /// Empty in production and in release (non-Development) players, always: those call through
        /// <see cref="gameServerUrl"/>, and the build guard strips the keys from them as well.
        /// Falls back to the 3.0 <see cref="sdkKey"/>.
        /// </summary>
        public string ActiveSandboxKey => useProduction || !Debug.isDebugBuild ? string.Empty : SandboxKeyFor(ActiveStorefront);

        public InvoStorefront ResolveStorefront(InvoBuildPlatform platform)
        {
            InvoStorefront? forced = InvoStorefrontResolver.DefineOverride;
            if (forced.HasValue)
                return forced.Value;
            if (storefrontMap != null)
            {
                foreach (InvoStorefrontMapping entry in storefrontMap)
                    if (entry != null && entry.platform == platform)
                        return entry.storefront;
            }
            return InvoStorefrontResolver.DefaultFor(platform);
        }

        public string SandboxKeyFor(InvoStorefront storefront)
        {
            if (sandboxKeys != null)
            {
                foreach (InvoStorefrontKey entry in sandboxKeys)
                    if (entry != null && entry.storefront == storefront && !string.IsNullOrWhiteSpace(entry.sdkKey))
                        return entry.sdkKey.Trim();
            }
            return string.IsNullOrWhiteSpace(sdkKey) ? string.Empty : sdkKey.Trim();
        }

        public static List<InvoStorefrontMapping> DefaultStorefrontMap()
        {
            var map = new List<InvoStorefrontMapping>();
            foreach (InvoBuildPlatform platform in Enum.GetValues(typeof(InvoBuildPlatform)))
                map.Add(new InvoStorefrontMapping { platform = platform, storefront = InvoStorefrontResolver.DefaultFor(platform) });
            return map;
        }
    }

    /// <summary>Maps the running platform to a storefront and back to wire names.</summary>
    public static class InvoStorefrontResolver
    {
        public static InvoBuildPlatform CurrentPlatform
        {
            get
            {
#if UNITY_ANDROID
                return InvoBuildPlatform.Android;
#elif UNITY_IOS
                return InvoBuildPlatform.iOS;
#elif UNITY_PS4 || UNITY_PS5
                return InvoBuildPlatform.PlayStation;
#elif UNITY_GAMECORE || UNITY_XBOXONE
                return InvoBuildPlatform.Xbox;
#elif UNITY_SWITCH
                return InvoBuildPlatform.Switch;
#elif UNITY_WEBGL
                return InvoBuildPlatform.WebGL;
#else
                return InvoBuildPlatform.Desktop;
#endif
            }
        }

        /// <summary>A scripting define (INVO_STOREFRONT_&lt;NAME&gt;) that forces the storefront for one build.</summary>
        public static InvoStorefront? DefineOverride
        {
            get
            {
#if INVO_STOREFRONT_STEAM
                return InvoStorefront.Steam;
#elif INVO_STOREFRONT_GOOGLEPLAY
                return InvoStorefront.GooglePlay;
#elif INVO_STOREFRONT_APPSTORE
                return InvoStorefront.AppStore;
#elif INVO_STOREFRONT_PLAYSTATION
                return InvoStorefront.PlayStation;
#elif INVO_STOREFRONT_XBOX
                return InvoStorefront.Xbox;
#elif INVO_STOREFRONT_NINTENDOSWITCH
                return InvoStorefront.NintendoSwitch;
#elif INVO_STOREFRONT_EPICGAMESSTORE
                return InvoStorefront.EpicGamesStore;
#elif INVO_STOREFRONT_WEB
                return InvoStorefront.Web;
#elif INVO_STOREFRONT_PLATFORM
                return InvoStorefront.Platform;
#else
                return null;
#endif
            }
        }

        public static InvoStorefront DefaultFor(InvoBuildPlatform platform)
        {
            switch (platform)
            {
                case InvoBuildPlatform.Android: return InvoStorefront.GooglePlay;
                case InvoBuildPlatform.iOS: return InvoStorefront.AppStore;
                case InvoBuildPlatform.PlayStation: return InvoStorefront.PlayStation;
                case InvoBuildPlatform.Xbox: return InvoStorefront.Xbox;
                case InvoBuildPlatform.Switch: return InvoStorefront.NintendoSwitch;
                case InvoBuildPlatform.WebGL: return InvoStorefront.Web;
                default: return InvoStorefront.Steam;
            }
        }

        /// <summary>The value of the X-Invo-Storefront header sent to your game server.</summary>
        public static string WireName(InvoStorefront storefront)
        {
            switch (storefront)
            {
                case InvoStorefront.GooglePlay: return "google_play";
                case InvoStorefront.AppStore: return "app_store";
                case InvoStorefront.PlayStation: return "playstation";
                case InvoStorefront.Xbox: return "xbox";
                case InvoStorefront.NintendoSwitch: return "nintendo_switch";
                case InvoStorefront.EpicGamesStore: return "epic_games_store";
                case InvoStorefront.Web: return "web";
                case InvoStorefront.Platform: return "platform";
                default: return "steam";
            }
        }

        public static string DisplayName(InvoStorefront storefront)
        {
            switch (storefront)
            {
                case InvoStorefront.GooglePlay: return "Google Play";
                case InvoStorefront.AppStore: return "App Store";
                case InvoStorefront.NintendoSwitch: return "Nintendo Switch";
                case InvoStorefront.EpicGamesStore: return "Epic Games Store";
                default: return storefront.ToString();
            }
        }
    }
}
