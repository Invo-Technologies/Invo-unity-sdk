<p align="center">
  <img src="https://console.invo.network/assets/logo-e0324218.svg" alt="Invo Network Logo" width="200"/>
</p>

<h1 align="center">InvoSDK for Unity</h1>

<p align="center">
  <strong>Game economy infrastructure for Unity 6</strong>
</p>

<p align="center">
  <a href="https://docs.invo.network"><img src="https://img.shields.io/badge/docs-invo.network-blue.svg" alt="Documentation"></a>
  <a href="#"><img src="https://img.shields.io/badge/unity-6000.0%2B-green.svg" alt="Unity Version"></a>
  <a href="#"><img src="https://img.shields.io/badge/platforms-iOS%20%7C%20Android%20%7C%20Windows%20%7C%20macOS-lightgrey.svg" alt="Platforms"></a>
  <a href="#"><img src="https://img.shields.io/badge/license-MIT-orange.svg" alt="License"></a>
</p>

<p align="center">
  <a href="#quick-start">Quick Start</a> •
  <a href="#features">Features</a> •
  <a href="#installation">Installation</a> •
  <a href="#security">Security</a> •
  <a href="#server-side-proxy">Server-Side Proxy</a> •
  <a href="#support">Support</a>
</p>

---

> ### Read this before you plan the work
>
> This plugin is a **client**. Several flows need a server that you host, and one of them — real-money purchases — cannot function at all until you build it. The honest, complete list of what is not in the box lives in **[InvoSDK-README.md → What You Must Wire Yourself](InvoSDK-README.md#what-you-must-wire-yourself)**. This document is the longer tutorial; that one is the specification. Where they differ, that one wins.

---

## Contents

- [Overview](#overview)
- [Architecture Overview](#architecture-overview)
- [Features](#features)
- [Quick Start](#quick-start)
- [Installation](#installation)
- [Configuration](#configuration)
- [Authentication](#authentication)
- [API Reference](#api-reference)
- [Error Handling](#error-handling)
- [Not Every 2xx Is a Success](#not-every-2xx-is-a-success)
- [Phone Numbers](#phone-numbers)
- [Money and Locales](#money-and-locales)
- [Idempotency](#idempotency)
- [Complete API Endpoints](#complete-api-endpoints)
- [UI Components](#ui-components)
- [Editor Tools](#editor-tools)
- [Security](#security)
- [Server-Side Proxy](#server-side-proxy)
- [Hosted Checkout (Real-Money Purchases)](#hosted-checkout-real-money-purchases)
- [Hosted approval on mobile (system browser)](#hosted-approval-on-mobile-system-browser)
- [Troubleshooting](#troubleshooting)
- [Examples](#examples)
- [Support](#support)

---

## Overview

**InvoSDK** is a Unity 6 plugin that connects your game to the [Invo Network](https://invo.network) platform:

- **Player balances** — per-currency balances with polling and a bindable label
- **Item purchases** — spend the player's in-game currency on catalog items
- **Player-to-player sends** — address a recipient by phone, settle with a claim code
- **Cross-game transfers** — move a player's own balance between games on the network
- **Hosted checkout** — real-money currency packs, via an endpoint **you** host

It is a thin, honest transport layer plus a set of reference UI panels. It is not a drop-in storefront, and it is not a complete production integration on its own — see [Security](#security) and [Server-Side Proxy](#server-side-proxy).

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              YOUR UNITY GAME                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐          │
│  │  UI Components  │    │   APIManager    │    │  InvoSDKConfig  │          │
│  │                 │───▶│   (Singleton)   │◀───│ (ScriptableObj) │          │
│  │ • Item Purchase │    │                 │    │                 │          │
│  │ • Transfer Flow │    │ • X-Game-Secret │    │ • SDK Key       │          │
│  │ • Send Flow     │    │ • Env routing   │    │ • Game Settings │          │
│  │ • Featured Shop │    │ • Balance poll  │    │ • useProduction │          │
│  └─────────────────┘    └────────┬────────┘    └─────────────────┘          │
│                                  │                                          │
│                                  │ HTTPS + X-Game-Secret-Key                │
│                                  │ (no login, no tokens, no cookies)        │
└──────────────────────────────────┼──────────────────────────────────────────┘
                                   │
        ┌──────────────────────────┴───────────────────────────┐
        │                                                      │
        │  PRODUCTION: you put YOUR SERVER here.               │
        │  It holds the key; the game never sees it.           │
        │  See "Server-Side Proxy".                            │
        │                                                      │
        └──────────────────────────┬───────────────────────────┘
                                   │
┌──────────────────────────────────▼──────────────────────────────────────────┐
│                          INVO NETWORK CLOUD                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Sandbox    │  │  Production  │  │    Hosted    │  │  Sends and   │     │
│  │ Environment  │  │ Environment  │  │   Checkout   │  │  Transfers   │     │
│  │              │  │              │  │              │  │              │     │
│  │ Test cards   │  │ Real money   │  │ Signed URL,  │  │ SMS PIN +    │     │
│  │ Isolated DB  │  │ Isolated DB  │  │ 15-min TTL   │  │ claim code   │     │
│  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘     │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

Sandbox and production are **fully isolated databases with independent SDK keys**. A sandbox key does not work against production, and vice versa.

---

## Features

### Core Capabilities

| Feature | Description |
|---------|-------------|
| **Player Balances** | Per-currency balances, single-flight polling with exponential backoff |
| **Item Purchases** | Spend in-game currency, `decimal` prices, caller-supplied idempotency key |
| **P2P Sends** | Phone-addressed sends with SMS PIN verification and a claim code |
| **Cross-Game Transfers** | Move a player's own balance to another game on the network |
| **Hosted Checkout** | Real-money packs via a checkout session your server mints |
| **Reference UI** | Panels for purchase, send and transfer that you can read, fork or replace |

### Developer Experience

| Feature | Description |
|---------|-------------|
| **Setup Wizard** | Guided configuration, stray-asset detection, production-key warning |
| **Editor Tools** | Test purchases, balance lookups, and catalog editing inside Unity |
| **Async/Await** | Every call is `async Task<T>`; callbacks are optional, not required |
| **Typed Errors** | Non-2xx throws `InvoApiException` with `ErrorCode`, `ErrorId`, `RetryAfterSeconds` |
| **Locale-Safe Wire Format** | `InvoFormat` and `InvoPhone` keep amounts and numbers valid on every device |
| **Dual Environment** | One boolean switches host and route prefix |

---

## Quick Start

### 1. Get Your Credentials

1. Sign up at [dev.console.invo.network](https://dev.console.invo.network) (sandbox) or [console.invo.network](https://console.invo.network) (production)
2. Create a game project
3. Copy your **Game ID** and your **sandbox SDK Key**

> Use a **sandbox** key. The key you paste into the config asset is compiled into every player build you make. See [Security](#security).

### 2. Install the SDK

```
Assets/
├── InvoSDK/                                  ← Copy this folder
│   └── Resources/
│       └── InvoSDKConfig.asset               ← Created by the Setup Wizard
└── APIManager.cs                             ← Copy this file
```

### 3. Configure

Open **InvoSDK → Setup Wizard** from the Unity menu and fill in your credentials.

The config asset is created at `Assets/InvoSDK/Resources/InvoSDKConfig.asset`. It is **gitignored** — every developer configures their own, and it will not arrive with a clone.

### 4. Start Using

```csharp
using InvoSDK;
using UnityEngine;

var api = APIManager.Instance;

// Read a balance. available_balance is the SPENDABLE figure.
var balance = await api.GetPlayerBalanceAsync("player@email.com");
Debug.Log($"Balance: {balance.balances[0].available_balance}");

// Buy an item with in-game currency.
// The client request id is minted ONCE per user intent and reused on every retry.
string requestId = APIManager.NewClientRequestId();

try
{
    var result = await api.PurchaseItemAsync(
        clientRequestId: requestId,
        playerEmail:     "player@email.com",
        playerName:      "PlayerOne",
        itemId:          "sword_001",
        itemName:        "Legendary Sword",
        quantity:        1,
        unitPrice:       9.99m,
        totalPrice:      9.99m);

    if (result.IsSuccess)
        Debug.Log($"Purchased. New balance: {result.new_balance}");
    else
        Debug.LogWarning($"Not complete: {result.status}");   // a 2xx is not always a success
}
catch (InvoApiException ex) when (ex.IsDuplicate)
{
    // 409 on a purchase means it ALREADY SUCCEEDED. Treat as success.
    Debug.Log("Already purchased.");
}
catch (InvoApiException ex)
{
    Debug.LogError(ex);   // ToString() is log-safe — it excludes the response body
}
```

> **What this quick start does not give you.** In-game-currency purchases work straight from the client, and that is what the sandbox demo does. **Real-money purchases do not** — they need a checkout endpoint you host ([Hosted Checkout](#hosted-checkout-real-money-purchases)) — and a production launch needs a [server-side proxy](#server-side-proxy) regardless.

---

## Installation

### Requirements

| Requirement | Version |
|-------------|---------|
| Unity | 6000.0+ (Unity 6) |
| .NET | Standard 2.1 |
| Newtonsoft.Json | 3.2.1+ (via Package Manager) |
| TextMeshPro | Bundled with `com.unity.ugui` 2.0.0 in Unity 6 |

### Step-by-Step Installation

1. **Import the SDK**

   Copy the following into your Unity project's `Assets` folder:
   - `Assets/InvoSDK/` (entire folder)
   - `Assets/APIManager.cs`

2. **Install Dependencies**

   Open **Window → Package Manager** and install:
   ```
   com.unity.nuget.newtonsoft-json
   ```

3. **Run Setup Wizard**

   **InvoSDK → Setup Wizard**. It creates the config asset if it does not exist.

4. **Add APIManager to a scene**

   `APIManager` is a `MonoBehaviour` singleton with `DontDestroyOnLoad`. It loads `InvoSDKConfig` from `Resources` in `Awake()`. Without it in the scene, `APIManager.Instance` is null and every panel logs an error and stops.

### Package Dependencies

The versions this project is built against:

```json
{
  "com.unity.nuget.newtonsoft-json": "3.2.1",
  "com.unity.ugui": "2.0.0",
  "com.unity.inputsystem": "1.14.2"
}
```

### One config asset, not two

The canonical path is `Assets/InvoSDK/Resources/InvoSDKConfig.asset`.

If a second asset named `InvoSDKConfig` exists under any other `Resources` root — `Assets/Resources/InvoSDKConfig.asset` is the usual stray — `Resources.Load` resolution becomes undefined and you can end up building against the wrong key. The Setup Wizard detects this and offers to select the stray for you. Delete it.

---

## Configuration

### InvoSDKConfig Asset

`Assets/InvoSDK/Resources/InvoSDKConfig.asset`:

| Field | Type | Description |
|-------|------|-------------|
| `sdkKey` | string | Your game's `ivsdk_…` secret. **Sandbox only.** Ships in your build — see [Security](#security). |
| `gameId` | string | Your game identifier. Not secret. |
| `gameName` | string | Display name for your game |
| `playerName` | string | Default player display name (development convenience) |
| `playerEmail` | string | Default player email (development convenience) |
| `playerPhone` | string | Default player phone (development convenience) |
| `gameIconUrl` | string | URL to your game's icon |
| `gameCurrencyName` | string | Name of your in-game currency |
| `gameCurrencyUrl` | string | URL to the currency icon |
| `gameCurrencyID` | string | Currency identifier |
| `gameVersion` | string | Your game's version string |
| `useProduction` | bool | `false` (sandbox) by default. `APIManager` copies this in `Awake()`. |
| `checkoutSessionEndpoint` | string | https URL of **your** hosted-checkout endpoint. Set it once in the Setup Wizard; see the note below for per-panel overrides. |

> **`playerPassword` is gone.** It existed only to feed the deleted `/auth/login` handshake. If you have an older config asset with a value in it, nothing reads it.

> **`playerEmail` / `playerName` / `playerPhone` are development conveniences.** In a shipping game these come from your own player session, not from a build constant.

> ### Where `checkoutSessionEndpoint` takes effect
>
> The field exists in two places, and the panel resolves them in order:
>
> 1. **`InvoSDKFeaturedPanel.checkoutSessionEndpoint`** — an optional per-panel override in the Inspector. Leave it blank unless one store surface needs a different endpoint from the rest.
> 2. **`InvoSDKConfig.checkoutSessionEndpoint`** — the shared value the Setup Wizard writes. Used whenever the panel's own field is empty.
>
> Setting it once in the wizard is enough for most projects. Either way it must be an **https** URL, or checkout stays disabled and the panel logs an actionable error.

### Environment Configuration

```
┌─────────────────────────────────────────────────────────────────┐
│                     ENVIRONMENT ROUTING                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  useProduction = false (SANDBOX)                                │
│  ├── API base: https://sandbox.invo.network/sandbox/api         │
│  └── Console:  https://dev.console.invo.network                 │
│                                                                 │
│  useProduction = true (PRODUCTION)                              │
│  ├── API base: https://invo.network/api                         │
│  └── Console:  https://console.invo.network                     │
│                                                                 │
│  Auth: X-Game-Secret-Key header on every request. That is all.  │
│        No /auth routes. No tokens. No cookies.                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

The two API roots are compile-time constants in `APIManager`, not Inspector fields — a mistyped host would silently send your game secret somewhere that is not Invo.

> **⚠️ Use Sandbox during development.** Only switch to production behind a [server-side proxy](#server-side-proxy). A build that carries an `sdkKey` **and** has `useProduction = true` ships a production secret to every player.

---

## Authentication

**There is no login flow, and there is nothing to implement.**

Every request carries exactly one credential:

```
X-Game-Secret-Key: ivsdk_…
```

(`POST /v1/game-items/list` additionally repeats the secret in the request body as `game_secret`, because the backend's validator for that one route reads the body rather than the header. `APIManager` does this for you.)

### What was removed, and why

Earlier versions of this plugin performed a `/auth/login` + `/auth/csrf-token` handshake and attached `Authorization: Bearer` and `X-CSRF-Token` to every call. That entire flow has been **deleted**, along with the `playerPassword` config field it required. It was verified inert:

- The game API endpoints authenticate **solely** on `X-Game-Secret-Key`. They never read the bearer or CSRF headers.
- `/auth/login` authenticates against the **developer-console account table**, not against players. It was never a player login in the first place.

So: no tokens to store, no refresh to schedule, no cookies to persist, no re-authentication path to write. If you are porting from an older integration, delete all of it. Anything that still calls `/auth/*` or `/sandbox/auth/*` from a game client is doing nothing except leaking a password into a build.

Authentication failures are therefore a **configuration** problem, never a session problem — a `401` means the key is missing, wrong, or pointed at the wrong environment. Retrying will not fix it.

---

## API Reference

### APIManager (Singleton)

```csharp
var api = APIManager.Instance;
```

Every method is `async Task<T>`. Each takes an optional `onSuccess` callback and an optional `onError` of type **`Action<InvoApiException>`** — not `Action<string>`. On a non-2xx the method invokes `onError` **and then throws**, so `await` in a `try/catch` and use the callbacks only where a callback style is genuinely more convenient.

Useful members:

```csharp
string ApiBase { get; }                 // resolved environment root
string ApiKey  { get; }                 // from config
string GameId  { get; }

string GetPlayerEmail();
string GetPlayerName();
string GetGameName();
string GetGameCurrency();

static string NewClientRequestId();     // mint ONE per user intent
static Task<Sprite> LoadSpriteAsync(string url, Image target = null);

void StartBalancePolling();
void StopBalancePolling();
```

### Player Balance

```csharp
await api.GetPlayerBalanceAsync(
    email: "player@email.com",
    onSuccess: response => {
        foreach (var b in response.balances)
            Debug.Log($"{b.currency_name}: {b.available_balance}");
    },
    onError: ex => Debug.LogError(ex)
);
```

**Response model:**

```csharp
public class PlayerBalanceResponse {
    public Player player;
    public List<Balance> balances;
    public Summary summary;
    public string last_updated;
}

public class Balance {
    public int currency_id;
    public string currency_name;
    public string available_balance;   // SPENDABLE — display this one
    public string reserved_balance;    // locked by in-flight sends/transfers
    public string total_balance;       // available + reserved. NOT spendable.
}
```

> **Display `available_balance`, never `total_balance`.** `total_balance` includes funds already committed to pending sends. Showing it makes a purchase look affordable right up until the server refuses it, which is how players end up in insufficient-balance lockouts.

Balances arrive as **decimal-formatted strings**, not numbers. Parse with `InvoFormat.TryParseAmount` into a `decimal`.

**Polling.** `APIManager` polls the balance for `config.playerEmail` and writes `available_balance` into an optional `playerbalance` label. Polling is **single-flight** — one request in flight at a time — with exponential backoff up to `maxBalancePollInterval` on failure, honouring `Retry-After`. Control it with `StartBalancePolling()` / `StopBalancePolling()`; `StopBalancePolling()` lets an in-flight request finish.

### Game Items Catalog

```csharp
await api.GetGameItemsAsync(
    onSuccess: response => {
        foreach (var item in response.items)
            Debug.Log($"{item.item_name}: ${item.price_usd}");
    },
    onError: ex => Debug.LogError(ex)
);
```

**Response model (abridged):**

```csharp
public class GameItem {
    public string item_id;
    public string game_id;              // owning game, as a STRING; null for template items
    public string item_name;
    public string display_name;
    public string item_category;        // featured, weekly, daily, standard
    public string item_type;            // bundle, currency_pack, daily_deal, special_offer
    public string image_url;
    public bool is_featured;
    public bool is_most_popular;
    public bool is_best_value;
    public int? discount_percentage;    // INTEGER 0-100, not a float
    public float price_usd;
    public float? original_price_usd;
    public double currency_amount;
    public double bonus_amount;
    public string item_rarity;
    public bool is_limited_time;
    public int? time_remaining_seconds;
    public string time_remaining_display;   // "2d 3h 15m", "15m", "Expired"
    public JObject metadata;                // arbitrary JSONB — values are not all strings
}
```

> **This returns all active items across all games, not just yours.** Filter on `game_id` if you only want your own. `discount_percentage` is a nullable **int**, not a float.

> **The server catalog and the item-purchase UI are separate paths.** `GetGameItemsAsync()` returns real-money `GameItem`s destined for hosted checkout. The item purchase panels read a **local** `InvoSDKItemCatalog` ScriptableObject of `InvoSDKItem`s priced in game currency. They do not interoperate and no mapper is provided — the two `price` fields are in different units, so a naive mapper is a silent-mispricing hazard. See [InvoSDK-README §8](InvoSDK-README.md#8-catalog-and-item-purchase-are-separate-paths).

### Item Purchases

Spends the player's **in-game currency**. For real money, see [Hosted Checkout](#hosted-checkout-real-money-purchases).

```csharp
Task<PurchaseItemResponse> PurchaseItemAsync(
    string clientRequestId,
    string playerEmail,
    string playerName,
    string itemId,
    string itemName,
    int quantity,
    decimal unitPrice,
    decimal totalPrice,
    string itemCategory = null,
    string itemDescription = null,
    Action<PurchaseItemResponse> onSuccess = null,
    Action<InvoApiException> onError = null)
```

```csharp
await api.PurchaseItemAsync(
    clientRequestId: requestId,          // minted once per intent, reused across retries
    playerEmail:     "player@email.com",
    playerName:      "PlayerOne",
    itemId:          "item_001",
    itemName:        "Power Boost",
    quantity:        1,
    unitPrice:       4.99m,              // decimal, not float
    totalPrice:      4.99m,
    itemCategory:    "consumable",       // optional
    itemDescription: "Restores 50 energy",
    onSuccess: r => Debug.Log($"Transaction: {r.transaction_id}, balance: {r.new_balance}"),
    onError:   ex => Debug.LogError(ex)
);
```

Prices are `decimal` and are serialized through `InvoFormat.Amount`, so they always go out with a period separator. The backend requires `total_price == unit_price × quantity` exactly, both positive and under 999999.99.

Read the post-purchase balance from the **top-level** `new_balance`. `balance_info.new_balance` is legacy and retained only for older callers. Check `response.IsSuccess` before granting the item — it is an ordinal, case-insensitive equality against `"success"`.

### Currency Transfers — a player moving their own balance between games

```csharp
Task<AvailableDestinationsResponse> GetTransferDestinationsAsync(...)
Task<InitiateTransferResponse>      InitiateTransferAsync(
        string clientRequestId, string sourceName, string sourceEmail, string sourcePhone,
        string targetPhone, string targetEmail, string targetGameId, string amount, ...)
Task<VerifySmsResponse>             VerifyTransferSmsAsync(string transactionId, string smsPin, ...)
Task<ResendPinResponse>             ResendTransferPinAsync(string transactionId, ...)
Task<TransactionStatusResponse>     GetTransferStatusAsync(string transactionId, ...)
Task<ClaimTransferResponse>         ClaimTransferAsync(
        string claimCode, string targetPlayerName, string targetPlayerEmail,
        string targetPlayerPhone, string targetCurrencyId, ...)
```

### Currency Sends — one player to another, addressed by phone

```csharp
Task<AvailableDestinationsResponse> GetSendDestinationsAsync(...)
Task<InitiateSendResponse>          InitiateSendAsync(
        string clientRequestId, string senderName, string senderEmail, string senderPhone,
        string receiverPhone, string receiverEmail, string receivingGameId, string amount, ...)
Task<VerifySmsResponse>             VerifySendSmsAsync(string transactionId, string smsPin, ...)
Task<ResendPinResponse>             ResendSendPinAsync(string transactionId, ...)
Task<TransactionStatusResponse>     GetSendStatusAsync(string transactionId, ...)
Task<ClaimCurrencyResponse>         ClaimCurrencyAsync(
        string claimCode, string receiverName, string receiverEmail, string receiverPhone,
        int? receiverPlayerId = null, ...)
```

> ### Sends and transfers are not interchangeable
>
> They are two distinct backend flows with two distinct sets of routes. The backend filters on transaction type inside its lookup, so submitting a **transfer's** id to the **sends** verifier returns a bare `404` with no useful message.
>
> The old ambiguous `VerifySmsAsync` and `GetAvailableDestinationsAsync` caused exactly that bug and have been split into explicitly named pairs: `VerifySendSmsAsync` / `VerifyTransferSmsAsync`, and `GetSendDestinationsAsync` / `GetTransferDestinationsAsync`.

#### Initiating

```csharp
_clientRequestId ??= APIManager.NewClientRequestId();

string senderPhone   = InvoPhone.Normalize(rawSenderPhone);
string receiverPhone = InvoPhone.Normalize(rawReceiverPhone);
if (senderPhone == null || receiverPhone == null) { /* show InvoPhone.DescribeProblem(...) */ return; }

var response = await api.InitiateSendAsync(
    clientRequestId: _clientRequestId,
    senderName:      api.GetPlayerName(),
    senderEmail:     api.GetPlayerEmail(),
    senderPhone:     senderPhone,
    receiverPhone:   receiverPhone,
    receiverEmail:   null,                 // optional; omitted from the body when blank
    receivingGameId: targetGameId,
    amount:          InvoFormat.Amount(amount));

// A 2xx does not mean "proceed to the PIN screen" — check the status first.
switch (response.status)
{
    case InvoStatus.Success:
        if (response.verification_method == "sms") ShowPinScreen(response.transaction_id);
        else                                       ShowInAppApprovalScreen(response.transaction_id);
        break;

    case InvoStatus.PendingGuardianApproval:      // HTTP 202
        ShowGuardianWaitingScreen(response.guardian_approval);
        break;

    case InvoStatus.PendingConfirmation:          // HTTP 202
        ShowRecipientConfirmationScreen();
        break;
}
```

`response.transaction_id` is what every subsequent call in the flow keys off. `send_details.fees_preview` (or `transfer_details.fees_preview`) carries the authoritative fees — display those, not a local estimate.

#### Verifying

```csharp
var verified = await api.VerifySendSmsAsync(transactionId, "123456");
Debug.Log($"Claim code: {verified.claim_code}");        // never log this in a shipping build
```

The PIN allows **3 attempts**. `ResendSendPinAsync` / `ResendTransferPinAsync` answer `2xx` with `status == "resent"` on success, but the same route also answers `2xx` with a cooldown — read `retry_after` before re-enabling the button.

#### Claiming

```csharp
var claimed = await api.ClaimCurrencyAsync(
    claimCode:        "ABC123",
    receiverName:     "ReceiverPlayer",
    receiverEmail:    "receiver@email.com",
    receiverPhone:    InvoPhone.Normalize(rawReceiverPhone),   // SAME value used at initiate
    receiverPlayerId: null);

if (claimed.status == InvoStatus.NeedsAccountSelection)
{
    // HTTP 200, but NOTHING was credited. Show a picker and call again with the chosen id.
    ShowAccountPicker(claimed.candidates);   // AccountCandidate { player_id, email_hint }
    return;
}

Debug.Log($"New balance: {claimed.new_balance}");
```

`ClaimTransferAsync` requires all five of `claimCode`, `targetPlayerName`, `targetPlayerEmail`, `targetPlayerPhone`, `targetCurrencyId`.

> **Claims authenticate with the *receiving* game's key.** This plugin holds one key. Same-game (peer-to-peer) claims work; a cross-game claim `404`s with "not intended for this game", because the transaction belongs to a different `to_game_id`. The claim must run in the receiving game, on the receiving player's device, with that game's key. See [InvoSDK-README §4](InvoSDK-README.md#4-cross-game-claims).

**Peer-to-peer sends.** The destinations endpoint omits your own game, but `initiate-send` permits same-game sends. Pass `receivingGameId: api.GameId` directly.

### `verification_method`

`InitiateSendResponse` and `InitiateTransferResponse` both carry `verification_method`, always present, either `"sms"` or `"in_app"`.

When it is `"in_app"` the proactive **SMS PIN is suppressed**. Showing a PIN screen leaves the player waiting for a text that was never sent. The bundled panels branch on this and fall back to polling the status endpoint until `verification_state` reads `"approved"`.

### Utility Methods

```csharp
Sprite sprite = await APIManager.LoadSpriteAsync(imageUrl, targetImage);

string email    = api.GetPlayerEmail();
string name     = api.GetPlayerName();
string gameName = api.GetGameName();
string currency = api.GetGameCurrency();
```

---

## Error Handling

Any non-2xx response throws `InvoApiException`. The exception is also handed to `onError` first, if you supplied one.

```csharp
try
{
    await api.PurchaseItemAsync(...);
}
catch (InvoApiException ex)
{
    if (ex.IsDuplicate)          { /* 409 — the purchase ALREADY SUCCEEDED. Treat as success. */ }
    else if (ex.IsRateLimited)   { /* 429 — back off for ex.RetryAfterSeconds */ }
    else if (ex.IsNetworkError)  { /* 0 — never reached the server; safe to retry with the SAME id */ }
    else                         { /* inspect ex.ErrorCode */ }

    Debug.LogError(ex);   // ToString() is log-safe: status, error_code, error_id, message. No body.
}
```

| Member | Meaning |
|--------|---------|
| `StatusCode` | HTTP status. **`0` means the request never reached the server** (offline, DNS, TLS). |
| `ErrorCode` | Machine-readable code from the body, e.g. `PHONE_SHARE_APPROVAL_REQUIRED`. May be null. |
| `ErrorId` | Support correlation id. Quote it to Invo support — it maps to a server log entry. |
| `RetryAfterSeconds` | From the body (`retry_after` / `retry_after_seconds`) or the `Retry-After` header. |
| `Body` | Raw response body. **May contain PII.** |
| `IsDuplicate` | `StatusCode == 409` |
| `IsRateLimited` | `StatusCode == 429` |
| `IsNetworkError` | `StatusCode == 0` |

**Match on `ErrorCode`, never on message text.** Messages are not a stable contract.

**Never render `ex.Body` to a player.** A `409 PHONE_SHARE_APPROVAL_REQUIRED` body contains another user's phone number and masked email.

> ### A 409 on item purchase means the purchase went through
>
> Replaying a `client_request_id` returns `409` **with the original transaction**. That is the deduplication working as designed — the money moved on the first attempt.
>
> Rendering it as "Purchase Failed" produces a support ticket and, once the player disputes the charge they were told did not happen, a **chargeback** on a completed sale. Treat `ex.IsDuplicate` on a purchase as success and reconcile the balance.

A 2xx body that cannot be deserialized throws with `ErrorCode == "SDK_RESPONSE_PARSE_ERROR"` and the body preserved on the exception.

---

## Not Every 2xx Is a Success

The API deliberately returns **HTTP 200 and 202 for outcomes that are not completion**. `APIManager` decides success from the HTTP status only and hands you the parsed body; branching on the `status` **string** is yours to do.

| `status` | HTTP | Meaning |
|---|---|---|
| `success` | 200 / 201 | Completed. |
| `needs_account_selection` | **200** | Several accounts share the receiver's phone. **Nothing was credited** — the session is rolled back. Show a picker from `candidates` and call again with the chosen player id. |
| `pending_guardian_approval` | **202** | The player is a minor. Held up to 15 minutes for a guardian's SMS reply. Do **not** advance to the PIN screen. |
| `pending_confirmation` | **202** | Recipient identity step-up in progress. |
| `resent` | 200 | A PIN resend succeeded. The same route also returns 2xx for a cooldown — read `retry_after`. |

Use the `InvoStatus` constants rather than string literals:

```csharp
public static class InvoStatus
{
    public const string Success                 = "success";
    public const string NeedsAccountSelection   = "needs_account_selection";
    public const string PendingGuardianApproval = "pending_guardian_approval";
    public const string PendingConfirmation     = "pending_confirmation";
    public const string Resent                  = "resent";
}
```

You cannot detect minor status from your side, so **handle 202 defensively on every initiate call**, in every title, whether or not you think you have underage players.

Also watch for **`403 GUARDIAN_REQUIRED`** — a minor with no usable guardian on file. That one is **terminal**, not a waiting state: do not poll, do not retry, tell the player the transfer cannot proceed.

---

## Phone Numbers

Use `InvoPhone`:

```csharp
string phone = InvoPhone.Normalize(rawInput);      // strict E.164, or null
if (phone == null)
{
    ShowFieldError(InvoPhone.DescribeProblem(rawInput));
    return;                                        // do NOT send
}
```

`Normalize` returns `"+"` followed by 10–15 digits, or **`null`** when the input cannot be valid. `IsValid` and `Mask` (keeps the last four digits, for display and logging) are also available.

The API **will not infer a country code**, and neither does this SDK. Two rules matter:

- **Never truncate.** A previous version capped numbers at 12 digits, which turned a legitimate 13–15 digit international number into a *different, valid* number. The API accepts it, the claim code binds to a phone that does not exist, and you get a 2xx with no error anywhere in the system.
- **Never guess a country code.** A number without a leading `+` is rejected locally rather than silently prefixed.
- **Use the same normalized value at initiate and at claim.** The backend compares digits exactly and returns `403` on a mismatch.

---

## Money and Locales

Use `InvoFormat` for everything that touches the wire:

```csharp
string wire = InvoFormat.Amount(9.99m);                       // always "9.99"
bool ok     = InvoFormat.TryParseAmount(userInput, out decimal amount);   // accepts "9,99" too
```

`value.ToString("F2")` uses the **device** locale. On a German, French, Spanish, Portuguese, Italian, Russian or Turkish device that emits `"9,99"`, which the backend's decimal parser rejects with `400 "Invalid price format"` — and that failure increments the player's failed-attempt counter toward an anti-abuse lockout. A locale bug becomes a lockout bug.

Prefer `decimal` over `float` for money throughout. `PurchaseItemAsync` takes `decimal` for exactly this reason.

Note that **balances and amounts come back as strings**, not numbers, and may be null. Parse them with `InvoFormat.TryParseAmount`; do not model them as `decimal` in your own DTOs.

---

## Idempotency

Every write takes a caller-supplied `clientRequestId`. Mint it **once per user intent** and reuse it across every retry of that intent:

```csharp
// When the user confirms — once.
_clientRequestId ??= APIManager.NewClientRequestId();

// On every attempt, including retries after a timeout.
await api.InitiateSendAsync(_clientRequestId, ...);
```

Minting a fresh id per attempt defeats server-side deduplication: a retry after a dropped response becomes a genuinely new transaction, with the funds reserved twice.

This is why a **network error is safe to retry** with the same id, and why a **409 means you already succeeded** — the two facts are the same mechanism seen from either side.

Do not use a `client_request_id` beginning with `sub_`; that prefix is reserved for subscription renewals and is rejected with `400 CLIENT_REQUEST_ID_RESERVED`.

---

## Complete API Endpoints

All paths are relative to the environment base — `https://invo.network/api` or `https://sandbox.invo.network/sandbox/api`.

| Method | HTTP | Endpoint |
|--------|------|----------|
| `GetPlayerBalanceAsync` | GET | `/player-balances/player/by-email/{email}` |
| `GetGameItemsAsync` | POST | `/v1/game-items/list` |
| `PurchaseItemAsync` | POST | `/item-purchases/purchase-item` |
| `GetSendDestinationsAsync` | POST | `/currency-sends/available-destinations` |
| `InitiateSendAsync` | POST | `/currency-sends/initiate-send` |
| `VerifySendSmsAsync` | POST | `/currency-sends/verify-sms` |
| `ResendSendPinAsync` | POST | `/currency-sends/resend-pin` |
| `GetSendStatusAsync` | GET | `/currency-sends/{transaction_id}/status` |
| `ClaimCurrencyAsync` | POST | `/currency-sends/claim-currency` |
| `GetTransferDestinationsAsync` | POST | `/transfers/available-destinations` |
| `InitiateTransferAsync` | POST | `/transfers/initiate-transfer` |
| `VerifyTransferSmsAsync` | POST | `/transfers/verify-sms` |
| `ResendTransferPinAsync` | POST | `/transfers/resend-pin` |
| `GetTransferStatusAsync` | GET | `/transfers/{transaction_id}/status` |
| `ClaimTransferAsync` | POST | `/transfers/claim-transfer` |

Not called by the plugin, but part of a complete integration — see [InvoSDK-README](InvoSDK-README.md#what-you-must-wire-yourself):

| Purpose | HTTP | Endpoint |
|---|---|---|
| Mint a hosted-checkout session (server-side) | POST | `/api/checkout/sessions` |
| Mint a 15-minute player token (server-side) | POST | `/api/sdk/player-token` |
| Approve a phone share after a 409 | POST | `/api/wallet/phone-share/approve` |
| Poll guardian approval | GET | `/api/transactions/{id}/approval-status` |

---

## UI Components

Under `Assets/InvoSDK/Scripts/UI/`. Treat them as **working references**, not a finished storefront.

```
Assets/InvoSDK/Scripts/UI/
├── Core/
│   └── InvoSDKWindowManager.cs      # Panel navigation
├── Config/
│   └── InvoSDKWindowConfig.cs
├── ItemPurchase/
│   ├── ItemPurchasePanel.cs         # Item grid (local catalog)
│   ├── ItemPurchaseConfirmPanel.cs  # Purchase confirmation
│   └── ItemCardView.cs              # Individual item card
├── Windows/
│   ├── SenderCurrencyPanel.cs       # Currency send UI
│   ├── InvoSDKFeaturedPanel.cs      # Real-money store + hosted checkout
│   └── FeaturedItemCard.cs
├── Featured/
│   ├── FeatureDailyItemCard.cs
│   └── FeatureWeeklyItemCard.cs
├── Components/
│   ├── InvoStepController.cs
│   ├── InvoUIButtonBinder.cs
│   └── VerificationCodeInput.cs
├── TransferCurrencyPanel.cs         # 4-step transfer wizard
└── TransferStep.cs
```

### Transfer Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    CURRENCY TRANSFER FLOW                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐       │
│  │ Step 1  │───▶│ Step 2  │───▶│ Step 3  │───▶│ Step 4  │       │
│  │         │    │         │    │         │    │         │       │
│  │ Enter   │    │ Review  │    │ Verify  │    │ Success │       │
│  │ Details │    │ Summary │    │         │    │ Confirm │       │
│  │         │    │         │    │ • SMS   │    │         │       │
│  │ • From  │    │ • Fees  │    │   PIN,  │    │ • Claim │       │
│  │ • To    │    │ • Net   │    │   or    │    │   code  │       │
│  │ • Amount│    │ • Total │    │ • in-app│    │ • Expiry│       │
│  │         │    │         │    │ • or    │    │         │       │
│  │         │    │         │    │ guardian│    │         │       │
│  └─────────┘    └─────────┘    └─────────┘    └─────────┘       │
│                                                                 │
│  [TransferCurrencyPanel.cs — one 4-step wizard]                 │
│                                                                 │
│  Step 3 is NOT always a PIN screen. It branches on              │
│  verification_method and on a 202 guardian-approval status.     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Scene wiring

Several fields in the bundled `MainScene` are unassigned, including the transfer panel's phone inputs and status label. The panels now log a **clear error naming the specific missing field** rather than throwing a swallowed `NullReferenceException` — but you still have to assign them in the Inspector.

Optional fields worth wiring on `TransferCurrencyPanel`: `senderPhoneErrorText`, `receiverPhoneErrorText`, `amountErrorText`, `destinationsStatusText`, `feeEstimateNoticeText`, `resendPinButton`, `resendPinLabelText`, `inAppApprovalGroup`, `guardianApprovalGroup`, `claimCodeText`, `claimCodeExpiryText`.

> **`claimCodeText` is effectively required.** Without it the recipient never sees the claim code and the transfer cannot be completed. The claim code is deliberately **never logged** — Unity writes `Player.log` to disk in release builds and Android logs to logcat, and claim codes are bearer credentials.

### Fees

Panels show a local estimate (`serviceFeePercent`) before initiate, then **replace it** with `fees_preview` from the response. Always display the returned figures: per-tenant rate overrides mean a client-side calculation can be wrong, and the send and transfer flows even use different key names for the three fee legs (`sending_game_fee`/`receiving_game_fee`/`platform_fee` versus `source_game_fee`/`target_game_fee`/`invo_fee`). `total_fee` and `net_amount` are common to both.

### Images

Catalog artwork must be served over **https**, and is skipped with a warning if it exceeds **4 MB** or **4096 px** on a side (`FeaturedItemCard.MaxImageBytes` / `MaxDimension`).

### Window Manager

```csharp
public class InvoSDKWindowManager : MonoBehaviour {
    public List<PanelEntry> panels;      // { string panelId; GameObject panel; }

    public void ShowPanel(string id);
    public void HideAll();
}
```

### Quantity

Purchases are fixed at quantity 1, though the API accepts 1–1000. To add a stepper, replace the `PurchaseQuantity` constant in `ItemPurchaseConfirmPanel`; `totalPrice` is already derived from it in `decimal`.

---

## Editor Tools

| Menu | Window |
|---|---|
| **InvoSDK → Setup Wizard** | Configuration, stray-config detection, production-key warning |
| **InvoSDK → Item Catalog** | Edit the local `InvoSDKItemCatalog` — items, prices, rarities, sprites |
| **InvoSDK → Test Purchase** | Fire a purchase against the configured environment |
| **InvoSDK → Check Player Balance** | Look up a balance without entering play mode |

These are **Editor-only** and never ship in a player build, so they use the SDK key directly — which is correct for a development tool.

Test Purchase handles `200` with `status: "requires_action"` correctly: that means 3-D Secure is required and **no money has been charged**. Use a test card that does not trigger 3DS (`pm_card_visa`) if you want a straight-through result.

---

## Security

### The key in `InvoSDKConfig` ships in your build

This is not a warning about carelessness. It is a description of how Unity works.

Unity bundles **everything** under a `Resources/` folder into the player. `InvoSDKConfig.asset` lives under `Assets/InvoSDK/Resources/`, so the value of `sdkKey` is present in `resources.assets` in every build you produce, on every platform. Anyone with a copy of your game can recover it with `strings` in about a minute. There is no obfuscation that changes this, because the process has to be able to read it at runtime.

### What an extracted key can do

An `ivsdk_` key is a **full tenant credential**, not a scoped client token. Someone holding it can:

- **charge cards** through your checkout, in your name;
- **move any player's balance** — initiate sends and transfers for accounts that are not theirs;
- **request a bank payout** of your accumulated developer revenue;
- **mint player tokens** for any player in your game (`POST /api/sdk/player-token`), and act as them;
- **reconfigure your webhooks**, redirecting your settlement notifications.

There is no per-scope restriction, no IP allowlist, and no rate limit that meaningfully constrains a determined holder of the key.

### There is no environment marker on the key

Sandbox and production keys are **visually identical**. Nothing in the string tells you which environment it belongs to, and no tooling will catch a misplaced value for you. A production key pasted into a sandbox build is a production key in a shipped binary. Check where a key came from before you paste it, every time.

### Also true of item pricing

`POST /api/item-purchases/purchase-item` validates only that `total_price == unit_price × quantity`, that both are positive, and that they are under 999999.99. It does **not** look up the item's real price — `item_id` is an opaque string.

That is sound for a trusted server-side caller, which is what the endpoint documents itself as. Called from a game client, it means a modified build sets its own prices.

### If a key leaks, rotate it with `immediate: true`

The default rotation keeps the old key working for a **7-day grace window**. That grace does two unhelpful things at once: it keeps the leaked key alive for a week, and it is **not honored uniformly across endpoints** — some accept the previous key during the window while others reject it immediately, so you cannot even rely on the grace period behaving as a grace period.

For a leaked key, take the hard cutover: rotate with `immediate: true` and update your server in the same change.

### The position, stated plainly

| | |
|---|---|
| Sandbox key in `InvoSDKConfig` | **Fine.** This is what the field is for. |
| Production key in `InvoSDKConfig` | **Never.** Treat any key you put there as public. |
| Production integration | **Requires a [server-side proxy](#server-side-proxy).** |

### Production Checklist

This is a build-order list, not a tick-box list. Items 1–3 are work you have to do before the rest are even meaningful.

- [ ] **1. Stand up your server** — see [Server-Side Proxy](#server-side-proxy). Nothing below is achievable without it.
- [ ] **2. Move the `ivsdk_` key to that server.** Clear `sdkKey` in the config asset. Confirm with `strings` on a built player that no `ivsdk_` string remains.
- [ ] **3. Point the client at your server**, not at `invo.network`. Authenticate the player with your own session before proxying anything.
- [ ] **4. Set prices server-side** from your own catalog. Never trust a client-supplied `unit_price`.
- [ ] **5. Host your checkout-session endpoint** and set it on every `InvoSDKFeaturedPanel` — [Hosted Checkout](#hosted-checkout-real-money-purchases).
- [ ] **6. Receive and verify webhooks.** HMAC-SHA256 over `"{timestamp}.{raw_body}"` in `X-Invo-Signature`, reject outside a 5-minute window, dedupe on `X-Invo-Idempotency-Key`. **This is your source of truth for real-money credit**, not the browser.
- [ ] **7. Handle the non-completion states** — 200 `needs_account_selection`, 202 `pending_guardian_approval`, 202 `pending_confirmation`, 403 `GUARDIAN_REQUIRED`, 409 `PHONE_SHARE_APPROVAL_REQUIRED`.
- [ ] **8. Treat a 409 on purchase as success.** Verify this in your own UI before launch.
- [ ] **9. Pass `client_request_id` through your proxy unchanged** so idempotency survives the extra hop.
- [ ] **10. Test the whole flow in sandbox first**, including the failure paths, on a comma-decimal locale device.
- [ ] **11. Confirm nothing logs** claim codes, SMS PINs, tokens, or raw response bodies.
- [ ] **12. Only then set `useProduction = true`.**

---

## Server-Side Proxy

> The Setup Wizard's "Read: shipping keys safely" button links here.

The rule that generates every requirement below:

> **The SDK secret key is a server-side credential. A Unity player build cannot hold it safely.**

The supported production topology:

```
Unity client ──(your session auth)──▶ Your server ──(X-Game-Secret-Key)──▶ Invo API
                                           │
                                           ◀──(webhook, HMAC-signed)───────┘
```

### Minimum viable proxy

In whatever stack you already use:

```
POST /invo/checkout-session   → mints a hosted checkout session
POST /invo/player-token       → mints a 15-minute player token
POST /invo/purchase-item      → validates your session, then proxies
POST /invo/initiate-transfer  → validates your session, then proxies
POST /invo/initiate-send      → validates your session, then proxies
POST /invo/claim              → proxies with the RECEIVING game's key
POST /invo/webhook            → receives Invo webhooks, verifies the signature
```

### Rules that matter

1. **The `ivsdk_` key exists only here.** Never in a build, never in a URL, never in a log, never in a repo.
2. **Authenticate the player yourself** before proxying. Invo authenticates your *game*, not your player. The API has no idea which of your players is calling.
3. **Set the price server-side.** Look the item up in your own catalog; do not trust a client-supplied `unit_price`.
4. **Pass the client's `client_request_id` through unchanged**, so idempotency survives the extra hop.
5. **Treat the webhook as the source of truth** for real-money credit.

### Player tokens

`POST /api/sdk/player-token` with your secret and `{"player_email": "..."}` returns a **15-minute** token scoped to one player, plus an opaque `identity_id`. There is **no refresh call** — on a `401`, mint a fresh one. That token is what the `/api/sdk/*` routes read. The plugin does not use those routes today; see [InvoSDK-README §3](InvoSDK-README.md#3-in-app-verification-and-passkeys-optional-not-implemented).

---

## Hosted Checkout (Real-Money Purchases)

### What changed

The `console.invo.network/stripe-checkout/?game_secret=…` pattern has been **removed from the plugin, with no fallback**. It was wrong twice over: it placed your master secret in a **browser URL** — which lands in history, referrers and proxy logs — and it no longer functions anyway, because the path redirects and the checkout page reads only `?session=`.

Real-money purchases now use hosted checkout sessions, and minting one requires the secret, which means it happens on **your** server.

### The flow

```
┌───────────────────────────────────────────────────────────────────────────┐
│                        HOSTED CHECKOUT FLOW                               │
├───────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  Unity            Your Server              Invo API           Browser     │
│    │                   │                       │                  │       │
│    │─POST {player_email, usd_amount}─▶         │                  │       │
│    │                   │                       │                  │       │
│    │                   │─POST /api/checkout/sessions──▶           │       │
│    │                   │   X-Game-Secret-Key   │                  │       │
│    │                   │◀──201 {checkout_url}──│                  │       │
│    │◀──{checkout_url}──│                       │                  │       │
│    │                   │                       │                  │       │
│    │──────────Application.OpenURL(checkout_url)──────────────────▶│       │
│    │                   │                       │◀───pays──────────│       │
│    │                   │◀══ WEBHOOK (signed) ══│                  │       │
│    │                   │  ↑ THIS is the truth  │                  │       │
│    │◀─your own "credit granted" signal─│       │                  │       │
│                                                                           │
└───────────────────────────────────────────────────────────────────────────┘
```

### 1. Configure the plugin

Set **`checkoutSessionEndpoint`** to an **https** URL on your server — once in **InvoSDK → Setup Wizard**. An individual `InvoSDKFeaturedPanel` can override it in the Inspector ("Real-Money Checkout"); blank means "use the shared config value". `checkoutTimeoutSeconds` alongside it clamps to 5–120 seconds.

The plugin POSTs:

```json
{ "player_email": "player@email.com", "usd_amount": "9.99" }
```

and expects:

```json
{ "checkout_url": "https://..." }
```

The returned URL must be **https** — the panel rejects anything else, because a checkout URL carries a signed session token and must not travel in clear. If the endpoint is unset or is not https, the panel shows "Real-money purchases need a server endpoint — see README" and logs an explanatory error once.

### 2. Implement the endpoint

Server-side, call:

```
POST https://invo.network/api/checkout/sessions
Headers: X-Game-Secret-Key: ivsdk_…
Body:    { "player_email", "usd_amount", "rail": "platform",
           "success_url", "cancel_url", "metadata": { … } }

→ 201 { "session_id", "checkout_url", "expires_at", "expires_in_seconds": 900 }
```

Constraints:

- `usd_amount` must be **greater than 0 and at most 999.99**
- `metadata` under 8 KB
- the returned JWT is **signed, single-use, and expires in 15 minutes**
- the path is `/api/checkout/sessions`. Some older docs say `/api/checkout-sessions` — **that does not exist.**

`rail: "platform"` gives cards plus Apple Pay, Google Pay and Link with no app-store commission. **That is a fee statement, not a policy clearance** — Apple and Google generally require in-app purchase for digital goods. Review a WebView checkout with counsel before shipping to either store.

### 3. Confirm the credit from the webhook, not the browser

The checkout page posts an `INVO_CHECKOUT_COMPLETE` message to `'*'` with **no origin validation**, so any page that can frame the checkout could forge it. Treat it as a UX hint — dismiss a spinner, show a "thanks" screen — and nothing more.

**Grant currency on the server-to-server webhook.** Verify `X-Invo-Signature` (HMAC-SHA256 over `"{timestamp}.{raw_body}"`), reject anything outside a 5-minute window, and dedupe on `X-Invo-Idempotency-Key`.

### WebView limitations

The bundled `WebViewObject` (GREE unity-webview) is a demo dependency, and it cannot carry this flow:

- **Unsupported on Windows, Linux and the Windows Editor.** You cannot test in-app checkout in a Windows Editor session at all.
- It implements `Unity.call(msg)` as `window.location = 'unity:' + msg`, which is **not** DOM `postMessage` — so `INVO_CHECKOUT_COMPLETE` never reaches Unity even in principle.
- It delivers to `CallFromJS(string)` with **no origin**, so origin validation is impossible on the Unity side.

For an in-app completion signal you need native bridges — an iOS `WKScriptMessage` handler and an Android `addJavascriptInterface`. Neither ships here. The plugin therefore opens checkout with `Application.OpenURL` (the system browser) and relies on your webhook.

### Platform support

| Platform | Checkout | Notes |
|----------|----------|-------|
| iOS | System browser via `Application.OpenURL` | Review store policy before shipping |
| Android | System browser via `Application.OpenURL` | Review store policy before shipping |
| macOS / Windows / Linux | System browser | Bundled WebView is unsupported on Windows and Linux |
| Unity Editor | System browser | Bundled WebView is unsupported in the **Windows** Editor |

---

## Hosted approval on mobile (system browser)

Transfers, sends and claims can be approved with a passkey on **Invo's hosted approval page** instead of an SMS PIN. On iOS and Android the plugin opens that page in the **system browser** and learns when it is done. Nothing in the plugin can move money: settlement happens on your server, with a value the client never sees.

### Two halves

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                       HOSTED APPROVAL (MOBILE)                               │
├──────────────────────────────────────────────────────────────────────────────┤
│  Unity                Your Server                 Invo API        Browser    │
│    │                      │                          │               │       │
│    │──"approve tx 123"───▶│                          │               │       │
│    │                      │─POST /api/sdk/approvals/device/begin──▶  │       │
│    │                      │  {transaction_id, flow, channel:"app_browser"}   │
│    │                      │◀─{device_code, verification_uri_complete,        │
│    │                      │    user_code, expires_in, interval}      │       │
│    │◀─handoff (NO device_code)                       │               │       │
│    │                      │                          │               │       │
│    │──InvoHostedApproval.OpenHostedApproval(verification_uri_complete)──▶│   │
│    │                      │                          │◀──passkey─────│       │
│    │                      │──poll (device_code)─────▶│               │       │
│    │◀─poll relay {enrollment?}                       │               │       │
│    │  match-code prompt → decision                   │               │       │
│    │──decision───────────▶│─POST .../confirm-enrollment {device_code,decision}│
│    │                      │                          │               │       │
│    │◀════ invo-sdk-<game_id>://done  (carries NOTHING) ═════════════│       │
│    │──"poll now"─────────▶│──poll → approved────────▶│               │       │
│    │                      │─POST /transfers/<id>/approve {device_code}──▶    │
└──────────────────────────────────────────────────────────────────────────────┘
```

**Server half (yours).** Call `POST /api/sdk/approvals/device/begin` with `{transaction_id, flow, "channel": "app_browser"}`. The response is RFC 8628-shaped: keep `device_code` on the server — it is what polls and what settles — and hand the client only `verification_uri_complete`, `user_code`, `expires_in` and `interval` (`HostedApprovalHandoff`). **Start polling on `interval` the moment `begin` returns**, not when the client says the page is done: the page can finish while the game is backgrounded, and the return signal is only a hint to poll sooner. When the poll says approved, call the approve endpoint with `device_code`.

**Client half (this plugin).** `InvoHostedApproval` does two things and nothing else:

```csharp
using InvoSDK;

public class ApprovalScreen : MonoBehaviour
{
    void OnEnable()
    {
        InvoHostedApproval.ApprovalPageFinished += OnPageFinished;
        // Cold start: the browser returned while the game was not running. The plugin's
        // startup listener latched it; nobody was subscribed yet.
        if (InvoHostedApproval.HasPendingReturn) OnPageFinished();
    }

    void OnDisable()  { InvoHostedApproval.ApprovalPageFinished -= OnPageFinished; }  // always unsubscribe

    void Approve(HostedApprovalHandoff handoff)   // handoff came from YOUR server's begin call
    {
        // iOS: an authentication session (a system-owned sheet; Safari on <13).
        // Android: Custom Tabs when androidx.browser is in the build, else the default browser.
        // Never an embedded WebView: WebAuthn does not run in one, so the passkey ceremony
        // would fail before it started.
        if (!InvoHostedApproval.OpenHostedApproval(handoff.verification_uri_complete))
            ShowFallbackCode(handoff.user_code);   // https + numeric gameId are required
    }

    void OnPageFinished()
    {
        InvoHostedApproval.ClearPendingReturn();
        MyServer.PollSoonerPlease();   // wake-up only; your server was already polling
    }
}
```

- `ApprovalPageFinished` fires when the page navigates to `invo-sdk-<game_id>://done`, when the iOS sheet closes, or when the game comes back to the foreground while a return is outstanding (a back-press out of a Custom Tab, a browser that refuses the custom scheme, another app that owns it — the game never hangs). It means **"poll now"** — never "approved". Approved, denied and expired all return on the same bare URL; the truth is your server's poll. The plugin reads nothing beyond the scheme, so a value smuggled onto the URL cannot tell the game anything.
- `HasPendingReturn` is the same signal as a latch, for the cold start and for any scene that subscribes late. Clear it once you have acted on it.
- `ApprovalPageDismissed` (iOS only) fires when the player swipes the sheet away. Still poll; the phone may have finished on its own. If the grant is still pending, reopen the **same** `verification_uri_complete` — it is valid until `expires_in`.
- `Cancel()` cancels the iOS sheet and stops waiting; neither event fires for a session the game cancelled itself.
- Games with their own deep-link plumbing can call `InvoHostedApproval.HandleDeepLink(url)` or `NotifyApprovalPageFinished()` instead.

### First-time phones: no prompt on mobile

For `channel: "app_browser"` the phone running the page **is** the device that opened it, so Invo **auto-confirms** the enrolment: the poll goes straight from pending to approved and the game shows nothing. There is no "dismiss the sheet, answer on the game screen, reopen" dance on mobile.

The match-code prompt API below exists for games that run the same flow on a screen that is *not* the phone (tablet/desktop builds of a mobile title, where the poll carries `enrollment: {state, device_label, match_code, requested_at}`). It is not required on iOS/Android.

| `state` | The plugin shows | Player action |
|---|---|---|
| `awaiting_screen` | "Set up INVO on "`<device_label>`"? Code `<match_code>`. Say Yes only if the phone you just opened shows this code." with **Yes** / **No** | Decision goes to your server as `approve` / `deny` |
| `confirmed` | "Finishing on "`<device_label>`"…" with one button, **Stop it (wrong code)** | Stop sends `deny` — the server lets a deny override a pending confirm |
| `denied` / absent | nothing | — |

```csharp
// Each poll tick, relay the poll's `enrollment` block (null when absent). The plugin
// shows the prompt, hides it, or does nothing when the block is unchanged.
InvoHostedApproval.ApplyEnrollmentState(poll.enrollment, decision =>
    MyServer.ConfirmEnrollment(InvoHostedApprovalCore.DecisionWire(decision)));  // "approve" | "deny"
```

`ShowEnrollmentPrompt(label, code, onDecision)` and `ShowEnrollmentFinishing(label, onStop)` are also callable directly. The default surface is an IMGUI overlay (`InvoEnrollmentPromptOverlay`) that needs no prefab or canvas; it dims the screen visually but does not block uGUI / Input System touches underneath. To draw the prompt in your own UI (uGUI, UI Toolkit, TMP), implement `IInvoEnrollmentPromptView` and assign `InvoHostedApproval.PromptView` — the copy and labels are passed in, so your view only lays them out. Labels and codes come from the server and are sanitised for display (tags, control and Unicode format characters removed; the label is capped at 32 characters and quoted, codes at 64).

### Registering the return scheme

The return scheme is `invo-sdk-<gameId>`, derived from the numeric `gameId` in `InvoSDKConfig` and nothing else; the server derives the same value. `InvoHostedApprovalBuildPostprocessor` registers it automatically on every iOS and Android build (idempotent). If you maintain your own `Info.plist` / `AndroidManifest.xml`, these are the equivalent snippets — replace `12345` with your game id:

**iOS — `Info.plist`** (the postprocessor also links `AuthenticationServices.framework`; iOS 13+ for the authentication session, Safari below that):

```xml
<key>CFBundleURLTypes</key>
<array>
  <dict>
    <key>CFBundleURLName</key>
    <string>invo-sdk-12345</string>
    <key>CFBundleURLSchemes</key>
    <array>
      <string>invo-sdk-12345</string>
    </array>
  </dict>
</array>
```

**Android — `AndroidManifest.xml`**, inside the launcher `<activity>`:

```xml
<intent-filter>
  <action android:name="android.intent.action.VIEW" />
  <category android:name="android.intent.category.DEFAULT" />
  <category android:name="android.intent.category.BROWSABLE" />
  <data android:scheme="invo-sdk-12345" />
</intent-filter>
```

Custom Tabs are optional. Add `implementation 'androidx.browser:browser:1.8.0'` to your `mainTemplate.gradle` dependencies to get them; without it the plugin uses the default browser, which works the same way. A custom scheme can be claimed by another app on Android; that is why the callback carries nothing.

### What the plugin does not do

- It never holds `device_code`, never polls Invo directly, and never calls the approve endpoint. Those belong to your server.
- It does not open the page in the bundled `WebViewObject` or any WebView.
- It does not decide anything from the return URL.

---

## Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `Config not found! Please run Setup Wizard.` | No `InvoSDKConfig.asset` under a `Resources` folder. It is gitignored — create your own. |
| Config changes have no effect | A second `InvoSDKConfig.asset` under another `Resources` root. Delete the stray; the wizard will point at it. |
| `APIManager.Instance is null` | The `APIManager` component is not in the scene. |
| `400 "Invalid price format"` | A device locale emitted a comma decimal. Use `InvoFormat.Amount(...)`, and `decimal` not `float`. |
| `400 "Phone number must start with country code"` | Missing `+`. Use `InvoPhone.Normalize(...)` and surface `DescribeProblem(...)`. |
| Transfer verify returns `404` | A transfer id was sent to the sends verifier. Use `VerifyTransferSmsAsync`. |
| Claim returns `403` | The claim phone does not match the initiate phone digit-for-digit. |
| Claim returns `404` cross-game | Cross-game claims need the **receiving** game's key. See [InvoSDK-README §4](InvoSDK-README.md#4-cross-game-claims). |
| Purchase shows "failed" but the balance dropped | A `409` was rendered as an error. `ex.IsDuplicate` means it **succeeded**. |
| Player waits forever for an SMS that never arrives | `verification_method` was `"in_app"`. The PIN is suppressed — branch on it. |
| Flow stalls after initiate with no error | A `202` (`pending_guardian_approval` / `pending_confirmation`) was treated as success. Branch on `status`. |
| Claim "works" but credits nothing | HTTP 200 `needs_account_selection`. Show `candidates`, re-call with the chosen player id. |
| `401` on every call | Missing or wrong `sdkKey`, or a sandbox key against production. Not a session problem — there are no sessions. |
| `403 "Game is not active"` | `game_status` is not `live` or `testing`. Check the console. |
| Repeated `429` | An anti-abuse lockout. Respect `ex.RetryAfterSeconds` and stop re-enabling the button. |
| Real-money purchase does nothing | `checkoutSessionEndpoint` is not set in the Setup Wizard (or on the panel), or is not https. See [Hosted Checkout](#hosted-checkout-real-money-purchases). |
| Catalog image missing | Not https, over 4 MB, or over 4096 px. |
| JSON parse errors | Verify `com.unity.nuget.newtonsoft-json` is installed and included in the build. |

### Debug Logging

All SDK logs are prefixed with `[InvoSDK]`.

> **Unity writes `Player.log` to disk in release builds**, and Android logs to logcat. Never log claim codes, SMS PINs, player tokens, or raw response bodies. `InvoApiException.ToString()` is log-safe by design — it prints status, error code, error id and message, and deliberately omits `Body`. URLs containing a player email are redacted at `/by-email/`.

### Network Issues

`InvoApiException.IsNetworkError` (`StatusCode == 0`) means the request never reached the server. That is the one failure that is always safe to retry — **with the same `clientRequestId`**.

```csharp
if (Application.internetReachability == NetworkReachability.NotReachable) {
    ShowOfflineMessage();
    return;
}
```

---

## Examples

### Complete Purchase Flow

```csharp
using System;
using UnityEngine;
using InvoSDK;

public class ShopController : MonoBehaviour
{
    private string _clientRequestId;   // one per confirmed intent

    public async void OnBuyButtonClicked(InvoSDKItem item)
    {
        var api = APIManager.Instance;
        if (api == null) { Debug.LogError("APIManager missing from scene"); return; }

        // 1. Read the SPENDABLE balance.
        var balanceResponse = await api.GetPlayerBalanceAsync(api.GetPlayerEmail());
        if (balanceResponse.balances == null || balanceResponse.balances.Count == 0) return;

        if (!InvoFormat.TryParseAmount(balanceResponse.balances[0].available_balance,
                                       out decimal available))
            return;

        // 2. Price in decimal, derived exactly as the server will re-derive it.
        const int quantity   = 1;
        decimal   unitPrice  = (decimal)item.priceUSD;   // NB: this field holds GAME currency
        decimal   totalPrice = unitPrice * quantity;

        if (available < totalPrice)
        {
            ShowInsufficientBalance();
            return;
        }

        // 3. Mint the idempotency key ONCE, and keep it across retries.
        _clientRequestId ??= APIManager.NewClientRequestId();

        try
        {
            var response = await api.PurchaseItemAsync(
                clientRequestId: _clientRequestId,
                playerEmail:     api.GetPlayerEmail(),
                playerName:      api.GetPlayerName(),
                itemId:          item.itemId,
                itemName:        item.itemName,
                quantity:        quantity,
                unitPrice:       unitPrice,
                totalPrice:      totalPrice,
                itemCategory:    item.category,
                itemDescription: item.itemDescription);

            // 4. A 2xx is not automatically a completed purchase.
            if (!response.IsSuccess)
            {
                Debug.LogWarning($"[Shop] status '{response.status}' — not granting.");
                ShowRetry();
                return;
            }

            GrantItem(item.itemId);
            UpdateBalanceLabel(response.new_balance);   // top-level, canonical
            _clientRequestId = null;                    // intent complete
        }
        catch (InvoApiException ex) when (ex.IsDuplicate)
        {
            // 409: this exact request already went through. It is a SUCCESS.
            Debug.Log("[Shop] Duplicate request — already purchased.");
            GrantItem(item.itemId);
            _clientRequestId = null;
        }
        catch (InvoApiException ex) when (ex.IsRateLimited)
        {
            ShowCooldown(ex.RetryAfterSeconds ?? 60);
        }
        catch (InvoApiException ex)
        {
            // Keep _clientRequestId: retrying with it cannot double-charge.
            Debug.LogError(ex);                          // log-safe
            ShowRetry();
        }
    }

    private void GrantItem(string itemId) { /* your game logic */ }
    private void UpdateBalanceLabel(string newBalance) { }
    private void ShowInsufficientBalance() { }
    private void ShowCooldown(int seconds) { }
    private void ShowRetry() { }
}
```

### Currency Send Flow

```csharp
using UnityEngine;
using InvoSDK;

public class SendController : MonoBehaviour
{
    private string _transactionId;
    private string _clientRequestId;
    private string _verificationMethod;

    public async void StartSend(string rawReceiverPhone, string targetGameId, string rawAmount)
    {
        var api = APIManager.Instance;

        // Normalize before anything else. Never truncate, never guess a country code.
        string senderPhone   = InvoPhone.Normalize(SenderPhoneFromSession());
        string receiverPhone = InvoPhone.Normalize(rawReceiverPhone);

        if (receiverPhone == null)
        {
            ShowError(InvoPhone.DescribeProblem(rawReceiverPhone));
            return;
        }

        if (!InvoFormat.TryParseAmount(rawAmount, out decimal amount) || amount <= 0m)
        {
            ShowError("Enter a valid amount.");
            return;
        }

        _clientRequestId ??= APIManager.NewClientRequestId();

        try
        {
            var response = await api.InitiateSendAsync(
                clientRequestId: _clientRequestId,
                senderName:      api.GetPlayerName(),
                senderEmail:     api.GetPlayerEmail(),
                senderPhone:     senderPhone,
                receiverPhone:   receiverPhone,
                receiverEmail:   null,                       // optional
                receivingGameId: targetGameId,
                amount:          InvoFormat.Amount(amount));

            _transactionId      = response.transaction_id;
            _verificationMethod = response.verification_method;

            switch (response.status)
            {
                case InvoStatus.PendingGuardianApproval:      // HTTP 202 — do NOT show a PIN screen
                    ShowGuardianWaiting(response.guardian_approval);
                    break;

                case InvoStatus.PendingConfirmation:          // HTTP 202
                    ShowRecipientConfirmationWaiting();
                    break;

                case InvoStatus.Success:
                    // "in_app" SUPPRESSES the SMS PIN — a PIN screen would wait forever.
                    if (_verificationMethod == "in_app") ShowInAppApprovalAndPoll();
                    else                                 ShowPinEntry(response.verification_required);
                    break;

                default:
                    Debug.LogWarning($"[Send] Unexpected status '{response.status}'");
                    break;
            }
        }
        catch (InvoApiException ex) when (ex.ErrorCode == "PHONE_SHARE_APPROVAL_REQUIRED")
        {
            // 409: that phone belongs to another account and an OTP has been texted to its owner.
            // NEVER show ex.Body — it contains the other user's phone and masked email.
            ShowError("That number is registered to another account and needs approval.");
        }
        catch (InvoApiException ex) when (ex.StatusCode == 403 && ex.ErrorCode == "GUARDIAN_REQUIRED")
        {
            ShowError("This account needs a guardian on file before it can send currency.");  // terminal
        }
        catch (InvoApiException ex)
        {
            Debug.LogError(ex);
            ShowError("The send could not be started.");
        }
    }

    public async void VerifySend(string smsPin)
    {
        try
        {
            // Sends verify against the SENDS route. A transfer id here always 404s.
            var verified = await APIManager.Instance.VerifySendSmsAsync(_transactionId, smsPin);

            // Show it — never log it. Player.log persists in release builds.
            ShowClaimCode(verified.claim_code, verified.claim_instructions?.claim_code_expires_at);
            _clientRequestId = null;
        }
        catch (InvoApiException ex)
        {
            Debug.LogError(ex);
            ShowError("That code was not accepted.");   // 3 attempts allowed
        }
    }

    public async void ResendPin()
    {
        var result = await APIManager.Instance.ResendSendPinAsync(_transactionId);

        if (result.status == InvoStatus.Resent) ShowInfo("A new code is on its way.");
        else if (result.retry_after.HasValue)   ShowCooldown(result.retry_after.Value);
    }

    private string SenderPhoneFromSession() => "+15551234567";
    private void ShowError(string message) { }
    private void ShowInfo(string message) { }
    private void ShowCooldown(int seconds) { }
    private void ShowClaimCode(string code, string expiresAt) { }
    private void ShowPinEntry(object verificationRequired) { }
    private void ShowInAppApprovalAndPoll() { }
    private void ShowGuardianWaiting(object guardianApproval) { }
    private void ShowRecipientConfirmationWaiting() { }
}
```

---

## Changelog

### Unreleased — correctness and security pass

**Removed**

- The `/auth/login` + `/auth/csrf-token` handshake and the `Authorization: Bearer` / `X-CSRF-Token` headers. They were never read by any game API endpoint, and `/auth/login` authenticates developer-console accounts rather than players.
- The `playerPassword` config field, which existed only to feed that handshake.
- The `console.invo.network/stripe-checkout/?game_secret=…` checkout flow, which put the master secret in a browser URL and no longer functions. There is deliberately no fallback to it.

**Fixed**

- `VerifySmsAsync` split into `VerifySendSmsAsync` and `VerifyTransferSmsAsync`. The ambiguous name caused transfers to verify against the sends endpoint, which always returned `404`.
- `GetAvailableDestinationsAsync` split into `GetSendDestinationsAsync` and `GetTransferDestinationsAsync`.
- Phone numbers are no longer truncated at 12 digits — truncation turned valid international numbers into different, valid, wrong numbers.
- Amounts are formatted with `InvoFormat.Amount` (invariant culture), so comma-decimal locales no longer produce `400 "Invalid price format"`.
- Money is `decimal` rather than `float` throughout the purchase path.
- Balance polling is single-flight with exponential backoff, and displays `available_balance` rather than `total_balance`.
- Panels log the specific unassigned Inspector field instead of throwing a swallowed `NullReferenceException`.

**Added**

- `InvoApiException` with `ErrorCode`, `ErrorId`, `RetryAfterSeconds`, `IsDuplicate`, `IsRateLimited`, `IsNetworkError`, and a log-safe `ToString()`.
- `InvoStatus` constants for the non-completion 2xx states.
- `InvoFormat` and `InvoPhone`.
- `ClaimTransferAsync`, `ResendSendPinAsync`, `ResendTransferPinAsync`, `GetSendStatusAsync`, `GetTransferStatusAsync`.
- `NewClientRequestId()`, and a caller-supplied `clientRequestId` on every write.
- `StartBalancePolling()` / `StopBalancePolling()`.
- `checkoutSessionEndpoint` and the hosted-checkout flow.
- `verification_method` and guardian-approval handling in the bundled panels.
- `InvoHostedApproval`: system-browser passkey approval on iOS/Android with the `invo-sdk-<gameId>://done` return, the match-code prompt (`IInvoEnrollmentPromptView` + IMGUI default), and a build postprocessor that registers the scheme. See [Hosted approval on mobile](#hosted-approval-on-mobile-system-browser).

### Version 1.0.0

- Initial release

---

## Support

### Resources

| Resource | Link |
|----------|------|
| Plugin specification and limitations | [InvoSDK-README.md](InvoSDK-README.md) |
| Documentation | [docs.invo.network](https://docs.invo.network) |
| Developer Console | [console.invo.network](https://console.invo.network) |
| Sandbox Console | [dev.console.invo.network](https://dev.console.invo.network) |
| API Reference | [docs.invo.network/api](https://docs.invo.network/api) |

### Getting Help

- **Technical Issues**: support@invo.network — quote `InvoApiException.ErrorId`, which maps to a server log entry
- **Account/Billing**: billing@invo.network
- **Partnership Inquiries**: partners@invo.network

### Community

- [Discord Server](https://discord.gg/invo)
- [Twitter @InvoNetwork](https://twitter.com/invonetwork)
- [GitHub Issues](https://github.com/Invo-Technologies/Invo-unity-sdk/issues)

---

## License

Released under the MIT License.

---

<p align="center">
  <strong>Built by the Invo Network Team</strong>
</p>

<p align="center">
  <a href="https://invo.network">Website</a> •
  <a href="https://docs.invo.network">Docs</a> •
  <a href="https://twitter.com/invonetwork">Twitter</a> •
  <a href="https://discord.gg/invo">Discord</a>
</p>
