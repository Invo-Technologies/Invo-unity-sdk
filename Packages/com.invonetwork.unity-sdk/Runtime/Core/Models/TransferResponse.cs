using System;
using System.Collections.Generic;

namespace InvoSDK
{
    /// <summary>
    /// Container for the transfer / currency-send response models.
    /// Consumed elsewhere via <c>using static InvoSDK.TransferResponse;</c>.
    /// This outer type is never deserialized itself — it is a namespace-like
    /// holder only.
    /// </summary>
    [Serializable]
    public class TransferResponse
    {
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
            public string source_game_icon { get; set; }
            public string source_currency_name { get; set; }
            public string source_currency_icon { get; set; }
            /// <summary>Count of rows in <see cref="available_games"/>.</summary>
            public int total_destinations { get; set; }
            /// <summary>Raw tenant setting: "yes" / "no" (string, not bool).</summary>
            public string universal_transfers { get; set; }
            /// <summary>"universal" or "linked".</summary>
            public string transfer_mode { get; set; }
            /// <summary>Only present when transfer_mode == "linked".</summary>
            public List<string> linked_game_ids { get; set; }
            /// <summary>Only present on the "no linked games configured" success body.</summary>
            public string message { get; set; }
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
            public string game_url;
            public string game_description;
            public string game_status;
            public string developer_name;
            public string publisher_name;
            public string genre;
            public string platform;
            public string tenant_type;
            public string minimum_transfer;
            public string maximum_transfer;
            /// <summary>
            /// UX hint: false means this destination will reject Steam-origin value.
            /// Gray the row out for a player holding Steam-origin funds.
            /// </summary>
            public bool accepts_steam_origin_value;
        }

        // =========================
        //  Initiate Send  (POST /api/currency-sends/initiate-send)
        // =========================
        [Serializable]
        public class InitiateSendResponse
        {
            public string status { get; set; }
            public string message { get; set; }
            public string transaction_id { get; set; }
            public string order_id { get; set; }
            /// <summary>
            /// Canonical post-write balance (decimal-as-string, may be null).
            /// Sender's available_balance after the send amount was reserved.
            /// </summary>
            public string new_balance { get; set; }
            public string currency_name { get; set; }
            /// <summary>"in_app" or "sms". Always present.</summary>
            public string verification_method { get; set; }
            /// <summary>ISO-8601 SMS PIN expiry, may be null.</summary>
            public string verification_expires_at { get; set; }
            public SendDetails send_details { get; set; }
            public VerificationRequired verification_required { get; set; }
            /// <summary>Only present when status == "pending_guardian_approval" (HTTP 202).</summary>
            public GuardianApproval guardian_approval { get; set; }
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
            public TransferPolicy transfer_policy { get; set; }
        }

        // =========================
        //  Initiate Transfer  (POST /api/transfers/initiate-transfer)
        // =========================
        [Serializable]
        public class InitiateTransferResponse
        {
            public string status;
            public string message;
            public string transaction_id;
            public string order_id;
            /// <summary>
            /// Canonical post-write balance (decimal-as-string, may be null).
            /// Source player's available_balance after the reservation.
            /// </summary>
            public string new_balance;
            public string currency_name;
            /// <summary>"in_app" or "sms". Always present.</summary>
            public string verification_method;
            /// <summary>ISO-8601 SMS PIN expiry, may be null.</summary>
            public string verification_expires_at;
            public TransferDetails transfer_details;
            public VerificationRequired verification_required;
            /// <summary>Only present when status == "pending_guardian_approval" (HTTP 202).</summary>
            public GuardianApproval guardian_approval;
        }

        [Serializable]
        public class TransferDetails
        {
            public string source_game;
            public string target_game;
            public string target_game_id;
            public string currency;
            public int currency_id;
            public string amount_initiated;
            public FeesPreview fees_preview;
            public TransferPolicy transfer_policy;
        }

        [Serializable]
        public class TransferPolicy
        {
            /// <summary>Raw tenant setting: "yes" / "no" (string, not bool).</summary>
            public string source_universal_transfers;
            /// <summary>"universal" or "linked".</summary>
            public string policy_applied;
            /// <summary>Null when the policy is universal (the server sends null there).</summary>
            public bool? target_in_linked_list;
        }

        /// <summary>
        /// Fee preview. The server serializes the whole fee-breakdown dict, and the
        /// SEND and TRANSFER endpoints use DIFFERENT key names for the three fee
        /// legs, so both sets live here and one of them is always null.
        ///   send     -> sending_game_fee / receiving_game_fee / platform_fee
        ///   transfer -> source_game_fee  / target_game_fee    / invo_fee
        /// total_fee, net_amount, source_rate_pct and target_rate_pct are common
        /// to both. All values arrive as decimal-formatted strings.
        /// </summary>
        [Serializable]
        public class FeesPreview
        {
            public string total_fee { get; set; }
            public string net_amount { get; set; }

