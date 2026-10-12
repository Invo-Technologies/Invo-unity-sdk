using System;
using System.Collections.Generic;

namespace InvoSDK
{
    /// <summary>Response of <c>POST /api/players/destinations</c>: titles where the player already plays.</summary>
    [Serializable]
    public class PlayerDestinationsResponse
    {
        public string status;
        public string source_game_id;
        public string source_game_name;
        /// <summary><c>universal</c> or <c>linked</c>.</summary>
        public string transfer_mode;
        /// <summary>Most recent activity first. Not filtered like available-destinations: cross-check before offering.</summary>
        public List<PlayerDestination> destinations = new List<PlayerDestination>();
    }

    [Serializable]
    public class PlayerDestination
    {
        public string game_id;
        public string game_name;
        public string tenant_type;
        public string game_status;
        public PlayerDestinationCurrency default_currency;
        /// <summary>In the destination's default currency.</summary>
        public string available_balance;
        public string last_active;
    }

    [Serializable]
    public class PlayerDestinationCurrency
    {
        public int currency_id;
        public string currency_name;
        public string currency_symbol;
    }

    /// <summary>Response of <c>POST /api/sdk/players/phone</c>.</summary>
    [Serializable]
    public class PhoneChangeResponse
    {
        /// <summary><c>updated</c> or <c>unchanged</c>.</summary>
        public string status;
        /// <summary>When true, switch to <see cref="player_token"/>: the old token belongs to the old identity.</summary>
        public bool identity_rekeyed;
        public string identity_id;
        public string player_token;
        public string player_token_expires_at;
    }

    /// <summary>
    /// A phone change on a secured account. INVO emails the player a code and a link; they confirm
    /// on INVO's page (never in the game). Poll <see cref="APIManager.GetPhoneChangeRequestAsync"/>.
    /// </summary>
    [Serializable]
    public class PhoneChangeRequest
    {
        public string change_request_id;
        /// <summary><c>pending_confirmation</c> on create; then <c>pending</c>, <c>confirmed</c>, <c>expired</c>,
        /// <c>cancelled</c>, <c>refused</c> or <c>denied</c>.</summary>
        public string status;
        /// <summary><c>email_code</c>, <c>email_code_and_passkey</c> or <c>email_code_and_device</c>.</summary>
        public string confirmation;
        /// <summary>Always <c>email</c>: no text message is sent.</summary>
        public string delivery;
        public string message;
        public string new_phone_masked;
        public string created_at;
        public string expires_at;
        public string confirmed_at;
        public string confirmed_via;

        public bool IsPending => status == "pending" || status == "pending_confirmation";
        public bool IsConfirmed => status == "confirmed";
    }
}
