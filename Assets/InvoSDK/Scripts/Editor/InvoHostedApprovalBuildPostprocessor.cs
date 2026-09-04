#if UNITY_EDITOR
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace InvoSDK.Editor
{
    /// <summary>
    /// Registers the hosted-approval return scheme (<c>invo-sdk-&lt;game_id&gt;</c>) at build time
    /// so the browser can hand control back to the game:
    /// iOS - adds a CFBundleURLTypes entry to Info.plist and links AuthenticationServices.framework;
    /// Android - adds a VIEW/BROWSABLE intent-filter for the scheme to the launcher activity.
    /// Both are idempotent and skipped when <see cref="InvoSDKConfig.gameId"/> is not numeric.
    /// The equivalent manual snippets are in README.md, "Hosted approval on mobile".
    /// </summary>
    public class InvoHostedApprovalBuildPostprocessor
#if UNITY_ANDROID
        : IPostGenerateGradleAndroidProject
#endif
    {
        public int callbackOrder { get { return 100; } }

        private static string ResolveScheme()
        {
            InvoSDKConfig config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
            string scheme = config != null ? InvoHostedApprovalCore.ReturnScheme(config.gameId) : null;
            if (scheme == null)
                Debug.LogWarning("[InvoSDK] No numeric gameId in InvoSDKConfig; the hosted-approval return scheme was not registered.");
            return scheme;
        }

#if UNITY_IOS
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;
            string scheme = ResolveScheme();
            if (scheme == null)
                return;

            string plistPath = Path.Combine(buildPath, "Info.plist");
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;
            PlistElementArray urlTypes = root.values.ContainsKey("CFBundleURLTypes")
                ? root["CFBundleURLTypes"].AsArray()
                : root.CreateArray("CFBundleURLTypes");

            bool present = false;
            foreach (PlistElement entry in urlTypes.values)
            {
                // AsDict() throws on a malformed entry; a soft cast just skips it.
                PlistElementDict dict = entry as PlistElementDict;
                if (dict == null || !dict.values.ContainsKey("CFBundleURLSchemes"))
                    continue;
                PlistElementArray schemes = dict["CFBundleURLSchemes"] as PlistElementArray;
                if (schemes == null)
                    continue;
                foreach (PlistElement s in schemes.values)
                {
                    PlistElementString str = s as PlistElementString;
                    if (str != null && str.value == scheme)
                        present = true;
                }
            }
            if (!present)
            {
                PlistElementDict dict = urlTypes.AddDict();
                dict.SetString("CFBundleURLName", scheme);
                dict.CreateArray("CFBundleURLSchemes").AddString(scheme);
            }
            plist.WriteToFile(plistPath);

            string projPath = PBXProject.GetPBXProjectPath(buildPath);
            PBXProject proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework", true);
            proj.WriteToFile(projPath);
        }
#endif

#if UNITY_ANDROID
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string scheme = ResolveScheme();
            if (scheme == null)
                return;
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (!File.Exists(manifestPath))
                return;

            XmlDocument doc = new XmlDocument();
            doc.Load(manifestPath);
            XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", AndroidNs);

            if (doc.SelectSingleNode("//intent-filter/data[@android:scheme='" + scheme + "']", ns) != null)
                return;

            XmlElement launcher = doc.SelectSingleNode(
                "//activity[intent-filter/category/@android:name='android.intent.category.LAUNCHER']", ns) as XmlElement;
            if (launcher == null)
            {
                Debug.LogWarning("[InvoSDK] No launcher activity found; add the hosted-approval intent-filter by hand (see README).");
                return;
            }

            XmlElement filter = doc.CreateElement("intent-filter");
            XmlElement action = doc.CreateElement("action");
            action.SetAttribute("name", AndroidNs, "android.intent.action.VIEW");
            filter.AppendChild(action);
            foreach (string category in new[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" })
            {
                XmlElement c = doc.CreateElement("category");
                c.SetAttribute("name", AndroidNs, category);
                filter.AppendChild(c);
            }
            XmlElement data = doc.CreateElement("data");
            data.SetAttribute("scheme", AndroidNs, scheme);
            filter.AppendChild(data);
            launcher.AppendChild(filter);
            doc.Save(manifestPath);
        }
#endif
    }
}
#endif
