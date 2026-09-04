# InvoSDK Unity Plugin

Unity 6 plugin for the Invo platform: player balances, item purchases, cross-game transfers, and player-to-player currency sends.

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

Toggle with `useProduction` on the config asset.

> **There is no login flow.** Every API call carries one header, `X-Game-Secret-Key`. Earlier versions of this plugin performed a `/auth/login` + `/auth/csrf-token` handshake and attached `Authorization: Bearer` and `X-CSRF-Token` to each request. Those headers were never read by any game API endpoint — the routes authenticate solely on the secret key — and `/auth/login` authenticates against the *developer console* account table, not players. That handshake has been removed, along with the `playerPassword` config field it required.

---

## Installation

**Requirements:** Unity 6 (6000.0+), .NET Standard 2.1, `com.unity.nuget.newtonsoft-json`, TextMeshPro.

1. Copy `Assets/InvoSDK/` and `Assets/APIManager.cs` into your project's `Assets` folder.
2. Install Newtonsoft.Json via **Window → Package Manager**.
3. Open **InvoSDK → Setup Wizard** and fill in your credentials.

The config asset is created at `Assets/InvoSDK/Resources/InvoSDKConfig.asset`. If you find a second one at `Assets/Resources/InvoSDKConfig.asset`, delete it — two identically-named assets in two `Resources` roots make `Resources.Load` resolution undefined, and you can end up building against the wrong key. The Setup Wizard detects this and offers to select the stray asset for you.

`InvoSDKConfig.asset` is **gitignored**. Each developer configures their own; it will not arrive with a clone.

---

## Configuration

