# InvoSDK Unity Plugin

## Overview

InvoSDK is a Unity plugin that integrates with the Invo platform to provide in-game currency management, item purchases, and player-to-player transfers. The package wraps the Invo HTTP API and exposes a singleton `APIManager`, Editor setup utilities, UI components, and runtime models to simplify integration.

This README summarizes the available features, setup steps, API endpoints implemented in the package, the configuration asset, editor utilities, and model shapes included in the repository.

## Environments (Important)

Invo now exposes distinct production and sandbox environments. For testing you MUST use the sandbox environment (do not call production endpoints while testing).

Production (Live games only):
- Host: `https://invo.network`
- Routes: `/api/*` and `/auth/*`
- Console: `https://console.invo.network`

Sandbox (All testing):
- Host: `https://sandbox.invo.network`
- Routes: `/sandbox/api/*` and `/sandbox/auth/*`
- Console: `https://dev.console.invo.network`

Quick Sandbox Example
1. Login
   POST `https://sandbox.invo.network/sandbox/auth/login`
   Body: `{ "email": "...", "password": "..." }`
   Response: `{ "access_token": "..." }`
2. Get CSRF token
   GET `https://sandbox.invo.network/sandbox/auth/csrf-token`
   Response: `{ "csrf_token": "..." }`
3. Use both tokens when calling protected sandbox API routes
   POST `https://sandbox.invo.network/sandbox/api/player-balances/check`
   Headers:
     `Authorization: Bearer {access_token}`
     `X-CSRF-Token: {csrf_token}`

ACTION REQUIRED for testers:
1. Login to your test account at `https://dev.console.invo.network`.
2. Update any code or editor tools to use the `/sandbox` prefix and `sandbox.invo.network` when running tests.

## Highlights / Feature List

- Player balance.
- Fetch game item catalog and featured items.
- Purchase items via `PurchaseItemAsync`.
- Initiate currency sends, verify SMS, and claim transfers.
- Async sprite loading helper for remote images.
- Editor Setup Wizard that auto-creates `InvoSDKConfig` and opens on first import.
- Example UI panels and window manager to quickly add Invo flows to your game.

## Installation

1. Copy the `Assets/InvoSDK` folder and `Assets/APIManager.cs` into your Unity project's `Assets` directory (or copy the repository contents into your project root).
2. Install dependencies:
   - `Newtonsoft.Json` (JSON serialization)
   - `TextMeshPro` (optional, UI integration)
3. The Editor scripts will auto-create a `Resources/InvoSDKConfig.asset` on first import and open the Setup Wizard.

## Runtime Configuration (`InvoSDKConfig`)

The plugin uses a `ScriptableObject` named `InvoSDKConfig` loaded via `Resources.Load<InvoSDKConfig>("InvoSDKConfig")`.
Place the asset inside any `Resources` folder (the provided Editor wizard creates `Assets/Resources/InvoSDKConfig.asset`).

Fields available on `InvoSDKConfig`:
- `sdkKey` (string) — your Invo API secret key (DO NOT ship this in client builds)
- `gameId` (string)
- `gameName` (string)
- `playerName` (string)
- `gameIconUrl` (string)
- `gameCurrencyName` (string)
- `gameCurrencyUrl` (string)
- `gameVersion` (string)
- `playerEmail` (string)
- `useProduction` (bool) — toggle production vs sandbox endpoints

Important: The SDK key is sensitive. The Setup Wizard warns: "Your SDK key should NEVER be shipped in client builds. Store it server-side only." Follow that guidance for production.

## Editor Tools

- `InvoSDKImporter` (initialized on load) — opens the Setup Wizard once on first import by setting a SessionState flag.
- `InvoSDKSetupWizard` EditorWindow — allows you to edit `InvoSDKConfig` fields, open docs/console, and perform quick actions (test purchase, etc.).
- `InvoSDKTestPurchaseWindow` and `InvoSDKItemCatalogWindow` — utility windows to test purchases and browse the item catalog (see Editor folder).

Menu: `InvoSDK > Setup Wizard` to reopen the setup window.

## APIManager (Runtime)

`Assets/APIManager.cs` implements the main runtime API. It is a MonoBehaviour singleton (`APIManager.Instance`) and supports automatic balance polling. Key configuration in the Inspector:
- `Use Production` — toggle sandbox/production URL (this value is synced from the `InvoSDKConfig` asset at runtime)
- `Player Balance` — assign a `TextMeshProUGUI` to display polled balance
- `Balance Poll Interval` — frequency in seconds

Implemented endpoints and helpers (method -> API path):

