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
        public string available_balance;
        public string reserved_balance;
        public string total_balance;
    }

    [Serializable]
    public class Summary
    {
        public string total_value;
        public int currency_count;
        public bool has_funds;
    }

    [Serializable]
    public class PurchaseResponse
    {
        public string status;
        public string transaction_id;
        public string message;
        public PurchaseDetails purchase_details;
        public string order_id;
    }

    [Serializable]
    public class PurchaseDetails
    {
        public string usd_charged;
        public string currency_received;
        public string currency_name;
        public string new_balance;
    }

    [Serializable]
    public class ItemPurchaseResponse
    {
        public string status;
        public string transaction_id;
        public string message;
        public string order_id;
    }
}
