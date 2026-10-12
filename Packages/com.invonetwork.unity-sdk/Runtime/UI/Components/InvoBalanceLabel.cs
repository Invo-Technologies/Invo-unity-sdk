using TMPro;
using UnityEngine;

namespace InvoSDK.UI
{
    /// <summary>Shows the player's spendable balance, kept current by APIManager's balance poll.</summary>
    public class InvoBalanceLabel : MonoBehaviour
    {
        public TMP_Text label;
        [Tooltip("{0} = balance, {1} = currency name.")]
        public string format = "Balance {0} {1}";

        private void OnEnable()
        {
            APIManager.BalanceChanged += Show;
            Show(APIManager.Instance != null ? APIManager.Instance.LastBalance : "-");
        }

        private void OnDisable() => APIManager.BalanceChanged -= Show;

        private void Show(string balance)
        {
            if (label == null) return;
            string currency = APIManager.Instance != null ? APIManager.Instance.GetGameCurrency() : string.Empty;
            label.text = string.Format(format, balance, currency).Trim();
        }
    }
}