            // --- send shape (currency_send_api) ---
            public string sending_game_fee { get; set; }
            public string receiving_game_fee { get; set; }
            public string platform_fee { get; set; }

            // --- transfer shape (transfer_api) ---
            public string source_game_fee { get; set; }
            public string target_game_fee { get; set; }
            public string invo_fee { get; set; }

            // --- common to both ---
            public string source_rate_pct { get; set; }
            public string target_rate_pct { get; set; }
        }

        [Serializable]
        public class VerificationRequired
        {
            public string phone_number_masked { get; set; }
            public int pin_expires_in_minutes { get; set; }
        }

        /// <summary>
        /// Pending guardian (parental) approval block. Layered onto an initiate
        /// response when status flips to "pending_guardian_approval" (HTTP 202),
        /// and returned standalone by the approval-status endpoint.
        /// </summary>
        [Serializable]
        public class GuardianApproval
        {
            public string approval_id;
            public string state;
            public string expires_at;
            public string poll_endpoint;

            // Present on the approval-status endpoint's to_dict() shape only.
            public string transaction_id;
            public string approval_type;
            public string decided_at;
            public string decision_source;
            public string action_description;
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
            /// <summary>
            /// Canonical balance echo (decimal-as-string, may be null).
            /// Verify does not move funds; this re-confirms the reservation debit.
            /// </summary>
            public string new_balance { get; set; }
            public ClaimInstructions claim_instructions { get; set; }
            /// <summary>Populated by the SEND flow only.</summary>
            public SendSummary send_summary { get; set; }
            /// <summary>Populated by the TRANSFER flow only.</summary>
            public TransferSummary transfer_summary { get; set; }
            public string order_id { get; set; }
        }

        /// <summary>
        /// Claim instructions. The SEND flow names the destination
        /// receiving_game_*; the TRANSFER flow names it target_game_*.
        /// Both sets are here — only one pair is populated per response.
        /// </summary>
        [Serializable]
        public class ClaimInstructions
        {
            public string message { get; set; }

            // --- send shape ---
            public string receiving_game_id { get; set; }
            public string receiving_game_name { get; set; }
            /// <summary>Send flow only; absent (false) on the transfer flow.</summary>
            public bool receiver_notified { get; set; }

            // --- transfer shape ---
            public string target_game_id { get; set; }
            public string target_game_name { get; set; }

            public string claim_code_expires_at { get; set; }
        }

        [Serializable]
        public class SendSummary
        {
            public string amount_sent { get; set; }
            public string net_amount_for_claim { get; set; }
            public string fees_deducted { get; set; }
            public string sender_current_available_balance { get; set; }
        }

        /// <summary>
        /// Transfer-flow counterpart of <see cref="SendSummary"/>. NOTE the server
        /// uses different key names here: amount_initiated (not amount_sent) and
        /// source_player_current_available_balance (not sender_...).
        /// </summary>
        [Serializable]
        public class TransferSummary
        {
            public string amount_initiated;
            public string net_amount_for_claim;
            public string fees_deducted;
            public string source_player_current_available_balance;
        }

        // =========================
        //  Resend PIN
        // =========================
        /// <summary>
        /// POST /resend-pin. Success is <c>status == "resent"</c>. Failure bodies use
        /// either <c>status == "error"</c> + message, or a bare <c>error</c> code
        /// ("resend_unavailable" 503, "pin_expired" 400, "resend_cooldown" 429 with
        /// <see cref="retry_after"/> seconds).
        /// </summary>
        [Serializable]
        public class ResendPinResponse
        {
            public string status;
            public string error;
            public int? retry_after;
            public string message;
        }

        // =========================
        //  Transaction Status  (GET /{transaction_id}/status)
        // =========================
        /// <summary>
        /// Poll surface for both the transfer and the currency-send flow. The two
        /// endpoints share most keys; the send flow names the endpoints
        /// sending_/receiving_game_*, the transfer flow source_/target_game_*.
        /// </summary>
        [Serializable]
        public class TransactionStatusResponse
        {
            /// <summary>Envelope status ("success" / "error"), NOT the transaction state.</summary>
            public string status;
            public string message;
            public string transaction_id;
            public string transaction_type;
            /// <summary>The transaction's effective status.</summary>
            public string transaction_status;
            /// <summary>"awaiting" -> "approved" once the sender clears the gate.</summary>
            public string verification_state;
            public string amount;
            public string net_amount;
            public string fee_amount;
            public string created_at;
            public string completed_at;
            public int currency_id;
            public string currency_name;
            public string order_id;
            public string financial_tracking_status;
            public string failure_reason;