- `GetGameItemsAsync` -> `POST {ApiBase}/v1/game-items/list` (payload: `{ game_secret }`)
- `GetPlayerBalanceAsync(email)` -> `GET {ApiBase}/player-balances/player/by-email/{email}`
- `GetAvailableDestinationsAsync` -> `POST {ApiBase}/currency-sends/available-destinations` (payload: `{ source_game_id }`)
- `InitiateSendAsync(...)` -> `POST {ApiBase}/currency-sends/initiate-send` (payload: sender/receiver, amount, client_request_id)
- `VerifySmsAsync(transactionId, pin)` -> `POST {ApiBase}/currency-sends/verify-sms` (payload: `{ transaction_id, sms_pin }`)
- `ClaimCurrencyAsync(claimCode, playerName, playerPhone)` -> `POST {ApiBase}/currency-sends/claim-currency` (payload: `{ claim_code, target_player_name, target_player_phone }`)
- `PurchaseItemAsync(...)` -> `POST {ApiBase}/item-purchases/purchase-item` (payload: purchase details)

Other helpers:
- `LoadSpriteAsync(url, targetImage)` — downloads a texture and converts to `Sprite`.

API request headers:
- `X-Game-Secret-Key` — value taken from `InvoSDKConfig.sdkKey`
- `Content-Type: application/json`

Note: `APIManager` reads the `InvoSDKConfig` using `Resources.Load`. If the config is missing the manager logs an error and will not function correctly.

## Models (Runtime)

The package contains basic response models located in `Assets/InvoSDK/Scripts/Runtime/Models`.

Key model classes and fields:

- `PlayerBalanceResponse`
  - `Player player` (player_id, player_name, player_email, date_joined)
  - `List<Balance> balances` (currency_id, currency_name, available_balance, reserved_balance, total_balance)
  - `Summary summary` (total_value, currency_count, has_funds)
  - `string last_updated`

- `PurchaseResponse` / `PurchaseDetails`
  - Fields such as `status`, `transaction_id`, `message`, `purchase_details`, `order_id`.

- `ItemPurchaseResponse`
  - `status`, `transaction_id`, `message`, `order_id`.

Browse `Assets/InvoSDK/Scripts/Runtime/Models/Models.cs` and `PurchaseItemResponse.cs` for the concrete shapes used by the plugin.

## UI Components (Included)

The package includes example UI components under `Assets/InvoSDK/Scripts/UI` to show how to integrate the SDK into your game flow:
- Item purchase panels (`ItemPurchasePanel`, `ItemPurchaseConfirmPanel`, `ItemCardView`)
- Transfer flow panels (`SenderCurrencyPanel`, `TransferCurrencyPanel`, `TransferStep`)
- Window manager and featured item panels (`InvoSDKWindowManager`, `InvoSDKFeaturedPanel`, `FeaturedItemCard`, `FeatureWeeklyItemCard`)
- Button binder helper (`InvoUIButtonBinder`)

These components demonstrate usage of `APIManager` methods and runtime models. Use them as references for building custom UI.

## Example Usage

Obtain the singleton instance:

```csharp
var api = InvoSDK.APIManager.Instance;
```

Get player balance:

```csharp
await api.GetPlayerBalanceAsync("player@example.com",
    onSuccess: resp => { /* handle resp.balances */ },
    onError: err => Debug.LogError(err));
```

Purchase an item:

```csharp
await api.PurchaseItemAsync(
    playerEmail: "player@example.com",
    playerName: "Player",
    itemId: "sword_001",
    itemName: "Legendary Sword",
    quantity: 1,
    unitPrice: 10f,
    totalPrice: 10f,
    onSuccess: resp => { /* use resp */ },
    onError: err => Debug.LogError(err)
);
```

Initiate a currency send and verify via SMS:

```csharp
await api.InitiateSendAsync(senderName, senderEmail, senderPhone, receivingGameId, receiverPhone, amount,
    onSuccess: resp => { /* resp.transaction_id */ },
    onError: err => Debug.LogError(err));

await api.VerifySmsAsync(transactionId, pin, onSuccess: verifyResp => { /* claim_code */ }, onError: Debug.LogError);
```

Load a sprite from a remote URL:

```csharp
var sprite = await InvoSDK.APIManager.LoadSpriteAsync(url, targetImageComponent);
```

## Troubleshooting

- "Config not found! Please run Setup Wizard." — ensure `Assets/Resources/InvoSDKConfig.asset` exists and has values populated.
- "Player email not set in config." — set `playerEmail` on the config asset or pass email directly to relevant calls.
- Missing `Newtonsoft.Json` at runtime will cause JSON parse failures — ensure the library is included in the player build.
- Avoid shipping `sdkKey` in public client builds — treat it as a server-side secret.

## Where to Look in the Repository

Key files and folders:
- `Assets/APIManager.cs` — main runtime API and helpers
- `Assets/InvoSDK/Scripts/InvoSDKConfig.cs` — config `ScriptableObject`
- `Assets/InvoSDK/Scripts/Runtime/Models` — data models
- `Assets/InvoSDK/Scripts/Editor` — setup wizard and editor windows
- `Assets/InvoSDK/Scripts/UI` — example UI panels and components



---

For API reference and documentation visit: https://docs.invo.network