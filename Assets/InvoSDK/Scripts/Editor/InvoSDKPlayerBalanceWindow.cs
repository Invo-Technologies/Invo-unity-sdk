using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Plastic.Newtonsoft.Json;

namespace InvoSDK.Editor
{
    public class InvoSDKPlayerBalanceWindow : EditorWindow
    {
        private string playerEmail = "";

        [MenuItem("InvoSDK/Check Player Balance", priority = 11)]
        public static void ShowWindow()
        {
            var window = GetWindow<InvoSDKPlayerBalanceWindow>("InvoSDK - Player Balance");
            window.minSize = new Vector2(500, 200);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Check Player Balance", EditorStyles.boldLabel);

            playerEmail = EditorGUILayout.TextField("Player Email", playerEmail);

            GUILayout.Space(10);

            if (GUILayout.Button("Get Balance"))
            {
                _ = GetBalanceAsync();
            }
        }

        private async Task GetBalanceAsync()
        {
           // try
           // {
                var response = await APIManager.Instance.GetPlayerBalanceAsync(playerEmail);
                string json = JsonConvert.SerializeObject(response);
                EditorUtility.DisplayDialog("Balance Retrieved", json, "OK");
            //}
            //catch (System.Exception ex)
            //{
            //    Debug.LogError("[InvoSDK] Error fetching balance: " + ex.Message);
            //    EditorUtility.DisplayDialog("Error Fetching Balance", ex.Message, "OK");
            //}
        }
    }
}