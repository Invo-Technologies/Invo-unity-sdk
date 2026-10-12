# Invo SDK for Unity (`com.invonetwork.unity-sdk`)

In-game currency for Unity: a store (top up with real money, spend on items), player-to-player
sends and cross-game transfers approved on the player's phone, collecting what was sent to you,
and phone-number changes, with a themeable drop-in UI.

**Requirements:** Unity 2021.3 LTS or newer (tested on 2021.3, 2022.3 and 6000.0).
Windows, macOS, Linux, Android, iOS, WebGL and consoles.

The full guide (API reference, error handling, server-side proxy, security) is the repository's
[README](https://github.com/Invo-Technologies/Invo-unity-sdk#readme).

## Install

**Package Manager, by git URL** (recommended). In **Window > Package Manager**, choose
**+ > Add package from git URL** and enter:

```
https://github.com/Invo-Technologies/Invo-unity-sdk.git?path=/Packages/com.invonetwork.unity-sdk#v3.1.1
```

**.unitypackage.** Import `InvoSDK-3.1.1.unitypackage` from the GitHub release.

Dependencies are added automatically: `com.unity.nuget.newtonsoft-json` and `com.unity.ugui`.

**TextMeshPro on 2021.3 and 2022.3.** There it is the separate `com.unity.textmeshpro` package.
New projects from the Unity Hub 3D templates include it; if yours does not, the SDK offers to add
it. Import **Window > TextMeshPro > Import TMP Essential Resources** if Unity asks.

**Input.** The prefabs contain no EventSystem, so they work with either input backend. Add one
EventSystem whose module matches **Project Settings > Player > Active Input Handling**:
`StandaloneInputModule` for the Input Manager, `InputSystemUIInputModule` for the Input System
package, either for Both.

## Set up

1. **InvoSDK > Settings.**
   - **Sandbox tab:** your sandbox Game ID and one sandbox key per storefront you sell on.
     Only Play Mode and Development builds use these keys; every other build is stripped of them.
   - **Production tab:** your production Game ID, your Game Server URL and your checkout endpoint.
     There are no key fields: INVO's docs say a key must never go into a Unity build. Your server
     holds one production key per storefront and receives the storefront in `X-Invo-Storefront`.
   - A production build fails until both are set, and never contains a sandbox key or the test player.
2. Drag `Runtime/Prefabs/InvoAPI.prefab` and `Runtime/Prefabs/InvoSDKWindow.prefab` into your
   first scene, plus an EventSystem.
3. Open the window from your own button: `InvoSDKWindowController.Instance.Open("Store")`
   (or `"Sender"`, `"Transfer"`, `"Collect"`, `"Phone"`).
4. On login: `APIManager.Instance.SetActivePlayer(email, name, phone)`.

Or import the **Demo** sample (**Package Manager > Invo SDK > Samples**).

## Prefabs

| Prefab | What it is |
|---|---|
| `InvoAPI` | `APIManager`, the singleton every panel talks to. One per game. |
| `InvoSDKWindow` | The window: tabs, panels, match-code prompt, SANDBOX watermark, safe area. |
| `Panels/StorePanel` | **Top up** (real-money packs through INVO hosted checkout) and **Items** (spend the balance). |
| `Panels/SendPanel` | Send to a phone: own phone, recent recipients, game picker, review, QR approval. |
| `Panels/TransferPanel` | Move the player's own balance to another game. |
| `Panels/CollectPanel` | Everything waiting for the player, each with Collect. |
| `Panels/PhonePanel` | Change the player's phone number. |
| `Components/*` | Item, pack, featured-pack and deal cards. |

## Make it yours

- **Theme:** duplicate `Runtime/Resources/InvoDefaultTheme`, change colours, font, corner radius
  and spacing, and assign it to `InvoThemeProvider` on the window root. Everything restyles live,
  without touching the prefabs. `InvoLightTheme` is an example. Swap at runtime by setting
  `InvoThemeProvider.theme`.
- **Layout:** make prefab variants and move things around; the panels find their parts through
  serialized fields, not names.
- **Your own UI:** the panels are components with public fields, and the approval and match-code
  screens have interfaces (`IInvoDeviceApprovalView`, `IInvoEnrollmentPromptView`), so you can
  drive the SDK from UI you build yourself.

## Platforms

- Desktop and consoles show a QR that the player scans with their phone; phones open INVO's page
  in the system browser.
- The UI supports gamepads and keyboards (`InvoGamepadFocus`), safe areas (`InvoSafeArea`) and
  phone-pad and number keyboards.
- WebGL is supported; polling uses `InvoDelay`, which does not depend on threads.

## Migrating from a copied `Assets/InvoSDK` folder (3.0 and earlier)

1. Back up your project. Keep `Assets/InvoSDK/Resources` (your config, catalog and window config).
2. Delete the rest of the old SDK: `Assets/APIManager.cs`, `Assets/InvoSDK/Scripts`,
   `Assets/InvoSDK/Tests`, `Assets/InvoSDK/Prefabs`, `Assets/InvoSDK/Inter`,
   `Assets/InvoSDK/NewArt`, `Assets/InvoSDK/Sprites` and `Assets/Plugins/iOS/InvoHostedApproval.mm`.
3. Install the package, then open **InvoSDK > Settings** and press
   **Move the 3.0 key and Game ID into Sandbox**.

Every file kept its GUID, so your scenes and prefabs keep their references, and `APIManager`
keeps its name, namespace and public API.

## Sample

**Demo**: a scene with `InvoAPI`, `InvoSDKWindow` and an EventSystem, sample item catalog and
window config assets, and `InvoCaptureDriver`. The driver does nothing unless the player starts
with `-invoCapture <folder>`, which saves screenshots of every screen using test data.
