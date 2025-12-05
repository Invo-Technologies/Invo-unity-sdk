using UnityEngine;

namespace InvoSDK
{
    [CreateAssetMenu(fileName = "InvoSDKConfig", menuName = "InvoSDK/Config", order = 1)]
    public class InvoSDKConfig : ScriptableObject
    {
        public string sdkKey;
        public string gameId; 
        public string gameName;
        public string playerName;
        public string gameIconUrl;
        public string gameCurrencyName;
        public string gameCurrencyUrl;
        public string gameCurrencyID;
        public string gameVersion;
        public string playerEmail;
        public string playerPassword;
        public string playerPhone;
        public bool useProduction;
    }
}
