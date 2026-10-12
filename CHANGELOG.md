# Changelog

All notable changes to the InvoSDK Unity plugin are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [3.1.0] — 2026-10-12

The SDK is now a Unity package that supports Unity 2021.3 LTS and newer, with a new
themeable UI, separate Sandbox and Production settings, and the 3.0.0 UI work finished.
Tested on Unity 2021.3.45f2, 2022.3.62f3 and 6000.0.58f2 (see the pull request's test matrix).

### Breaking

- **Install as a package.** The SDK moved from `Assets/InvoSDK` + `Assets/APIManager.cs` to
  `Packages/com.invonetwork.unity-sdk`. Install it by git URL
  (`https://github.com/Invo-Technologies/Invo-unity-sdk.git?path=/Packages/com.invonetwork.unity-sdk#v3.1.0`)
  or from the release's `.unitypackage`. Every file kept its GUID, so scenes and prefabs that
  reference SDK scripts, sprites or prefabs keep working; delete the old copied files first.
  Keep `Assets/InvoSDK/Resources` (your config, catalog and window config).
- **New prefabs replace `MainScene`'s panels.** Use `Runtime/Prefabs/InvoSDKWindow.prefab`
  and `InvoAPI.prefab`. The 3.0 panel components are unchanged, so a custom 3.0 layout still
  works.
- **Production keys are never in the project.** The Production tab has no key fields, and a
  production build fails without a production Game ID or an https Game Server URL. Sandbox
  keys and the test player are stripped from production builds.

### Added

- **Unity 2021.3 / 2022.3 support.** `InvoWebRequest.SendAsync` replaces
  `await req.SendWebRequest()` (2023.1+ only); `InvoDelay` replaces `Task.Delay` in polling
  so it also works on WebGL. Assemblies `InvoSDK.Runtime`, `InvoSDK.Editor`,
  `InvoSDK.Editor.Setup` and `InvoSDK.Tests.Editor`. On 2021.3/2022.3 the SDK offers to add
  `com.unity.textmeshpro` when it is missing.
- **Settings window** (InvoSDK > Settings): Sandbox, Production and Game & display tabs.
  Separate Game IDs per environment, one sandbox key per storefront (Steam, Google Play,
  App Store, PlayStation, Xbox, Nintendo Switch, plus Epic Games Store, Web, Platform),
  a build-target-to-storefront map, Test connection, a SANDBOX watermark for development
  builds, and a one-click migration of 3.0 settings. Requests to your game server carry
  `X-Invo-Storefront` so it can attach that storefront's production key.
- **New UI**, themeable from one `InvoTheme` asset (colours, font, radius, spacing; default
  dark theme and an example light theme), with `InvoThemeProvider` and `InvoThemed`:
  - Store: **Top up** (real-money currency packs through INVO hosted checkout) and
    **Items** (spend the balance), with a live balance.
  - Send: the player's own phone (masked, with Change) instead of a field, recent
    recipients, a Plaid-style game picker with search, review, QR approval and holds.
  - Transfer: the player's own balance between games.
  - Collect: everything waiting for the player, each with Collect; held items greyed.
  - Phone settings: change the player's number; secured accounts confirm through INVO's
    emailed link.
  - uGUI match-code prompt (`InvoEnrollmentPromptPanel`).
  - Gamepad and keyboard navigation (`InvoGamepadFocus`), safe-area insets
    (`InvoSafeArea`), phone-pad and number keyboards on mobile.
- **API:** `GetPlayerDestinationsAsync`, `ChangePlayerPhoneAsync`,
  `CreatePhoneChangeRequestAsync`, `GetPhoneChangeRequestAsync`, `ApplyConfirmedPhone`,
  `PlayerPhoneChanged`, `BalanceChanged` / `LastBalance`, `Storefront`,
  `InvoPhone.MaskShort`, `InvoSDKWindowController`, `InvoTabBar`.
- Demo sample with `InvoCaptureDriver` (`-invoCapture <dir>` saves screenshots of every
  screen), and 17 new EditMode tests (94 in total).

### Changed

- The player-token mint sends `player_phone` and `player_name`, as INVO's docs require, and
  logs `phone_mismatch`.
- The Collect screen has no claim-code form. INVO is retiring claim codes; receivers without
  an account in the game collect through the link INVO emails them.
  `ClaimCurrencyAsync` / `ClaimTransferAsync` remain for existing integrations.
- SMS-era wording removed from the send and transfer panels.

### Fixed

- **No text rendered on Unity 2021.3 / 2022.3:** the Inter font asset had an empty material
  reference, which only Unity 6's TextMeshPro could recover from. Regenerated in 2021.3.
- `InvoDeviceApprovalPanelView` could not be saved in a scene or prefab (its class lived in a
  file with another name), so every `approvalView` field read as missing after a reload.
- The transfer panel never selected a destination when no dropdown was assigned.
- `MainScene` had a real-looking phone number typed into its phone fields.

### Removed

- The unused NuGet restore folders under `Packages/` (Stripe.net, Newtonsoft.Json, System.*
  shims, about 73 MB) and `packages.config`.

---

## [3.0.0] — 2026-10-01

Invo retired SMS verification. Sends and transfers are now approved on the player's
phone with INVO's device approval grant (RFC 8628). Built against `invo-backend-api`
`origin/develop` @ `3c892305` and the `@invonetwork/web-sdk` 3.19 reference client.

> **⚠️ Action required: update your UI.** This release was written and compile-checked
> without the Unity editor. **No scene or prefab was changed.** The send and transfer panels
> have new fields to wire and old SMS widgets to remove. Follow
> [InvoSDK-README.md → Updating your UI for 3.0.0](InvoSDK-README.md#updating-your-ui-for-300).

### Breaking

- **Production builds no longer send the game secret.** Every call that needs
  `X-Game-Secret-Key` goes to the new `InvoSDKConfig.gameServerUrl` as
  `<gameServerUrl>/api/<Invo path>`. Your server attaches the key and forwards the call.
  With no game server, production calls throw `InvoApiException` with
  `SDK_GAME_SERVER_REQUIRED`. Sandbox builds may still use `sdkKey` directly. See
  README → Server-side proxy for the allow-list.
- **The panels' step 3 is a phone approval, not an SMS code.** `SenderCurrencyPanel`
  and `TransferCurrencyPanel` gained `approvalView` and `retryApprovalButton`, and
  `SenderCurrencyPanel` gained `collectPendingButton` and `collectPendingText`. The SMS
  fields are kept only so old prefabs load, and are hidden at runtime.
- `VerifySendSmsAsync`, `VerifyTransferSmsAsync`, `ResendSendPinAsync` and
  `ResendTransferPinAsync` are `[Obsolete]`. The panels no longer call them.
- `InvoApiException.ErrorCode` now reads `code`, then `error_code`, then a bare-token
  `error`. Before, it read `error_code` only, which left every `/api/sdk/*` error code null.
- The send panel's step 4 no longer shows the sender a claim code. A send's code goes to
  the receiver.

### Added

- **QR phone approval.** `InvoDeviceApproval.RunAsync(transactionId, flow)` runs the whole
  approval:
  - begin;
  - a QR on desktop, Steam and consoles, or the system browser on iOS and Android;
  - the RFC 8628 poll, honouring `interval` and `slow_down`;
  - the first-time-phone match-code prompt on the game screen;
  - the settle call that moves the money (`/sdk/{transfers|send}/{id}/approve` or
    `/confirm-receipt` with `device_code`).

  Every stage uses the player token.
- `InvoQrCode` / `InvoQrTexture`: a dependency-free QR encoder (byte mode, versions 1–40).
  It was cross-checked module-for-module against `qrcode-generator` on 391 inputs across
  all versions and error-correction levels.
- `IInvoDeviceApprovalView`, `InvoDeviceApprovalPanelView` (uGUI) and
  `InvoDeviceApprovalOverlay` (IMGUI fallback).
- `InvoApprovalStep` (UI): the shared verification step. It waits out guardian and review
  holds, and reads the transaction status back after any ambiguous settle failure before
  reporting.
- **Receiver collect.** The claim panel's Collect button collects a pending send with
  `send_receipt`, using the receiver's own approval. The claim code is now the fallback.
- **Player session.** `SetActivePlayer` (re-scopes and drops the previous token),
  `GetPlayerTokenAsync` (cached, re-minted early, one automatic retry on
  `401 SDK_TOKEN_*`), `PlayerTokenProvider`, `GameServerRequestDecorator`, and
  `GetPendingActionsAsync`.
- `APIManager` stage methods: `BeginDeviceApprovalAsync`, `PollDeviceApprovalAsync`,
  `ConfirmDeviceEnrollmentAsync`, `SettleDeviceApprovalAsync`. `TRANSACTION_NOT_PENDING`
  maps to `not_pending` with a fail-closed `already_settled`.
- Setup Wizard: a Game Server URL field, and a warning when production has none.
- EditMode tests: QR known-answer and structure, the settled-status allow-list, poll
  pacing, channel choice, and pending-item routing.

### Fixed

- `InvoSDKTestPurchaseWindow` imported `Unity.Plastic.Newtonsoft.Json`, the copy bundled
  inside the Version Control package. Projects without that package failed to compile. It
  now uses `Newtonsoft.Json`.
- `retry_after` holding an ISO timestamp (the passkey-recovery cooldown) no longer hides
  `retry_after_seconds`.

### Hosted approval on mobile (previously unreleased)

- **Hosted approval on mobile.** `InvoHostedApproval.OpenHostedApproval(url)` opens Invo's
  hosted approval page in the system browser (iOS authentication session, Android Custom
  Tabs when `androidx.browser` is present, default browser otherwise; never a WebView) and
  fires `ApprovalPageFinished` when the page returns on `invo-sdk-<gameId>://done`. The
  return carries nothing; the game polls its own server. A foreground return while waiting
  counts as the return, `HasPendingReturn` latches a cold-start return, and `Cancel()` is silent.
- `InvoHostedApproval.ApplyEnrollmentState` / `ShowEnrollmentPrompt` / `ShowEnrollmentFinishing` (not needed on mobile, where app_browser enrolment is auto-confirmed):
  the first-time-phone match-code prompt, drawn on an IMGUI overlay by default or on your own
  `IInvoEnrollmentPromptView`.
- `HostedApprovalHandoff` and `EnrollmentInfo` models; `InvoHostedApprovalCore` pure helpers.
- `InvoHostedApprovalBuildPostprocessor` registers the return scheme in `Info.plist` (and links
  `AuthenticationServices.framework`) and in the Android manifest at build time.
- EditMode tests under `Assets/InvoSDK/Tests/Editor`.

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
