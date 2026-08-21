# Changelog

All notable changes to the InvoSDK Unity plugin are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.0.0] — 2026-08-21

Audit of the plugin against the live Invo API. Two flows were failing on every
attempt and are now fixed; several call signatures changed as a result.

Major version bump: this release is not source-compatible with 1.0.0. Existing
integrations need the changes in the table below.

### Breaking

Method names and signatures changed. Existing integrations need updating.

| Previous | Current |
|---|---|
| `VerifySmsAsync` | `VerifySendSmsAsync` / `VerifyTransferSmsAsync` |
| `GetAvailableDestinationsAsync` | `GetSendDestinationsAsync` / `GetTransferDestinationsAsync` |
| `ClaimCurrencyAsync(code, name, phone)` | `ClaimCurrencyAsync(code, name, email, phone, playerId?)` |
| `PurchaseItemAsync(email, name, …)` | `PurchaseItemAsync(clientRequestId, email, name, …)`, prices `decimal` |
| `InitiateSendAsync` / `InitiateTransferAsync` | Both take a leading `clientRequestId` |
| `onError: Action<string>` | `onError: Action<InvoApiException>` |

- Transfers and sends use separate endpoints server-side and are not
  interchangeable. The verify and destinations methods are now explicit per flow.
- `client_request_id` is caller-supplied. Mint it once per user intent with
  `APIManager.NewClientRequestId()` and reuse it across retries of that action;
  a fresh id per attempt defeats server-side deduplication.
- `InvoSDKConfig.playerPassword` removed.
- `InvoSDKConfig.asset` is no longer distributed with the repository. Run
  `InvoSDK → Setup Wizard` to create your own.
- Real-money purchases require a checkout-session endpoint on your own server.
  See `InvoSDK-README.md` §2.

### Fixed

- **Cross-game transfers failed at verification.** The panel called the currency-send
  verifier, which filters on transaction type, so every transfer returned 404 with
  funds left reserved until expiry.
- **Currency send claims failed before any lookup.** The claim body used
  `target_player_*` field names and omitted the required `receiver_player_email`.
- **Item purchases failed on comma-decimal locales.** Amounts were serialized with
  the device culture, so `9,99` reached an API that parses `9.99`. All wire values
  now go through `InvoFormat.Amount()`.
- **Phone numbers were silently corrupted.** Input was truncated to 12 digits
  against a 10–15 range, turning valid international numbers into different valid
  numbers and binding claim codes to unreachable phones. A country code was also
  prepended unconditionally. `InvoPhone.Normalize()` now returns strict E.164 or
  null, never a corrupted value.
- **Non-completion responses rendered as success.** `needs_account_selection`
  returns HTTP 200 with nothing credited and was reported as a completed claim.
  All flows branch on the `status` field via `InvoStatus`.
- **A 409 on item purchase was reported as failure.** It signals a replayed request
  whose original succeeded; `InvoApiException.IsDuplicate` now identifies it.
- **429 responses re-enabled the confirm button**, letting players extend their own
  anti-abuse cooldown. The button now honours `RetryAfterSeconds`.
- **The send panel claimed the currency it had just sent**, passing the sender as
  recipient. Claiming moved to its own panel that collects the receiver's details.
- **UI listeners accumulated across panel opens**, so one tap could fire multiple
  requests the server could not deduplicate.
- **Balance displayed `total_balance`**, which includes reserved funds. Now shows
  `available_balance`.
- **Guardian approval (HTTP 202) advanced to the SMS PIN screen** instead of waiting.
- **`verification_method: "in_app"` was ignored**, leaving players waiting for an
  SMS that is suppressed in that mode.
- Balance polling could overlap requests and had no backoff.
- `GameItem.metadata` was typed to reject the API's actual payload.
- Android builds failed to compile against an undefined `StripeAndroidBridge`.

### Added

- `InvoApiException` — typed errors carrying `StatusCode`, `ErrorCode`, `ErrorId`,
  `RetryAfterSeconds`, with `IsDuplicate` / `IsRateLimited` / `IsNetworkError`.
  Match on `ErrorCode`, never on message text.
- `InvoFormat` — locale-independent amount formatting and parsing.
- `InvoPhone` — E.164 normalization, validation messaging, and masking.
- `InvoStatus` — constants for the non-completion response states.
- `ClaimTransferAsync`, `ResendSendPinAsync`, `ResendTransferPinAsync`,
  `GetSendStatusAsync`, `GetTransferStatusAsync`.
- `StartBalancePolling()` / `StopBalancePolling()`.
- `InvoSDKConfig.checkoutSessionEndpoint`, with a per-panel override.
- Account-selection picker, guardian and in-app approval waiting states, PIN resend,
  and claim-code display with expiry and copy-to-clipboard.
- Response models now cover the fields the API actually returns, including
  `fees_preview`, `guardian_approval`, `verification_method` and `new_balance`.

### Removed

- The sandbox login and CSRF handshake. Every endpoint authenticates on a single
  `X-Game-Secret-Key` header; the bearer and CSRF headers were never read.
- `SendWithRetryAsync`, which matched status codes against response body text.
- `StripePaymentManager` and `StripeWebViewHandler` placeholder stubs.

### Security

- Removed the SDK key from a checkout URL opened in the player's browser.
- Cleared credentials from the distributed config asset and untracked it, along
  with build output whose binary embedded the key.
- Removed `playerPassword`; the endpoint it authenticated against is the developer
  console account table, not player accounts.
- Claim codes are no longer written to `Debug.Log`, which persists to `Player.log`
  and logcat.
- Response bodies are no longer surfaced to players — an error body can contain
  another user's contact details.
- Catalog images require HTTPS and are size-capped.

### Documentation

- `InvoSDK-README.md` rewritten, including **What You Must Wire Yourself**: the
  server-side proxy, hosted checkout, in-app verification and passkeys, cross-game
  claims, phone-share recovery, guardian polling, and uncovered API surfaces.
- `README.md` rewritten against the current API surface.

---

## [1.0.0] — 2025-12-05

Initial public release.
