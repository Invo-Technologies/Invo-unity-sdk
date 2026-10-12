// Harness-only. Puts the sandbox Game ID and keys from environment variables into the harness
// project's InvoSDKConfig (never the repo's), and takes them out again.
//   INVO_SANDBOX_GAME_ID, INVO_SANDBOX_KEY_STEAM, INVO_SANDBOX_KEY_GOOGLE_PLAY, INVO_SANDBOX_KEY_APP_STORE,
//   INVO_SANDBOX_KEY_WEB, INVO_SANDBOX_KEY_CONSOLE (PlayStation, Xbox and Switch)
// Usage: Unity -batchmode -projectPath <harness> -executeMethod InvoSDK.Harness.InvoHarnessSandbox.Configure -quit
// Keys are never logged; only which storefronts have one.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace InvoSDK.Harness
{
    public static class InvoHarnessSandbox
    {
        private const string ConfigPath = "Assets/InvoSDK/Resources/InvoSDKConfig.asset";

        private static readonly (string env, InvoStorefront[] storefronts)[] Keys =
        {
            ("INVO_SANDBOX_KEY_STEAM", new[] { InvoStorefront.Steam }),
            ("INVO_SANDBOX_KEY_GOOGLE_PLAY", new[] { InvoStorefront.GooglePlay }),
            ("INVO_SANDBOX_KEY_APP_STORE", new[] { InvoStorefront.AppStore }),
            ("INVO_SANDBOX_KEY_WEB", new[] { InvoStorefront.Web }),
            ("INVO_SANDBOX_KEY_CONSOLE", new[] { InvoStorefront.PlayStation, InvoStorefront.Xbox, InvoStorefront.NintendoSwitch }),
        };

        public static void Configure()
        {
            string gameId = Environment.GetEnvironmentVariable("INVO_SANDBOX_GAME_ID");
            if (string.IsNullOrWhiteSpace(gameId))
                throw new InvalidOperationException("INVO_SANDBOX_GAME_ID is not set");

            InvoSDKConfig config = LoadOrCreate();
            config.useProduction = false;
            config.sandboxGameId = gameId.Trim();
            config.sandboxKeys = new List<InvoStorefrontKey>();
            var set = new List<string>();
            foreach (var (env, storefronts) in Keys)
            {
                string key = Environment.GetEnvironmentVariable(env);
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                foreach (InvoStorefront storefront in storefronts)
                {
                    config.sandboxKeys.Add(new InvoStorefrontKey { storefront = storefront, sdkKey = key.Trim() });
                    set.Add(storefront.ToString());
                }
            }
            config.sdkKey = string.Empty;
            config.gameId = string.Empty;
            config.playerEmail = "test@example.com";
            config.playerPhone = "+15555550111";
            config.playerName = "Test Player";
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[InvoHarnessSandbox] sandbox game {config.sandboxGameId}; keys for: {string.Join(", ", set)}");
        }

        /// <summary>Removes every key from the harness config (run after the smoke builds).</summary>
        public static void Strip()
        {
            InvoSDKConfig config = AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(ConfigPath);
            if (config == null)
                return;
            config.sandboxKeys = new List<InvoStorefrontKey>();
            config.sdkKey = string.Empty;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[InvoHarnessSandbox] keys removed");
        }

        private static InvoSDKConfig LoadOrCreate()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:InvoSDKConfig"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != ConfigPath && path.StartsWith("Assets/"))
                    AssetDatabase.DeleteAsset(path); // one config only, or Resources.Load is ambiguous
            }
            var config = AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(ConfigPath);
            if (config != null)
                return config;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            config = ScriptableObject.CreateInstance<InvoSDKConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }
    }
}
