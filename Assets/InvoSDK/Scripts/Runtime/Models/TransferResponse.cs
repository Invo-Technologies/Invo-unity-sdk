using System;
using System.Collections.Generic;

namespace InvoSDK
{
    [Serializable]
    public class TransferResponse
    {
        public string status;
        public string message;
        public string transaction_id;
        public string claim_code;


        // =========================
        //  Purchase Response
        // =========================
        [Serializable]
        public class PurchaseResponse
        {
            public string status { get; set; }
            public string message { get; set; }
            public string transaction_id { get; set; }
            public string order_id { get; set; }
            public decimal amount { get; set; }
        }

        // =========================
        //  Item Purchase
        // =========================
        [Serializable]
        public class ItemPurchaseResponse
        {
            public string status { get; set; }
            public string message { get; set; }
            public string item_id { get; set; }
            public string transaction_id { get; set; }
        }

        // =========================
        //  Available Destinations
        // =========================
        [Serializable]
        public class AvailableDestinationsResponse
        {
            public string status { get; set; }
            public List<AvailableGame> available_games { get; set; }
            public string source_game_id { get; set; }
            public string source_game_name { get; set; }
        }

        [Serializable]
        public class AvailableGame
        {
            public string game_id;
            public string game_name;
            public string currency_name;
            public string currency_symbol;
            public string currency_symbol_url;
            public string game_icon;
            public string game_poster;
            public string game_description;
            public string game_status;
            public string developer_name;
            public string publisher_name;
            public string genre;
            public string platform;
            public string minimum_transfer;
            public string maximum_transfer;
        }
        // =========================
        //  Initiate Send
        // =========================
        [Serializable]
        public class InitiateSendResponse
        {
            public string status { get; set; }
            public string message { get; set; }
            public string transaction_id { get; set; }
            public string order_id { get; set; }
            public SendDetails send_details { get; set; }
            public VerificationRequired verification_required { get; set; }
        }

        [Serializable]
        public class SendDetails
        {
            public string sending_game { get; set; }
            public string receiving_game { get; set; }
            public string receiving_game_id { get; set; }
            public string currency { get; set; }
            public int currency_id { get; set; }
            public string amount_initiated { get; set; }
            public FeesPreview fees_preview { get; set; }
            public string receiver_phone { get; set; }
            public string send_type { get; set; }
        }

        [Serializable]
        public class FeesPreview
        {
            public string total_fee { get; set; }
            public string sending_game_fee { get; set; }
            public string receiving_game_fee { get; set; }
            public string platform_fee { get; set; }
            public string net_amount { get; set; }
        }

        [Serializable]
        public class VerificationRequired
        {
            public string phone_number_masked { get; set; }
            public int pin_expires_in_minutes { get; set; }
        }

        // =========================
        //  Verify SMS
        // =========================
        [Serializable]
        public class VerifySmsResponse
        {
            public string status { get; set; }
            public string message { get; set; }
            public string transaction_id { get; set; }
            public string claim_code { get; set; }
            public ClaimInstructions claim_instructions { get; set; }
            public SendSummary send_summary { get; set; }
            public string order_id { get; set; }
        }

        [Serializable]
        public class ClaimInstructions
        {
            public string message { get; set; }
            public string receiving_game_id { get; set; }
            public string receiving_game_name { get; set; }
            public string claim_code_expires_at { get; set; }
            public bool receiver_notified { get; set; }
        }

        [Serializable]
        public class SendSummary
        {
            public string amount_sent { get; set; }
            public string net_amount_for_claim { get; set; }
            public string fees_deducted { get; set; }
            public string sender_current_available_balance { get; set; }
        }

        // =========================
        //  Claim Transfer
        // =========================
        [Serializable]
        public class ClaimTransferResponse
        {
            public string status { get; set; }
            public string message { get; set; }
            public string claim_code { get; set; }
            public decimal new_balance { get; set; }
        }
    }
    
}