            // --- transfer shape ---
            public string source_game_id;
            public string target_game_id;

            // --- send shape ---
            public string sending_game_id;
            public string sending_game_name;
            public string receiving_game_id;
            public string receiving_game_name;
            public string claimed_by_player_name;
            public string claimed_by_player_email;

            /// <summary>Sender's own game only, and only after approval. Null otherwise.</summary>
            public string claim_code;
            public string claim_code_expires_at;

            /// <summary>Destination tenant only — recipient attribution.</summary>
            public string to_phone;
            public string to_identity_id;
        }

        // =========================
        //  Claim Transfer  (POST /api/transfers/claim-transfer)
        // =========================
        [Serializable]
        public class ClaimTransferResponse
        {
            public string status;
            public string message;
            public string transaction_id;
            public string order_id;
            public string currency_name;
            /// <summary>
            /// Decimal-as-string and nullable — do NOT model this as decimal.
            /// Target player's available_balance after the credit.
            /// </summary>
            public string new_balance;
            public ClaimedTransferDetails transfer_details;
            public string completion_time;
            /// <summary>
            /// Only on the HTTP-200 <c>status == "needs_account_selection"</c> body:
            /// several players share the destination phone and the caller must
            /// re-submit with target_player_id.
            /// </summary>
            public List<AccountCandidate> candidates;
            /// <summary>
            /// Echoed back on the "needs_account_selection" body only; null on a
            /// successful claim.
            /// </summary>
            public string claim_code;
        }

        // =========================
        //  Claim Currency  (POST /api/currency-sends/claim-currency)
        // =========================
        [Serializable]
        public class ClaimCurrencyResponse
        {
            public string status;
            public string message;
            public string transaction_id;
            public string order_id;
            public string currency_name;
            /// <summary>
            /// Decimal-as-string and nullable — receiver's available_balance
            /// after the credit.
            /// </summary>
            public string new_balance;
            public ClaimedSendDetails send_details;
            public string completion_time;
            /// <summary>Only on the HTTP-200 "needs_account_selection" body.</summary>
            public List<AccountCandidate> candidates;
            /// <summary>Echoed back on the "needs_account_selection" body only.</summary>
            public string claim_code;
        }

        [Serializable]
        public class ClaimedSendDetails
        {
            public string amount_received;
            public string sending_game;
            public string receiving_currency;
            public string receiver_player;
            /// <summary>Legacy duplicate of the top-level new_balance.</summary>
            public string new_balance;
        }

        [Serializable]
        public class ClaimedTransferDetails
        {
            public string amount_received;
            public string source_game;
            public string target_currency;
            public string target_player;
            /// <summary>Legacy duplicate of the top-level new_balance.</summary>
            public string new_balance;
        }

        /// <summary>
        /// One row of the <c>needs_account_selection</c> picker (HTTP 200, not an
        /// error). Re-submit the claim with the chosen player_id.
        /// </summary>
        [Serializable]
        public class AccountCandidate
        {
            public int player_id;
            /// <summary>Masked email, e.g. "j***@e***.com".</summary>
            public string email_hint;
        }

        // =========================
        //  Phone Share Required (HTTP 409 error envelope)
        // =========================
        /// <summary>
        /// Returned with HTTP 409 and <c>error_code == "PHONE_SHARE_APPROVAL_REQUIRED"</c>
        /// when the phone being attached already belongs to another account. The
        /// server has already minted an approval row and (usually) sent the OTP.
        /// </summary>
        [Serializable]
        public class PhoneShareRequired
        {
            public string status;
            public string error_code;
            public string message;
            /// <summary>Canonical E.164 phone.</summary>
            public string phone;
            public string requesting_email;
            /// <summary>Masked existing-owner emails for the UI.</summary>
            public List<string> existing_account_hints;
            /// <summary>Fallback endpoint to call when code_sent is false.</summary>
            public string next_endpoint;
            public string approval_id;
            public string expires_at;
            public bool code_sent;
            public bool already_approved;
            /// <summary>Present when inside the per-pair cooldown window.</summary>
            public int? cooldown_seconds;
            /// <summary>Present (true) when the phone hit the hourly OTP cap.</summary>
            public bool rate_limited;
            public int? retry_after_seconds;
        }
    }
}
