// Harness-only. Builds the demo scene for the test matrix.
// Usage: Unity -batchmode -projectPath <harness> -executeMethod InvoSDK.Harness.InvoHarnessBuild.Windows -invoOut <dir> -quit
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace InvoSDK.Harness
{
    public static class InvoHarnessBuild
    {
        /// <summary>The imported Demo sample's scene (Assets/Samples/...), or the 2021.3 build copy.</summary>
        private static string Scene
        {
            get
            {
                foreach (string guid in AssetDatabase.FindAssets("InvoDemo t:Scene"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith("Assets/Samples/"))
                        return path;
                }
                return "Assets/InvoDemoBuild/InvoDemo.unity";
            }
        }

        public static void Windows() => Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "InvoDemo.exe");

        public static void WebGL() => Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, "");

        public static void Android()
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "network.invo.sdkdemo");
            Build(BuildTarget.Android, BuildTargetGroup.Android, "InvoDemo.apk");
        }

        /// <summary>Exports the Xcode project; compiling it needs a Mac.</summary>
        public static void iOS()
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "network.invo.sdkdemo");
            Build(BuildTarget.iOS, BuildTargetGroup.iOS, "");
        }

        private static void Build(BuildTarget target, BuildTargetGroup group, string exe)
        {
            string outDir = Arg("-invoOut");
            if (string.IsNullOrEmpty(outDir))
                throw new ArgumentException("-invoOut <dir> is required");
            if (target == BuildTarget.StandaloneWindows64)
                PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground = true;
            if (target == BuildTarget.StandaloneWindows64)
            {
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                target = target,
                targetGroup = group,
                locationPathName = string.IsNullOrEmpty(exe) ? outDir : System.IO.Path.Combine(outDir, exe),
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[InvoHarnessBuild] {target}: {report.summary.result}, {report.summary.totalErrors} error(s), " +
                      $"{report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime.TotalSeconds:F0} s");
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
