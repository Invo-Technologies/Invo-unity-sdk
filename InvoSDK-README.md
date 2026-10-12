# InvoSDK Unity Plugin

Unity package (Unity 2021.3 LTS or newer) for the Invo platform: player balances, a store (top up
with real money, spend on items), cross-game transfers and player-to-player sends approved on the
player's phone, collecting what was sent to you, and phone-number changes. Runs on Windows, macOS,
Linux, Android, iOS, WebGL and consoles.

> **3.1.0** is a package (`Packages/com.invonetwork.unity-sdk`) with a new themeable UI, separate
> Sandbox and Production settings, and the 3.0.0 UI work done: the prefabs are wired and the SMS
> widgets are gone. Upgrading from a copied `Assets/InvoSDK` folder? See
> [Upgrading from 3.0](#upgrading-from-30).

> **Read [What You Must Wire Yourself](#what-you-must-wire-yourself) before you plan the work.** This plugin is a client. Several flows need a server you host, and one of them — real-money purchases — cannot work at all until you do. That section is the honest list of what is not in the box.

---

## Contents

- [Environments](#environments)
- [Installation](#installation)
- [Configuration](#configuration)
- [APIManager reference](#apimanager-reference)
- [Error handling](#error-handling)
- [Not every 2xx is a success](#not-every-2xx-is-a-success)
- [Phone numbers](#phone-numbers)
- [Money and locales](#money-and-locales)
- [Idempotency](#idempotency)
- [UI components](#ui-components)
- [Upgrading from 3.0](#upgrading-from-30)
- [Phone approval (QR)](#phone-approval-qr)
- [Editor tools](#editor-tools)
- [What You Must Wire Yourself](#what-you-must-wire-yourself)
- [Server-side proxy](#server-side-proxy)
- [Troubleshooting](#troubleshooting)

---

## Environments

| | Production | Sandbox |
|---|---|---|
| Host | `https://invo.network` | `https://sandbox.invo.network` |
| API routes | `/api/*` | `/sandbox/api/*` |
| Console | `console.invo.network` | `dev.console.invo.network` |
| Payments | Real money | Test cards only |

Sandbox and production are **fully isolated databases with independent SDK keys**. A sandbox key does not work against production, and vice versa.

Each environment has its own Game ID and its own keys. Pick the environment in **InvoSDK > Settings**
("Play Mode and builds use"), which sets `useProduction` on the config asset.

> **Two credentials, never mixed.**
>
> - The **game secret** (`X-Game-Secret-Key`) authenticates your *game*. It is used for initiate, purchase, catalog, balance and claims, and for minting player tokens. It belongs on your server. In production the SDK sends these calls to `gameServerUrl`; only a sandbox build may use a sandbox key from the config.
> - A **player token** (`Authorization: Bearer`, 15 minutes, one player) authenticates the *player*. It is used for the phone approval, the approve / confirm-receipt calls that move the money, and the pending list. It is safe on the device. The SDK mints and re-mints it for you.
>
> There is no player login against Invo itself. The old `/auth/login` + CSRF handshake was removed in 2.0.0.

---

## Installation

**Requirements:** Unity 2021.3 LTS or newer (tested on 2021.3.45f2, 2022.3.62f3 and 6000.0.58f2).
Dependencies `com.unity.nuget.newtonsoft-json` and `com.unity.ugui` are installed with the package.
On 2021.3 and 2022.3 TextMeshPro is the separate `com.unity.textmeshpro` package; the SDK offers to
add it when it is missing.

1. **Window > Package Manager > + > Add package from git URL**:
   `https://github.com/Invo-Technologies/Invo-unity-sdk.git?path=/Packages/com.invonetwork.unity-sdk#v3.1.1`
   (or import `InvoSDK-3.1.1.unitypackage` from the release).
2. **InvoSDK > Settings** (see [Configuration](#configuration)).
3. Drag `InvoAPI.prefab` and `InvoSDKWindow.prefab` (package `Runtime/Prefabs`) into your first scene,
   with an EventSystem whose input module matches your project's input handling.

The config asset is created at `Assets/InvoSDK/Resources/InvoSDKConfig.asset`. If you find a second one
at `Assets/Resources/InvoSDKConfig.asset`, delete it: two identically-named assets in two `Resources`
roots make `Resources.Load` resolution undefined, and you can end up building against the wrong key.
The Settings window detects this and offers to select the stray asset.

`InvoSDKConfig.asset` is **gitignored**. Each developer configures their own; it will not arrive with a clone.

---

## Configuration

**InvoSDK > Settings** has three tabs.

| Tab | Fields |
|---|---|
| **Sandbox** | Sandbox Game ID; one sandbox key (`ivsdk_…`) per storefront: Steam, Google Play, App Store, PlayStation, Xbox, Nintendo Switch, and Epic Games Store, Web or Platform if you add them; which storefront each build target uses; the test player (email, phone, name); the SANDBOX watermark for development builds. Test connection checks the Game ID and key. |
| **Production** | Production Game ID, `gameServerUrl` (required, https) and `checkoutSessionEndpoint`. **No key fields, by design.** Your server holds one production key per storefront. |
| **Game & display** | `gameName`, `gameIconUrl`, `gameCurrencyName`, `gameCurrencyUrl`, `gameVersion`. |

- The SDK picks the key for the storefront the build runs on (`InvoSDKConfig.ActiveSandboxKey`),
  and sends that storefront to your game server as `X-Invo-Storefront` (`steam`, `google_play`,
  `app_store`, `playstation`, `xbox`, `nintendo_switch`, `epic_games_store`, `web`, `platform`).
  A scripting define such as `INVO_STOREFRONT_EPICGAMESSTORE` overrides it for one build.
- **Production build check.** A production build fails without a production Game ID or an https
  Game Server URL. Sandbox keys and the test player are stripped from it and restored afterwards.
- **3.0 fields.** `sdkKey` and `gameId` still work when the new fields are empty; the Settings window
  moves them into the Sandbox tab with one button.
- `gameId` also names the mobile return scheme `invo-sdk-<gameId>://done` (the active environment's ID).
- `playerEmail`, `playerName`, `playerPhone` are the test player. In a real game call
  `APIManager.Instance.SetActivePlayer(email, name, phone)` from your login.

> ### Sandbox keys go into Development builds only
>
> Unity bundles everything under a `Resources/` folder into the player. Anyone can recover a key from
> `resources.assets` in about a minute. An extracted key is a **full tenant credential**: it can charge
> cards, move any player's balance, request a payout, mint player tokens and reconfigure webhooks.
> Sandbox and production keys look identical. That is why only the Sandbox tab takes keys, why the
> build guard strips them from every production and release build, and why release players ignore
> them. Don't distribute Development builds.

**If a key leaks, rotate it with `immediate: true`.** The default rotation keeps the old key working for a 7-day grace window, and that grace is not honored uniformly across endpoints — some accept the previous key during the window while others reject it at once. For a leaked key, take the hard cutover.

---

## APIManager reference

`APIManager.Instance` is a `MonoBehaviour` singleton with `DontDestroyOnLoad`. All methods are `async Task<T>` and take an optional `onSuccess` and an `onError` of type `Action<InvoApiException>`.

### Balance and catalog

```csharp
Task<PlayerBalanceResponse> GetPlayerBalanceAsync(string email, ...)
Task<GameItemListResponse>  GetGameItemsAsync(...)
void StartBalancePolling();
void StopBalancePolling();
```

Balance polling is single-flight with exponential backoff on failure. It displays `balances[0].available_balance` — the **spendable** figure. `total_balance` includes reserved funds and showing it drives players into insufficient-balance lockouts.

`GetGameItemsAsync` returns **all active items from all games**, not only yours. Filter on `game_id` if you want just your own.

### Item purchase

```csharp
Task<PurchaseItemResponse> PurchaseItemAsync(
    string clientRequestId, string playerEmail, string playerName,
    string itemId, string itemName, int quantity,
    decimal unitPrice, decimal totalPrice,
    string itemCategory = null, string itemDescription = null, ...)
```

Read the post-purchase balance from top-level `new_balance` (canonical). `balance_info.new_balance` is legacy.

### Transfers — a player moving their own balance between games

```csharp
Task<AvailableDestinationsResponse> GetTransferDestinationsAsync(...)
Task<InitiateTransferResponse>      InitiateTransferAsync(clientRequestId, sourceName, sourceEmail, sourcePhone, targetPhone, targetEmail, targetGameId, amount, ...)
Task<TransactionStatusResponse>     GetTransferStatusAsync(transactionId, ...)
Task<ClaimTransferResponse>         ClaimTransferAsync(claimCode, targetPlayerName, targetPlayerEmail, targetPlayerPhone, targetCurrencyId, ...)
```

### Sends — one player to another, by phone

```csharp
Task<AvailableDestinationsResponse> GetSendDestinationsAsync(...)
Task<InitiateSendResponse>          InitiateSendAsync(clientRequestId, senderName, senderEmail, senderPhone, receiverPhone, receiverEmail, receivingGameId, amount, ...)
Task<TransactionStatusResponse>     GetSendStatusAsync(transactionId, ...)
Task<ClaimCurrencyResponse>         ClaimCurrencyAsync(claimCode, receiverName, receiverEmail, receiverPhone, int? receiverPlayerId = null, ...)
```

> **After initiate, approve on the phone.** `InitiateSendAsync` / `InitiateTransferAsync` only reserve the funds. Nothing moves until `InvoDeviceApproval.RunAsync(transactionId, InvoApprovalFlow.Send | Transfer)` completes. See [Phone approval (QR)](#phone-approval-qr). Transfers and sends settle on separate endpoints; the flow you pass picks the right one.
>
> `VerifySendSmsAsync`, `VerifyTransferSmsAsync`, `ResendSendPinAsync` and `ResendTransferPinAsync` are `[Obsolete]`. Invo has retired the SMS path. It does not text the PIN on SDK tenants, `resend-pin` sends nothing for those transactions, and high-value SMS verifies are refused (`403 HIGH_VALUE_REQUIRES_APP`).

### Phone approval and player session

```csharp
Task<InvoDeviceApprovalResult>     InvoDeviceApproval.RunAsync(transactionId, flow, view = null, cancellationToken = default, channel = null)
Task<DeviceApprovalGrant>          BeginDeviceApprovalAsync(transactionId, flow, channel)
Task<DeviceApprovalPollResponse>   PollDeviceApprovalAsync(deviceCode)
Task<ConfirmEnrollmentResponse>    ConfirmDeviceEnrollmentAsync(deviceCode, approve)
Task<DeviceApprovalSettleResponse> SettleDeviceApprovalAsync(transactionId, flow, deviceCode)   // the money step
Task<PendingActionsResponse>       GetPendingActionsAsync(...)
Task<string>                       GetPlayerTokenAsync()
void                               SetActivePlayer(email, name, phone = null)
static Func<Task<PlayerTokenResponse>> PlayerTokenProvider            // optional: mint via your login API
static Action<UnityWebRequest>         GameServerRequestDecorator     // attach YOUR session to game-server calls
```

**Peer-to-peer sends:** the destinations endpoint omits your own game, but `initiate-send` permits same-game sends. Pass `receivingGameId: APIManager.Instance.GameId` directly.

---

## Error handling

Non-2xx responses throw `InvoApiException`:

```csharp
try {
    await api.PurchaseItemAsync(...);
} catch (InvoApiException ex) {
    if (ex.IsDuplicate)      { /* 409 — the purchase ALREADY SUCCEEDED. Treat as success. */ }
    else if (ex.IsRateLimited) { /* 429 — back off for ex.RetryAfterSeconds */ }
    else if (ex.IsNetworkError) { /* never reached the server */ }
    Debug.LogError(ex);      // ToString() is log-safe; it excludes the response body
}
```

`ex.ErrorCode`, `ex.ErrorId`, `ex.StatusCode`, `ex.RetryAfterSeconds`, `ex.Body`.

**Match on `ErrorCode`, never on message text.** Quote `ErrorId` to Invo support — it maps to a server log entry.

**Never render `ex.Body` to a player.** A `409 PHONE_SHARE_APPROVAL_REQUIRED` body contains another user's phone number and a masked email.

**A 409 on item purchase means the purchase went through.** Replaying a `client_request_id` returns 409 with the original transaction. Showing "Purchase Failed" there produces support tickets and chargebacks for a completed sale.

---

## Not every 2xx is a success

The API returns **HTTP 200 and 202 for outcomes that are not completion**. Branch on the `status` string, not the status code:

| `status` | HTTP | Meaning |
|---|---|---|
| `success` | 200/201 | Completed. |
| `needs_account_selection` | **200** | Several accounts share the receiver's phone. **Nothing was credited** — the session is rolled back. Show a picker from `candidates` and re-call with `receiverPlayerId`. |
| `pending_guardian_approval` | **202** | The player is a minor. Held for a guardian (email first, SMS only as a fallback). The sender approves on their phone once it clears. |
| `pending_confirmation` | **202** | Recipient identity step-up in progress. |
| `held_for_review` / `step_up_required` | **202** | From a settle call: approved, but held by Invo's risk checks. Nothing moved, nothing refused. |
| `approved` / `completed` | 200 | From a settle call: the sender's step landed / the receiver was credited. |

Use the `InvoStatus` constants. You cannot detect minor status from your side, so handle 202 defensively on **every** initiate call.

Also watch for **`403 GUARDIAN_REQUIRED`** — a minor with no usable guardian on file. That one is terminal, not a waiting state.

### `verification_method`

`InitiateTransferResponse` / `InitiateSendResponse` carry `verification_method`, either `"in_app"` or `"sms"`. **Either way, approve with the phone approval.** `"in_app"` means Invo texted nothing. `"sms"` only means Invo also texted a legacy PIN, which happens to a brand-new tenant before its first approval. Do not build a PIN screen for it. There is no `"qr"` value; QR is the *channel* of the approval, not a verification method.

---

## Phone numbers

Use `InvoPhone.Normalize(raw)`. It returns strict E.164 (`+` then 10–15 digits) or **null** when the input cannot be valid. Show `InvoPhone.DescribeProblem(raw)` and do not send.

Phones are **still required** at initiate, for both sender and receiver. They address the send and identify both players. They are **not** used to verify anything any more; no code is texted to them.

The API **will not infer a country code**, and neither does this SDK. Two rules matter:

- **Never truncate.** A previous version capped numbers at 12 digits, which turned a legitimate 13–15 digit international number into a *different, valid* number. The API accepts it, the claim code binds to a phone that does not exist, and you get a 2xx with no error anywhere.
- **The same normalized value must be used at initiate and at claim.** The backend compares digits exactly and returns `403` on mismatch.

---

## Money and locales

Use `InvoFormat.Amount(value)` for anything going into a request body or URL, and `InvoFormat.TryParseAmount(text, out value)` for player input.

`ToString("F2")` uses the device locale. On a German, French, Spanish, Portuguese, Italian, Russian or Turkish device that emits `"9,99"`, which the backend's decimal parser rejects with `400 "Invalid price format"` — and increments that player's failed-attempt counter toward a lockout. Prefer `decimal` over `float` for money.

---

## Idempotency

Every write takes a caller-supplied `clientRequestId`. Mint it **once per user intent** with `APIManager.NewClientRequestId()`, store it, and reuse it across retries of that same action:

```csharp
_clientRequestId ??= APIManager.NewClientRequestId();   // once, when the user confirms
await api.InitiateSendAsync(_clientRequestId, ...);     // reuse on every retry
```

Minting a fresh id per attempt defeats server-side deduplication — a retry after a timeout becomes a genuinely new transaction with funds reserved twice.

Do not use a `client_request_id` beginning with `sub_`; that prefix is reserved for subscription renewals and is rejected with `400 CLIENT_REQUEST_ID_RESERVED`.

---

## UI components

The window (`InvoSDKWindow.prefab`) has tabs for **Store**, **Send**, **Transfer** and **Collect**,
a phone button and a close button. Open it from your own UI with
`InvoSDKWindowController.Instance.Open("Store" | "Sender" | "Transfer" | "Collect" | "Phone")`.

| Panel | Components |
|---|---|
| Store: Top up | `InvoSDKFeaturedPanel` with `FeaturedItemCard`, `FeatureWeeklyItemCard`, `FeatureDailyItemCard` (real-money packs through hosted checkout) |
| Store: Items | `ItemPurchasePanel`, `ItemPurchaseConfirmPanel`, `ItemCardView` (spend the balance) |
| Send | `SenderCurrencyPanel` with `InvoRecentRecipients`, `InvoGamePicker`, `InvoDeviceApprovalPanelView` |
| Transfer | `TransferCurrencyPanel` with `InvoGamePicker`, `InvoDeviceApprovalPanelView` |
| Collect | `InvoCollectPanel`, `InvoPendingItemView` |
| Phone | `InvoPhoneSettingsPanel` |
| Shared | `InvoEnrollmentPromptPanel` (match code), `InvoApprovalStep`, `InvoBalanceLabel`, `InvoSandboxWatermark`, `InvoGamepadFocus`, `InvoSafeArea`, `InvoTabBar`, `InvoSDKWindowManager` |

**Theme.** Every graphic has `InvoThemed` (a role such as Surface, Accent or TextSecondary, a shape and
a text style); `InvoThemeProvider` on the window root picks the `InvoTheme` asset. Duplicate
`InvoDefaultTheme`, change it and assign it: every panel restyles live, without prefab edits.

**Send.** The sender is the active player: their phone is shown masked with a Change link instead of
an input. Recent recipients come from `InvoRecentRecipientsStore` (this device only, the last five per
player, keyed by a hash of the email), because INVO's API does not return recipients' numbers.
The game picker lists games the player already plays first (`GetPlayerDestinationsAsync`), then every
destination, filtered as they type; unavailable games stay listed, greyed, with the reason.

**Collect.** Lists `receiving_confirm` items from `GetPendingActionsAsync`, each with Collect (the
receiver's phone approval). Held items are greyed with the reason. There is no claim-code form:
INVO is retiring claim codes, and a receiver with no account in this game collects through the link
INVO emails them. `ClaimCurrencyAsync` / `ClaimTransferAsync` remain for existing integrations.
Claim codes are bearer credentials, so they are never logged.

**Phone.** `ChangePlayerPhoneAsync` switches an unsecured account at once. A secured account answers
`409 PHONE_CHANGE_REQUIRES_VERIFICATION`; the panel then calls `CreatePhoneChangeRequestAsync`, INVO
emails the player a link to confirm on INVO's page (no text message), and the panel polls
`GetPhoneChangeRequestAsync` and calls `ApplyConfirmedPhone`. Save the new number in your own account
system too (`APIManager.PlayerPhoneChanged`).

**Platforms.** Desktop and consoles show a QR; phones open INVO's page in the system browser.
`InvoGamepadFocus` gives every screen d-pad and keyboard navigation; `InvoSafeArea` keeps the sheet
clear of notches and overscan; phone fields open the phone pad.

**Fees.** Panels show a local estimate before initiate, then replace it with `fees_preview` from the response. Always display the returned figures — per-tenant rate overrides mean a client-side calculation can be wrong.

**Images.** Catalog artwork must be served over **https**, under 4 MB and 4096 px, or it is skipped with a warning.

---

## Upgrading from 3.0

1. Keep `Assets/InvoSDK/Resources`. Delete the rest of the copied SDK (`Assets/APIManager.cs`,
   `Assets/InvoSDK/Scripts`, `Tests`, `Prefabs`, `Inter`, `NewArt`, `Sprites`, and
   `Assets/Plugins/iOS/InvoHostedApproval.mm`), then install the package. Every file kept its GUID.
2. **InvoSDK > Settings > Move the 3.0 key and Game ID into Sandbox**, then fill in the Production tab.
3. Replace your SDK UI with `InvoSDKWindow.prefab`, or keep your own: the 3.0 panel components and
   their fields are unchanged, and the 3.1 additions (`gamePicker`, `recentRecipients`,
   `senderPhoneText`, `ownPhoneText`, ...) are optional.
4. A custom 3.0 layout still needs the QR view wired (`approvalView`, `retryApprovalButton`);
   `InvoDeviceApprovalPanelView` can now be saved in scenes and prefabs (3.0 could not).

---

## Phone approval (QR)

Sends and transfers are approved on the player's phone. Nothing is texted, and there is no code for the player to type. This is INVO's device approval grant (OAuth 2.0 Device Authorization Grant, RFC 8628), bound to **one transaction**.

| Build | What the player sees | Channel |
|---|---|---|
| Windows / macOS / Linux, Steam, consoles | A QR on the game screen. They scan it with their phone and approve with a passkey. | `qr` |
| iOS / Android | The INVO page opens in the system browser on the same phone. | `app_browser` |

`InvoDeviceApproval.RunAsync` picks the channel from the platform. Pass `channel` to override it.

```csharp
// After InitiateSendAsync / InitiateTransferAsync returned status "success":
var result = await InvoDeviceApproval.RunAsync(
    transactionId: resp.transaction_id,
    flow: InvoApprovalFlow.Send,          // Transfer, Send, SendReceipt, TransferReceipt
    view: myApprovalView);                // null = built-in overlay

switch (result.Outcome)
{
    case InvoDeviceApprovalOutcome.Settled:
    case InvoDeviceApprovalOutcome.AlreadySettled: /* done: show "Sent" */ break;
    case InvoDeviceApprovalOutcome.Held:           /* guardian / review: poll the status */ break;
    case InvoDeviceApprovalOutcome.Denied:
    case InvoDeviceApprovalOutcome.Expired:        /* offer "Show code again" (same transaction) */ break;
    case InvoDeviceApprovalOutcome.NotPending:     /* read the status before saying anything */ break;
    case InvoDeviceApprovalOutcome.Cancelled:      break;
}
```

What `RunAsync` does, in order:

1. **Begin.** It calls `POST /api/sdk/approvals/device/begin {transaction_id, flow, channel}` and gets `device_code`, `user_code`, `verification_uri(_complete)`, `interval` and `expires_in` (10 minutes).
2. **Show.** It draws `verification_uri_complete` as a QR with `user_code` printed underneath, or opens it in the system browser.
3. **Poll.** It calls `POST /api/sdk/approvals/device/poll` every `interval` seconds. On `slow_down` it adds 5 s. If the player taps back from the browser, it polls straight away. `authorization_pending`, `slow_down`, `access_denied` and `expired_token` arrive as HTTP 400s; they are outcomes, not errors.
4. **Match code.** While the player's phone has no INVO passkey yet, the poll carries an `enrollment` block. The game screen shows the match code, and the player's Yes / No is sent to `confirm-enrollment`. **The game screen is the proof that phone and console belong to the same person**, so no message is sent.
5. **Settle.** This is the step that moves the money. An approved poll **moves nothing**. `RunAsync` then calls the endpoint for the flow, with `{device_code}`:

   | Flow | Endpoint |
   |---|---|
   | `transfer` | `POST /api/sdk/transfers/{id}/approve` |
   | `send` | `POST /api/sdk/send/{id}/approve` |
   | `send_receipt` | `POST /api/sdk/send/{id}/confirm-receipt` |
   | `transfer_receipt` | `POST /api/sdk/transfers/{id}/confirm-receipt` |

Every call uses the **player token**; the game secret is never involved. The individual stages are on `APIManager` (`BeginDeviceApprovalAsync`, `PollDeviceApprovalAsync`, `ConfirmDeviceEnrollmentAsync`, `SettleDeviceApprovalAsync`) for games that want their own loop.

**Rules that matter:**

- **A settle failure is ambiguous about the money.** The backend may commit and then fail building its reply, and a timeout tells you nothing. Read the transaction status (`GetSendStatusAsync` / `GetTransferStatusAsync`) before telling the player it failed, and **never** start a replacement transaction automatically. `InvoApprovalStep` in the bundled panels does exactly this.
- **`not_pending` is a normal answer.** If the settle finds the transaction already past this step, `RunAsync` returns `AlreadySettled`. That only happens when the status proves the step happened; any status the SDK does not know counts as not settled.
- **Retry with the same transaction.** After a decline or an expired code, run `RunAsync` again for the same `transaction_id`. A new `begin` replaces the old pending code. Never initiate a second send for the same intent.
- **`409 DEVICE_APPROVAL_ALREADY_PENDING`** means a phone already approved and the grant is waiting to be settled from the device that holds its code. Wait for it to expire (10 minutes) before beginning again.
- **Holds.** A `202` from the settle (`GUARDIAN_APPROVAL_PENDING`, `RISK_HOLD`, `STEP_UP_REQUIRED`, `RECIPIENT_IDENTITY_PENDING`) means nothing moved yet and nothing was refused. Poll the status until it reads `approved` / `completed`.
- **The player must exist in this game** before a token can be minted. Initiating a send or transfer creates the sender. A receiver with no account in the receiving game gets `409 receiver_not_enrolled_use_claim_code` and collects with the claim code instead.

### Receivers: collect

A send addressed to the active player appears in `GetPendingActionsAsync()` as `kind: "receiving_confirm"`. Collect it with `RunAsync(item.transfer_id, InvoApprovalFlow.SendReceipt)`. That is the same QR flow, run in the **receiving** game with the receiver's own token. The claim panel's **Collect** button does this. The claim-code form (`ClaimCurrencyAsync` / `ClaimTransferAsync`) is now the fallback.

### Player and token

```csharp
APIManager.Instance.SetActivePlayer(email, name, phone);   // on login / account switch
string token = await APIManager.Instance.GetPlayerTokenAsync();
```

- Tokens last 15 minutes and cannot be refreshed. The SDK caches one, re-mints it a minute before expiry, and on a `401 SDK_TOKEN_*` re-mints once and retries. The player is never prompted.
- `SetActivePlayer` drops the previous player's token, so nothing signed for one identity is presented for another.
- By default the token is minted through `POST /api/sdk/player-token` on your game server (or with the sandbox key). To use your own login API instead, set `APIManager.PlayerTokenProvider` to a function that returns a `PlayerTokenResponse`.

---
## Editor tools

`InvoSDK →` **Settings** (also **Setup Wizard**), **Item Catalog**, **Test Purchase**, **Player Balance**.

These are Editor-only and never ship in a player build. They use the sandbox key for the desktop storefront.

Test Purchase handles `200` with `status: "requires_action"` correctly: that means 3DS is required and **no money has been charged**.

---

## What You Must Wire Yourself

This plugin cannot be a complete integration on its own, and the gaps are not oversights. They follow from one rule:

> **The SDK secret key is a server-side credential.** A Unity player build cannot hold it safely.

### 1. A backend of your own (required for production)

**Status: not provided.**

In **sandbox**, the plugin may send `X-Game-Secret-Key` from the game client with the `sdkKey` in the config. That is how the demo is set up, but the key ships in your build and is extractable. In **production** the plugin refuses to: set `gameServerUrl` and implement the [server-side proxy](#server-side-proxy).

The supported model:

```
Unity client ──(your session auth)──▶ Your server ──(X-Game-Secret-Key)──▶ Invo API
```

Your server needs two jobs:

**a. Mint player tokens.** `POST /api/sdk/player-token` with your secret and `{"player_email": "..."}` returns a 15-minute token scoped to one player, plus an opaque `identity_id`. There is no refresh call; on a 401, mint a fresh one. The phone approval runs entirely on that token. Derive the email from **your** session, never from the request body.

**b. Proxy the writes.** Item purchases, transfers, sends and claims should originate server-side, where you can validate the player's session first.

**Why this matters most — item pricing.** `POST /api/item-purchases/purchase-item` validates only that `total_price == unit_price × quantity`, that both are positive, and that they are under 999999.99. It does **not** look up the item's real price — `item_id` is an opaque string. That is sound for a trusted server-side caller, which is what the endpoint documents itself as. Called from a game client, it means a modified build sets its own prices.

**If you ship real-money value, put a server between your client and this endpoint.**

### 2. A hosted-checkout endpoint (required for real-money purchases)

**Status: the plugin calls an endpoint you configure. You must host it.**

Set `checkoutSessionEndpoint` to an **https** URL on your server — once in **InvoSDK → Settings › Production**, which writes it to `InvoSDKConfig`. An individual `InvoSDKFeaturedPanel` can override it with its own Inspector field; blank means "use the config value". The plugin POSTs `{player_email, usd_amount}` and expects `{"checkout_url": "..."}`.

Your endpoint calls:

```
POST https://invo.network/api/checkout/sessions
Headers: X-Game-Secret-Key: ivsdk_…
Body:    { "player_email", "usd_amount", "rail": "platform",
           "success_url", "cancel_url", "metadata": { … } }

→ 201 { "session_id", "checkout_url", "expires_at", "expires_in_seconds": 900 }
```

Constraints: `usd_amount` must be `> 0` and `<= 999.99`; `metadata` under 8 KB; the returned JWT is single-use with a **15-minute TTL**.

- The path is `/api/checkout/sessions`. Some older docs say `/api/checkout-sessions` — that does not exist.
- The legacy `console.invo.network/stripe-checkout/?game_secret=…` flow is **dead** and has been removed. It redirects, the checkout page reads only `?session=`, and it put your secret in a browser URL.
- `rail: "platform"` gives cards plus Apple Pay, Google Pay and Link with no app-store commission — but **that is a fee statement, not a policy clearance.** Apple and Google generally require in-app purchase for digital goods; review a WebView checkout with counsel before shipping to either store.
- **Confirm the credit from the server-to-server webhook**, not the browser. `INVO_CHECKOUT_COMPLETE` is posted to `'*'` with no origin validation, so any page that can frame the checkout could forge it. Treat it as a UX hint only.

**WebView limitations.** The bundled `WebViewObject` (GREE unity-webview):

- is **unsupported on Windows, Linux and the Windows Editor** — you cannot test this flow in a Windows Editor session;
- implements `Unity.call(msg)` via `window.location = 'unity:' + msg`, which is **not** DOM `postMessage`, so `INVO_CHECKOUT_COMPLETE` never reaches Unity;
- delivers to `CallFromJS(string)` with **no origin**, so origin validation is impossible on the Unity side.

For in-app completion signals you need native bridges — an iOS `WKScriptMessage` handler and an Android `addJavascriptInterface`. Neither ships here.

### 3. Phone approval (implemented)

**Status: implemented.** QR on desktop, Steam and consoles; system browser on iOS and Android. See [Phone approval (QR)](#phone-approval-qr).

What remains on your side:

- **Wire the UI.** The 3.1 prefabs come wired; for a custom 3.0 layout see [Upgrading from 3.0](#upgrading-from-30).
- **Enable your tenant.** The `/api/sdk/*` routes answer `403 TENANT_NOT_MIGRATED` until Invo enables SDK verification for your game, and `503 DEVICE_APPROVAL_NOT_CONFIGURED` / `sdk_verification_disabled` while it is switched off. Coordinate the sandbox switch with Invo first.
- **Register the mobile return scheme.** `invo-sdk-<gameId>://done` is registered at build time by `InvoHostedApprovalBuildPostprocessor` (iOS `Info.plist`, Android manifest). See README.md → [Hosted approval on mobile](README.md#hosted-approval-on-mobile-system-browser).
- **Not implemented:** the in-app device-signature path (`device/register` + a hardware-keystore `device_signal`) and in-engine WebAuthn. The QR and system-browser grant replaces both for engine builds; Invo's own guidance is to use the grant unless you hold a verified passkey domain.

### 4. Cross-game claims

**Status: partially implemented.**

Both claim endpoints authenticate with the **receiving** game's secret key and scope the lookup to `to_game_id`. This plugin holds one key.

- **The phone-approval collect works cross-game.** The receiving game's own build mints the receiver's token and runs `send_receipt` / `transfer_receipt`; no shared key is involved.
- **Claim-code fallback, same-game:** works. Your key is both sending and receiving.
- **Claim-code fallback, cross-game:** returns 404 with "not intended for this game", because the transaction belongs to a different `to_game_id`. It must run in the *receiving* game, with *that* game's key.

`claim-transfer` requires five fields: `claim_code`, `target_player_name`, `target_player_email`, `target_player_phone`, `target_currency_id`.

### 5. Phone-share approval (409 recovery not implemented)

When a send or transfer names a phone already registered to a different email, the API returns **409 `PHONE_SHARE_APPROVAL_REQUIRED`** and auto-texts an OTP to the existing owner. The panels surface the error; recovery is not implemented.

Call `POST /api/wallet/phone-share/approve` with the `approval_id` from the 409 body and the 6-digit OTP, then retry the original call **once**. If `already_approved: true`, skip the OTP prompt and retry immediately. Watch `cooldown_seconds` (no new SMS sent — the previous code is still valid) and `rate_limited` with `retry_after_seconds`.

The owner can also approve by SMS reply, which gives you no callback — poll `GET /api/wallet/phone-share/status?phone=…&email=…` every 3–5s while your panel is open.

### 6. Guardian approval (polling is approximate)

On a 202, the panels show a waiting state and poll the **transaction status** endpoint. The documented approach is to poll `guardian_approval.poll_endpoint` (`/api/transactions/{id}/approval-status`) every 5–10s, which gives you the richer state machine including `410` for rejected or expired. Implement that if you need to distinguish rejection from expiry.

### 7. Not covered at all

| Surface | Notes |
|---|---|
| Steam purchases | `steam/init-purchase` / `steam/finalize-purchase`, plus a Steamworks callback |
| Subscriptions | ~10 endpoints — create, manage, renewals, webhooks, reporting |
| Saved cards | `setup-intent`, `player-cards`. The SDK sets `save_card: true` but cannot list or reuse saved cards |
| Platform commerce | `/api/platform-commerce` |
| Webhooks | Server-side. Verify `X-Invo-Signature` (HMAC-SHA256 over `"{timestamp}.{raw_body}"`), reject outside a 5-minute window, dedupe on `X-Invo-Idempotency-Key` |
| Withdrawals | Developer revenue, server-side only |

### 8. Catalog and item purchase are separate paths

`GetGameItemsAsync()` returns the **server** catalog (`GameItem`, real-money packs, checkout flow). The item purchase UI reads a **local** `InvoSDKItemCatalog` ScriptableObject (`InvoSDKItem`, game currency, item-purchase API).

They do not interoperate, and no mapper is provided — the shapes are irreconcilable without inventing data. `GameItem` has no description, `item_id`/`itemId` and `price_usd`/`priceUSD` differ, and the two prices are in *different units*. A naive mapper would be a silent-mispricing hazard.

Note `InvoSDKItem.priceUSD` is misnamed — it holds a **game-currency** amount, and the UI labels it accordingly.

### 9. No quantity control

Purchases are fixed at quantity 1 though the API accepts 1–1000. To add a stepper, wire it to `PurchaseQuantity` in `ItemPurchaseConfirmPanel`; `total_price` is already derived from it in `decimal`.

---

## Server-side proxy

**Required for production.** The SDK refuses to send the game secret from a production build. Every call that needs it fails with `SDK_GAME_SERVER_REQUIRED` until `InvoSDKConfig.gameServerUrl` is set.

The contract is a **pass-through**. The plugin sends Invo's own path, method and JSON body to `<gameServerUrl>/api/<path>`. Your server:

1. authenticates **your** player session (attach it with `APIManager.GameServerRequestDecorator`);
2. checks the path against an allow-list;
3. adds `X-Game-Secret-Key` and forwards to `https://invo.network/api/<path>` (sandbox: `https://sandbox.invo.network/sandbox/api/<path>`);
4. returns Invo's status code and body **unchanged**. The SDK reads Invo's error envelope and status strings.

Allow-list exactly these paths:

| Method | Path | Your server must also… |
|---|---|---|
| POST | `/api/sdk/player-token` | **ignore `player_email` in the body** and use the email from your own session. Otherwise any client can mint a token for any player. |
| POST | `/api/currency-sends/initiate-send` | check that `sender_player_email` is the session's player. |
| POST | `/api/transfers/initiate-transfer` | check that `source_player_email` is the session's player. |
| POST | `/api/item-purchases/purchase-item` | **set `unit_price` / `total_price` from your own catalog.** Invo charges whatever price it is sent. |
| POST | `/api/v1/game-items/list` | add `game_secret` to the body; this route reads it from the body, not the header. |
| GET | `/api/player-balances/player/by-email/{email}` | check that the email is the session's player. |
| POST | `/api/currency-sends/available-destinations`, `/api/transfers/available-destinations` | — |
| GET | `/api/currency-sends/{id}/status`, `/api/transfers/{id}/status` | — |
| POST | `/api/currency-sends/claim-currency`, `/api/transfers/claim-transfer` | check that the receiver is the session's player. |

The phone approval and pending-list calls (`/api/sdk/approvals/device/*`, `/api/sdk/{transfers|send}/{id}/approve|confirm-receipt`, `/api/sdk/transfers/pending`) do **not** go through your server. They carry the player token straight to Invo, which is safe on the client.

Rules that matter:

1. **The `ivsdk_` key exists only on your server.** Never in a build, a URL, a log, or a repo.
2. **Authenticate the player yourself** before forwarding. Invo authenticates your *game*, not your player.
3. **Pass `client_request_id` through unchanged** so idempotency survives the extra hop.
4. **Treat webhooks as the source of truth** for anything you credit. Verify `X-Invo-Signature` and dedupe on `X-Invo-Idempotency-Key`; `device_approval.approved` and `transfer.claim_pending` tell your server about approvals as they land.

Hosted checkout is separate: it keeps its own `checkoutSessionEndpoint` (§2).

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| `Config not found! Please run Setup Wizard.` | No `InvoSDKConfig.asset` under a `Resources` folder. It is gitignored: open **InvoSDK > Settings** to create yours. |
| `400 "Invalid price format"` | A locale emitted a comma decimal. Use `InvoFormat.Amount(...)`. |
| `400 "Phone number must start with country code"` | Missing `+`. Use `InvoPhone.Normalize(...)`. |
| `SDK_GAME_SERVER_REQUIRED` | Production build with no `gameServerUrl`. See [Server-side proxy](#server-side-proxy). |
| `403 TENANT_NOT_MIGRATED` / `503 sdk_verification_disabled` | SDK verification is not enabled for your game yet. Ask Invo to enable it (sandbox first). |
| `404 player_not_found` minting a token | The active player has never transacted in this game. Initiate creates the sender; a receiver with no account uses the claim code. |
| QR shows but scanning does nothing | The QR was cropped or tinted. Keep the RawImage square with the texture's white border intact. |
| Mobile page never returns to the game | The `invo-sdk-<gameId>` scheme is not registered. Check the build postprocessor ran and `gameId` is numeric. |
| Claim returns `403` | The claim phone does not match the initiate phone digit-for-digit. |
| Claim returns `404` cross-game | Cross-game claims need the receiving game's key. See §4. |
| Purchase shows "failed" but the balance dropped | A `409` was rendered as an error. `ex.IsDuplicate` means it **succeeded**. |
| `401` on every call | Missing or wrong `sdkKey`, or a sandbox key against production. |
| `403 "Game is not active"` | `game_status` is not `live` or `testing`. Check the console. |
| Repeated `429` | An anti-abuse lockout. Respect `ex.RetryAfterSeconds` and stop re-enabling the button. |
| Real-money purchase does nothing | `checkoutSessionEndpoint` is not set. See §2. |
| Catalog image missing | Not https, over 4 MB, or over 4096 px. |

Debug logging is on by default via `Debug.Log`. **Unity writes `Player.log` to disk in release builds** and Android logs to logcat, so never log claim codes, device codes, player tokens, or raw response bodies. `InvoApiException.ToString()` is log-safe.

---

For API reference: <https://docs.invo.network>
