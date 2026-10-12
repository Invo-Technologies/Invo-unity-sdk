using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace InvoSDK.Editor
{
    /// <summary>
    /// Keeps INVO keys out of every build that could reach players, as INVO's docs require ("never
    /// ship an SDK key in client builds"). Sandbox keys go into Development builds only: before a
    /// production build, or any build without Development Build ticked, it strips every sandbox key
    /// and the test player from the config asset, and after the build it puts them back. Production
    /// builds also have their settings checked. The stripped values are backed up under Library/
    /// first, so an editor crash mid-build cannot lose them: the next editor start restores the backup.
    /// </summary>
    internal sealed class InvoBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string BackupPath = "Library/InvoSDK/build-guard-backup.json";

        public int callbackOrder => -100;

        [Serializable]
        private class Backup
        {
            public string configPath;
            public string sdkKey;
            public List<InvoStorefrontKey> sandboxKeys;
            public string playerEmail;
            public string playerPhone;
            public string playerName;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            RestoreIfBackedUp();
            InvoSDKConfig config = InvoSDKSetupWizard.FindConfig();
            if (config == null)
                return;
            bool development = (report.summary.options & BuildOptions.Development) != 0;
            if (!config.useProduction && development)
                return;

            if (config.useProduction)
            {
                List<string> problems = ProductionProblems(config);
                if (problems.Count > 0)
                    throw new BuildFailedException("[InvoSDK] Production build stopped:\n- " + string.Join("\n- ", problems) +
                                                   "\nFix it in InvoSDK > Settings > Production.");
            }
            else if (string.IsNullOrWhiteSpace(config.gameServerUrl))
            {
                Debug.LogWarning("[InvoSDK] Sandbox keys only go into Development builds, and this build is not one. " +
                                 "INVO calls that need a key will fail in it. Tick Development Build to test against the " +
                                 "sandbox, or set a Game Server URL in InvoSDK > Settings.");
            }

            var backup = new Backup
            {
                configPath = AssetDatabase.GetAssetPath(config),
                sdkKey = config.sdkKey,
                sandboxKeys = config.sandboxKeys,
                playerEmail = config.playerEmail,
                playerPhone = config.playerPhone,
                playerName = config.playerName
            };
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup));

            config.sdkKey = string.Empty;
            config.sandboxKeys = new List<InvoStorefrontKey>();
            config.playerEmail = string.Empty;
            config.playerPhone = string.Empty;
            config.playerName = string.Empty;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[InvoSDK] " + (config.useProduction ? "Production" : "Release") +
                      " build: sandbox keys and test player stripped from the build.");
        }

        public void OnPostprocessBuild(BuildReport report) => RestoreIfBackedUp();

        /// <summary>The reasons a production build may not go ahead. Empty when it may.</summary>
        public static List<string> ProductionProblems(InvoSDKConfig config)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(config.productionGameId) && string.IsNullOrWhiteSpace(config.gameId))
                problems.Add("The production Game ID is empty.");
            string server = config.gameServerUrl?.Trim();
            if (string.IsNullOrEmpty(server))
                problems.Add("The Game Server URL is empty. Production calls that need a key go through your server.");
            else if (!server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                problems.Add("The Game Server URL must use https.");
            if (!string.IsNullOrWhiteSpace(config.checkoutSessionEndpoint) &&
                !config.checkoutSessionEndpoint.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                problems.Add("The checkout session endpoint must use https.");
            return problems;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterCrash() => EditorApplication.delayCall += RestoreIfBackedUp;

        private static void RestoreIfBackedUp()
        {
            if (!File.Exists(BackupPath))
                return;
            Backup backup;
            try
            {
                backup = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
            }
            catch (Exception e)
            {
                Debug.LogError("[InvoSDK] Could not read the build-guard backup at " + BackupPath + ": " + e.Message);
                return;
            }
            var config = AssetDatabase.LoadAssetAtPath<InvoSDKConfig>(backup.configPath);
            if (config != null)
            {
                config.sdkKey = backup.sdkKey;
                config.sandboxKeys = backup.sandboxKeys ?? new List<InvoStorefrontKey>();
                config.playerEmail = backup.playerEmail;
                config.playerPhone = backup.playerPhone;
                config.playerName = backup.playerName;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            File.Delete(BackupPath);
        }
    }
}
