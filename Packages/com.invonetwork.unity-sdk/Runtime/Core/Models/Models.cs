using System;
using System.Collections.Generic;

namespace InvoSDK
{
    [Serializable]
    public class PlayerBalanceResponse
    {
        public Player player;
        public List<Balance> balances;
        public Summary summary;
        public string last_updated;
    }

    [Serializable]
    public class Player
    {
        public int player_id;
        public string player_name;
        public string player_email;
        public string date_joined;
    }

    [Serializable]
    public class Balance
    {
        public int currency_id;
        public string currency_name;
        /// <summary>
        /// The SPENDABLE figure — what the player can actually transfer, send or
        /// spend right now. Use this for "your balance" UI.
        /// </summary>
        public string available_balance;
        public string reserved_balance;
        /// <summary>
        /// available_balance + reserved_balance. Includes funds locked by in-flight
        /// transfers/sends, so it is NOT spendable. Do not display it as "balance".
        /// </summary>
        public string total_balance;
    }

    [Serializable]
    public class Summary
    {
        public string total_value;
        public int currency_count;
        public bool has_funds;
    }
}