| Field | Notes |
|---|---|
| `sdkKey` | Your game's `ivsdk_…` key. **Use a sandbox key.** See the warning below. |
| `gameId` | Your game's ID from the console. Not secret. |
| `checkoutSessionEndpoint` | https URL on **your** server that mints hosted-checkout sessions. Set once here; an individual `InvoSDKFeaturedPanel` can override it in the Inspector. See [§2](#2-a-hosted-checkout-endpoint-required-for-real-money-purchases). |
| `gameName`, `gameCurrencyName`, `gameIconUrl`, `gameCurrencyUrl`, `gameVersion` | Display values. |
| `playerEmail`, `playerName`, `playerPhone` | Demo/testing convenience. In a real game these come from your player session, not a build constant. |
| `useProduction` | Sandbox by default. |

> ### The key in this config ships in your build
>
> Unity bundles everything under a `Resources/` folder into the player. Anyone can recover the key from `resources.assets` with `strings` in about a minute.
>
> An extracted key is a **full tenant credential**. It can charge cards, move any player's balance, request a bank payout of your revenue, mint player tokens for any player in your game, and reconfigure your webhooks. There is no per-scope restriction and no IP allowlist. There is also **no environment marker on the key** — sandbox and production keys look identical, so a misplaced value is not visually obvious.
>
> Until you have a [server-side proxy](#server-side-proxy), treat any key you put here as public, and only ever put a **sandbox** key there.

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
Task<VerifySmsResponse>             VerifyTransferSmsAsync(transactionId, smsPin, ...)
Task<ResendPinResponse>             ResendTransferPinAsync(transactionId, ...)
Task<TransactionStatusResponse>     GetTransferStatusAsync(transactionId, ...)
Task<ClaimTransferResponse>         ClaimTransferAsync(claimCode, targetPlayerName, targetPlayerEmail, targetPlayerPhone, targetCurrencyId, ...)
```

### Sends — one player to another, by phone

```csharp
Task<AvailableDestinationsResponse> GetSendDestinationsAsync(...)
Task<InitiateSendResponse>          InitiateSendAsync(clientRequestId, senderName, senderEmail, senderPhone, receiverPhone, receiverEmail, receivingGameId, amount, ...)
Task<VerifySmsResponse>             VerifySendSmsAsync(transactionId, smsPin, ...)
Task<ResendPinResponse>             ResendSendPinAsync(transactionId, ...)
Task<TransactionStatusResponse>     GetSendStatusAsync(transactionId, ...)
Task<ClaimCurrencyResponse>         ClaimCurrencyAsync(claimCode, receiverName, receiverEmail, receiverPhone, int? receiverPlayerId = null, ...)
```

> **Transfers and sends have separate verify endpoints and they are not interchangeable.** The backend filters on transaction type inside its lookup, so submitting a transfer's id to the sends verifier returns a bare `404`. The method names are deliberately explicit — `VerifyTransferSmsAsync` and `VerifySendSmsAsync` — because the previous ambiguous `VerifySmsAsync` caused exactly that bug.

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
| `pending_guardian_approval` | **202** | The player is a minor. Held up to 15 minutes for a guardian's SMS reply. Do **not** advance to the PIN screen. |
| `pending_confirmation` | **202** | Recipient identity step-up in progress. |

Use the `InvoStatus` constants. You cannot detect minor status from your side, so handle 202 defensively on **every** initiate call.

Also watch for **`403 GUARDIAN_REQUIRED`** — a minor with no usable guardian on file. That one is terminal, not a waiting state.

### `verification_method`

`InitiateTransferResponse` / `InitiateSendResponse` carry `verification_method`, either `"sms"` or `"in_app"`.

When it is `"in_app"` the proactive SMS PIN is **suppressed** — showing a PIN screen leaves the player waiting for a text that was never sent. The bundled panels branch on this and fall back to polling the status endpoint. See [§3](#3-in-app-verification-and-passkeys-optional-not-implemented).

---

## Phone numbers

Use `InvoPhone.Normalize(raw)`. It returns strict E.164 (`+` then 10–15 digits) or **null** when the input cannot be valid. Show `InvoPhone.DescribeProblem(raw)` and do not send.

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

Under `Assets/InvoSDK/Scripts/UI/`. Treat them as working references, not a finished storefront.

- **Item purchase** — `ItemPurchasePanel`, `ItemPurchaseConfirmPanel`, `ItemCardView`
- **Transfers** — `TransferCurrencyPanel` (4-step wizard with SMS verification)
- **Sends** — `SenderCurrencyPanel`
- **Featured** — `InvoSDKFeaturedPanel`, `FeaturedItemCard`, `FeatureDailyItemCard`, `FeatureWeeklyItemCard`
- **Infrastructure** — `InvoSDKWindowManager`, `VerificationCodeInput`, `InvoUIButtonBinder`

**Scene wiring.** Several fields in the bundled `MainScene` are unassigned, including the transfer panel's phone inputs and status label. The panels now log a clear error naming the specific missing field rather than throwing a swallowed `NullReferenceException` — but you must assign them in the Inspector.

Optional serialized fields worth wiring: per-field error labels, `destinationsStatusText`, `feeEstimateNoticeText`, `resendPinButton`, `inAppApprovalGroup`, `guardianApprovalGroup`, `claimCodeText`, `claimCodeExpiryText`, `emptyStateText`, `balanceText`.

> **`claimCodeText` is effectively required.** Without it the recipient never sees the claim code, and the transfer cannot be completed. It is deliberately never logged — Unity's `Debug.Log` persists to `Player.log` and logcat, and claim codes are bearer credentials.

**Fees.** Panels show a local estimate before initiate, then replace it with `fees_preview` from the response. Always display the returned figures — per-tenant rate overrides mean a client-side calculation can be wrong.

**Images.** Catalog artwork must be served over **https**, under 4 MB and 4096 px, or it is skipped with a warning.

---

## Editor tools

`InvoSDK →` **Setup Wizard**, **Item Catalog**, **Test Purchase**, **Player Balance**.

These are Editor-only and never ship in a player build, so they use the SDK key directly — which is correct for a development tool.

Test Purchase handles `200` with `status: "requires_action"` correctly: that means 3DS is required and **no money has been charged**.

---

## What You Must Wire Yourself

This plugin cannot be a complete integration on its own, and the gaps are not oversights. They follow from one rule:

> **The SDK secret key is a server-side credential.** A Unity player build cannot hold it safely.

### 1. A backend of your own (required for production)

**Status: not provided.**

The plugin sends `X-Game-Secret-Key` from the game client. That works, and it is how the sandbox demo is set up — but the key ships in your build and is extractable.

The supported model:

```
Unity client ──(your session auth)──▶ Your server ──(X-Game-Secret-Key)──▶ Invo API
```

Your server needs two jobs:

**a. Mint player tokens.** `POST /api/sdk/player-token` with your secret and `{"player_email": "..."}` returns a 15-minute token scoped to one player, plus an opaque `identity_id`. There is no refresh call — on a 401, mint a fresh one. That token is what the `/api/sdk/*` routes read.

**b. Proxy the writes.** Item purchases, transfers, sends and claims should originate server-side, where you can validate the player's session first.

**Why this matters most — item pricing.** `POST /api/item-purchases/purchase-item` validates only that `total_price == unit_price × quantity`, that both are positive, and that they are under 999999.99. It does **not** look up the item's real price — `item_id` is an opaque string. That is sound for a trusted server-side caller, which is what the endpoint documents itself as. Called from a game client, it means a modified build sets its own prices.

**If you ship real-money value, put a server between your client and this endpoint.**

### 2. A hosted-checkout endpoint (required for real-money purchases)

**Status: the plugin calls an endpoint you configure. You must host it.**

Set `checkoutSessionEndpoint` to an **https** URL on your server — once in **InvoSDK → Setup Wizard**, which writes it to `InvoSDKConfig`. An individual `InvoSDKFeaturedPanel` can override it with its own Inspector field; blank means "use the config value". The plugin POSTs `{player_email, usd_amount}` and expects `{"checkout_url": "..."}`.

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

### 3. In-app verification and passkeys (optional, not implemented)

**Status: 23 backend routes exist under `/api/sdk/*`. None are implemented.**

Nothing breaks without it — SMS PIN remains the fallback. But no Unity title can be switched onto in-app approval as things stand.

**One path IS implemented: the hosted approval page on mobile.** `InvoHostedApproval` opens Invo's passkey page in the system browser and shows the match-code prompt; your server owns `begin` (with `channel: "app_browser"`), the poll, `confirm-enrollment` and the approve call, and the client never sees `device_code`. See README.md → [Hosted approval on mobile](README.md#hosted-approval-on-mobile-system-browser). The native device-signature path below is still not implemented.

Four things that will save you time:

- **The master flag defaults to off**, with a per-game switch on top. Handle `403 TENANT_NOT_MIGRATED` and `503 sdk_verification_disabled` as distinct, non-retryable states, and coordinate with Invo to enable your game.
- **There is no `.../approve/webauthn/complete` route.** WebAuthn approval finishes *inline* — post `webauthn_assertion` in the body of the `approve` call. Building a `/complete` step will 404.
- **Passkeys are not web-only.** The same relying-party ID backs native iOS and Android through associated domains, and `device/register` accepts `platform: ios | android | web | other`. Gating is per-tenant and per-player. Plan for three approval paths — device signature, WebAuthn assertion, SMS fallback — not a binary switch.
- **Signing contract.** Sign `"{action}|{transfer_id}|{nonce}|{timestamp}"` as UTF-8 bytes, `action` being `approve` or `confirm-receipt`. Single-use base64url nonce; epoch **seconds** within **±300s** of server time or you get `SIGNAL_STALE`. Algorithms: `EC_P256`, `ED25519`, `RSA_2048` — **Ed25519** fits iOS Secure Enclave and Android StrongBox best. Only the public key is ever transmitted.

### 4. Cross-game claims

**Status: partially implemented.**

Both claim endpoints authenticate with the **receiving** game's secret key and scope the lookup to `to_game_id`. This plugin holds one key.

- **Same-game (peer-to-peer) claims work** — your key is both sending and receiving.
- **Cross-game claims 404** with "not intended for this game", because the transaction belongs to a different `to_game_id`.

The claim must run in the *receiving* game, on the *receiving* player's device, with *that* game's key. If you operate both titles, each build claims with its own key.

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

The minimum viable version, in whatever stack you use:

```
POST /invo/checkout-session     → mints a hosted checkout session       (see §2)
POST /invo/player-token         → mints a 15-min player token           (see §1a)
POST /invo/purchase-item        → validates session, then proxies       (see §1b)
POST /invo/initiate-transfer    → validates session, then proxies
POST /invo/initiate-send        → validates session, then proxies
POST /invo/claim                → proxies with the RECEIVING game's key (see §4)
POST /invo/webhook              → receives Invo webhooks, verifies signature
```

Rules that matter:

1. **The `ivsdk_` key exists only here.** Never in a build, a URL, a log, or a repo.
2. **Authenticate the player yourself** before proxying. Invo authenticates your *game*, not your player.
3. **Set the price server-side.** Look the item up in your own catalog; do not trust a client-supplied `unit_price`.
4. **Pass the client's `client_request_id` through unchanged** so idempotency survives the extra hop.
5. **Treat the webhook as the source of truth** for real-money credit.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| `Config not found! Please run Setup Wizard.` | No `InvoSDKConfig.asset` under a `Resources` folder. It is gitignored — create your own. |
| `400 "Invalid price format"` | A locale emitted a comma decimal. Use `InvoFormat.Amount(...)`. |
| `400 "Phone number must start with country code"` | Missing `+`. Use `InvoPhone.Normalize(...)`. |
| Transfer verify returns `404` | A transfer id was sent to the sends verifier. Use `VerifyTransferSmsAsync`. |
| Claim returns `403` | The claim phone does not match the initiate phone digit-for-digit. |
| Claim returns `404` cross-game | Cross-game claims need the receiving game's key. See §4. |
| Purchase shows "failed" but the balance dropped | A `409` was rendered as an error. `ex.IsDuplicate` means it **succeeded**. |
| `401` on every call | Missing or wrong `sdkKey`, or a sandbox key against production. |
| `403 "Game is not active"` | `game_status` is not `live` or `testing`. Check the console. |
| Repeated `429` | An anti-abuse lockout. Respect `ex.RetryAfterSeconds` and stop re-enabling the button. |
| Real-money purchase does nothing | `checkoutSessionEndpoint` is not set. See §2. |
| Catalog image missing | Not https, over 4 MB, or over 4096 px. |

Debug logging is on by default via `Debug.Log`. **Unity writes `Player.log` to disk in release builds** and Android logs to logcat, so never log claim codes, SMS PINs, tokens, or raw response bodies. `InvoApiException.ToString()` is log-safe.

---

For API reference: <https://docs.invo.network>
