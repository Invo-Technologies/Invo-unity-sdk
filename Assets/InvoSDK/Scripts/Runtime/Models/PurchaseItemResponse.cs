using System;

namespace InvoSDK
{
    [Serializable]
    public class PurchaseItemResponse
    {
        public string status;                      // "success"
        public string message;                     // "Item purchased successfully"
        public string order_id;                    // e.g., "ORD_1759998299_QRA75WL1"
        public string transaction_id;              // e.g., "TXN20251009082459886850PRI52Y"
        public BalanceInfo balance_info;           // nested balance info
        public FinancialBreakdown financial_breakdown; // nested fee info
        public PlayerInfo player;                  // nested player info
        public PurchaseDetails purchase_details;   // nested item info

        [Serializable]
        public class BalanceInfo
        {
            public string previous_balance;  // "1750.00"
            public string new_balance;       // "1740.00"
            public string amount_spent;      // "10.00"
        }

        [Serializable]
        public class FinancialBreakdown
        {
            public string total_paid;        // "10.00"
            public string platform_fee;      // "0.50"
            public string developer_revenue; // "9.50"
        }

        [Serializable]
        public class PlayerInfo
        {
            public int player_id;            // 58
            public string player_email;      // ""
            public string player_name;       // ""
        }

        [Serializable]
        public class PurchaseDetails
        {
            public string item_id;           // "sword_001"
            public string item_name;         // "Legendary Sword"
            public string item_description;  // optional
            public string item_category;     // optional
            public string purchased_by;      // ""
            public string currency_name;     // "Gems"
            public int quantity;             // 1
            public string unit_price;        // "10.00"
            public string total_price;       // "10.00"
        }

        public bool IsSuccess => status != null && status.ToLower().Contains("success");
    }
}