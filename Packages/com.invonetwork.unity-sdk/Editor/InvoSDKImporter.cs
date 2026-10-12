using UnityEditor;
using UnityEngine;

namespace InvoSDK.Editor
{
    [InitializeOnLoad]
    public class InvoSDKImporter
    {
        static InvoSDKImporter()
        {
            if (!SessionState.GetBool("InvoSDK_FirstImportDone", false))
            {
                SessionState.SetBool("InvoSDK_FirstImportDone", true);
                EditorApplication.update += OpenSetup;
            }
        }

        private static void OpenSetup()
        {
            EditorApplication.update -= OpenSetup;
            InvoSDKSetupWizard.ShowWindow(); // 
        }
    }
}
