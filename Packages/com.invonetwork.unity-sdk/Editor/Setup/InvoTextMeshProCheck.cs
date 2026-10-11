// Unity 2023.2 and newer ship TextMeshPro inside com.unity.ugui 2.x, so there is nothing to check.
// On 2021.3 and 2022.3 TextMeshPro is the separate com.unity.textmeshpro package, and a UPM
// dependency cannot be conditional on the editor version. This assembly has no references, so it
// still compiles when TextMeshPro is missing and the SDK's own assemblies cannot.
#if !UNITY_2023_2_OR_NEWER
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace InvoSDK.Editor
{
    [InitializeOnLoad]
    internal static class InvoTextMeshProCheck
    {
        private const string TmpPackage = "com.unity.textmeshpro";
        private const string AskedKey = "InvoSDK.TextMeshProCheck.Asked";

        private static ListRequest listRequest;
        private static AddRequest addRequest;

        static InvoTextMeshProCheck()
        {
            // Once per editor session, not on every domain reload.
            if (SessionState.GetBool(AskedKey, false))
                return;
            SessionState.SetBool(AskedKey, true);
            listRequest = Client.List(true, false);
            EditorApplication.update += WaitForList;
        }

        private static void WaitForList()
        {
            if (!listRequest.IsCompleted)
                return;
            EditorApplication.update -= WaitForList;

            if (listRequest.Status != StatusCode.Success)
                return;
            foreach (var package in listRequest.Result)
            {
                if (package.name == TmpPackage)
                    return;
            }

            const string message =
                "The Invo SDK needs TextMeshPro, which on this Unity version is the separate " +
                "com.unity.textmeshpro package. It is not installed in this project.";
            // Never prompt in batch mode (CI, command-line builds); just log.
            if (Application.isBatchMode)
            {
                Debug.LogError("[InvoSDK] " + message + " Add it with the Package Manager.");
                return;
            }

            if (EditorUtility.DisplayDialog("Invo SDK: TextMeshPro missing",
                    message + "\n\nAdd it now?", "Add TextMeshPro", "Not now"))
            {
                addRequest = Client.Add(TmpPackage);
                EditorApplication.update += WaitForAdd;
            }
        }

        private static void WaitForAdd()
        {
            if (!addRequest.IsCompleted)
                return;
            EditorApplication.update -= WaitForAdd;
            if (addRequest.Status == StatusCode.Success)
                Debug.Log("[InvoSDK] Added " + addRequest.Result.packageId +
                          ". Import 'TMP Essential Resources' from Window > TextMeshPro if prompted.");
            else
                Debug.LogError("[InvoSDK] Could not add " + TmpPackage + ": " + addRequest.Error?.message);
        }
    }
}
#endif
