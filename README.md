<p align="center">
  <img src="https://console.invo.network/assets/logo-e0324218.svg" alt="Invo Network Logo" width="200"/>
</p>

<h1 align="center">InvoSDK for Unity</h1>

<p align="center">
  <strong>The complete game economy infrastructure for Unity developers</strong>
</p>

<p align="center">
  <a href="https://docs.invo.network"><img src="https://img.shields.io/badge/docs-invo.network-blue.svg" alt="Documentation"></a>
  <a href="#"><img src="https://img.shields.io/badge/unity-6000.0%2B-green.svg" alt="Unity Version"></a>
  <a href="#"><img src="https://img.shields.io/badge/platforms-iOS%20%7C%20Android%20%7C%20Windows%20%7C%20macOS-lightgrey.svg" alt="Platforms"></a>
  <a href="#"><img src="https://img.shields.io/badge/license-MIT-orange.svg" alt="License"></a>
</p>

<p align="center">
  <a href="#-quick-start">Quick Start</a> •
  <a href="#-features">Features</a> •
  <a href="#-installation">Installation</a> •
  <a href="#-documentation">Documentation</a> •
  <a href="#-support">Support</a>
</p>

---

> **📖 Source Documentation**: This README provides a quick-start guide. For complete documentation, API reference, and advanced integration guides, visit **[docs.invo.network](https://docs.invo.network)**

---

## Overview

**InvoSDK** is a Unity plugin that seamlessly integrates your game with the [Invo Network](https://invo.network) platform — a powerful gaming economy infrastructure that enables:

- **In-game currency purchases** with real money via secure Invo Payments
- **Item marketplace** with full catalog management
- **Player-to-player transfers** within your game
- **Cross-game currency transfers** across the Invo Network ecosystem
- **Real-time balance tracking** and transaction history

Whether you're building a mobile game, PC title, or cross-platform experience, InvoSDK provides the tools you need to monetize effectively while creating engaging player economies.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              YOUR UNITY GAME                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐         │
│  │   UI Components │    │   APIManager    │    │  InvoSDKConfig  │         │
│  │                 │───▶│   (Singleton)   │◀───│ (ScriptableObj) │         │
│  │ • Item Purchase │    │                 │    │                 │         │
│  │ • Transfer Flow │    │ • Auth Handler  │    │ • SDK Key       │         │
│  │ • Balance View  │    │ • API Routing   │    │ • Game Settings │         │
│  │ • Featured Shop │    │ • Token Refresh │    │ • Environment   │         │
│  └─────────────────┘    └────────┬────────┘    └─────────────────┘         │
│                                  │                                          │
│                                  │ HTTPS/REST                               │
│                                  ▼                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                          INVO NETWORK CLOUD                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │   Sandbox    │  │  Production  │  │     Invo     │  │  Cross-Game  │    │
│  │ Environment  │  │ Environment  │  │   Payments   │  │  Transfers   │    │
│  │              │  │              │  │              │  │              │    │
│  │ Testing &    │  │ Live Games   │  │  Secure &    │  │ Network of   │    │
│  │ Development  │  │ Only         │  │ PCI Compliant│  │ Connected    │    │
│  │              │  │              │  │              │  │ Games        │    │
│  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Features

### Core Capabilities

| Feature | Description |
|---------|-------------|
| **Currency Purchases** | Allow players to buy in-game currency with real money via secure Invo Payments |
| **Item Marketplace** | Full catalog management with categories, rarities, discounts, and featured items |
| **Player Balances** | Real-time balance tracking with automatic polling and UI components |
| **P2P Transfers** | Enable players to send currency to other players with SMS verification |
| **Cross-Game Transfers** | Connect to the Invo Network ecosystem for inter-game currency transfers |
| **Invo Payments** | Native payment flows for iOS/Android with WebView fallback |

### Developer Experience

| Feature | Description |
|---------|-------------|
| **Setup Wizard** | One-click configuration with guided setup in Unity Editor |
| **Editor Tools** | Test purchases, view balances, and manage catalogs without leaving Unity |
| **Pre-built UI** | Production-ready UI components you can customize or use as-is |
| **Async/Await** | Modern C# async patterns throughout the entire SDK |
| **Auto-retry** | Automatic token refresh and retry on authentication failures |
| **Dual Environment** | Seamless switching between sandbox and production |

---

## Quick Start

### 1. Get Your Credentials

1. Sign up at [console.invo.network](https://console.invo.network)
2. Create a new game project
3. Copy your **Game ID** and **SDK Key**

### 2. Install the SDK

```
Assets/
├── InvoSDK/           ← Copy this folder
├── APIManager.cs      ← Copy this file
└── Resources/
    └── InvoSDKConfig.asset  ← Auto-created by Setup Wizard
```

### 3. Configure

Open **InvoSDK → Setup Wizard** from the Unity menu:

<p align="center">
  <img src="https://docs.invo.network/images/setup-wizard.png" alt="Setup Wizard" width="500"/>
</p>

### 4. Start Using

```csharp
using InvoSDK;

// Get player balance
var balance = await APIManager.Instance.GetPlayerBalanceAsync("player@email.com");
Debug.Log($"Balance: {balance.balances[0].total_balance}");

// Purchase an item
await APIManager.Instance.PurchaseItemAsync(
    playerEmail: "player@email.com",
    playerName: "PlayerOne",
    itemId: "sword_001",
    itemName: "Legendary Sword",
    quantity: 1,
    unitPrice: 9.99f,
    totalPrice: 9.99f,
    onSuccess: (response) => Debug.Log("Purchase successful!"),
    onError: (error) => Debug.LogError(error)
);
```

---

## Installation

### Requirements

| Requirement | Version |
|-------------|---------|
| Unity | 6000.0+ (Unity 6) |
| .NET | Standard 2.1 |
| Newtonsoft.Json | 3.2.1+ (via Package Manager) |

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

   The wizard opens automatically on first import. You can also open it via:
   **InvoSDK → Setup Wizard**

4. **Configure Your Credentials**

   Enter your SDK Key, Game ID, and other settings in the Setup Wizard.

### Package Dependencies

The SDK uses these Unity packages (auto-resolved):

```json
{
  "com.unity.nuget.newtonsoft-json": "3.2.1",
  "com.unity.textmeshpro": "3.0.0",
  "com.unity.inputsystem": "1.14.0"
}
```

---

## Configuration

### InvoSDKConfig Asset

The SDK uses a ScriptableObject for configuration, stored at `Assets/Resources/InvoSDKConfig.asset`:

| Field | Type | Description |
|-------|------|-------------|
| `sdkKey` | string | Your Invo API secret key |
| `gameId` | string | Your unique game identifier |
| `gameName` | string | Display name for your game |
| `playerName` | string | Default player display name |
| `playerEmail` | string | Default player email (for testing) |
| `playerPassword` | string | Sandbox login password |
| `playerPhone` | string | Player phone for SMS verification |
| `gameIconUrl` | string | URL to your game's icon |
| `gameCurrencyName` | string | Name of your in-game currency |
| `gameCurrencyUrl` | string | URL to currency icon |
| `gameCurrencyID` | string | Currency identifier |
| `gameVersion` | string | Your game's version string |
| `useProduction` | bool | Toggle between sandbox/production |

### Environment Configuration

```
┌─────────────────────────────────────────────────────────────────┐
│                     ENVIRONMENT ROUTING                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  useProduction = false (SANDBOX)                                │
│  ├── Host: https://sandbox.invo.network                         │
│  ├── API:  /sandbox/api/*                                       │
│  ├── Auth: /sandbox/auth/*                                      │
│  └── Console: https://dev.console.invo.network                  │
│                                                                 │
│  useProduction = true (PRODUCTION)                              │
│  ├── Host: https://invo.network                                 │
│  ├── API:  /api/*                                               │
│  ├── Auth: /auth/*                                              │
│  └── Console: https://console.invo.network                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

> **⚠️ IMPORTANT**: Always use the **Sandbox** environment during development and testing. Only switch to Production when deploying to live players.

---

## API Reference

### APIManager (Singleton)

Access the API manager anywhere in your code:

```csharp
var api = APIManager.Instance;
```

### Player Balance

```csharp
// Get player's current balance
await api.GetPlayerBalanceAsync(
    email: "player@email.com",
    onSuccess: (response) => {
        foreach (var balance in response.balances) {
            Debug.Log($"{balance.currency_name}: {balance.total_balance}");
        }
    },
    onError: (error) => Debug.LogError(error)
);
```

**Response Model:**
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
    public string available_balance;
    public string reserved_balance;
    public string total_balance;
}
```

### Item Purchases

```csharp
// Purchase an item from your catalog
await api.PurchaseItemAsync(
    playerEmail: "player@email.com",
    playerName: "PlayerOne",
    itemId: "item_001",
    itemName: "Power Boost",
    quantity: 1,
    unitPrice: 4.99f,
    totalPrice: 4.99f,
    onSuccess: (response) => {
        Debug.Log($"Transaction ID: {response.transaction_id}");
    },
    onError: (error) => Debug.LogError(error)
);
```

### Game Items Catalog

```csharp
// Fetch all items in your game catalog
await api.GetGameItemsAsync(
    onSuccess: (response) => {
        foreach (var item in response.items) {
            Debug.Log($"{item.item_name}: ${item.price_usd}");
        }
    },
    onError: (error) => Debug.LogError(error)
);
```

**Response Model:**
```csharp
public class GameItem {
    public string item_id;
    public string item_name;
    public string display_name;
    public string item_category;    // daily, weekly, standard
    public string item_type;        // bundle, currency_pack, etc.
    public string image_url;
    public bool is_featured;
    public bool is_most_popular;
    public float? discount_percentage;
    public float price_usd;
    public float? original_price_usd;
    public double currency_amount;
    public string item_rarity;      // rare, epic, legendary
}
```

### Currency Transfers

#### Get Available Destinations

```csharp
// Get list of games player can transfer currency to
await api.GetAvailableDestinationsAsync(
    onSuccess: (response) => {
        foreach (var game in response.available_games) {
            Debug.Log($"{game.game_name} ({game.currency_name})");
        }
    },
    onError: (error) => Debug.LogError(error)
);
```

#### Initiate Transfer

```csharp
// Start a currency transfer
await api.InitiateSendAsync(
    senderName: "PlayerOne",
    senderEmail: "sender@email.com",
    senderPhone: "+1234567890",
    receivingGameId: "target_game_id",
    receiverPhone: "+0987654321",
    amount: "100",
    onSuccess: (response) => {
        // Store transaction_id for SMS verification
        string txId = response.transaction_id;
    },
    onError: (error) => Debug.LogError(error)
);
```

#### Verify SMS

```csharp
// Verify transfer with SMS code
await api.VerifySmsAsync(
    transactionId: txId,
    pin: "123456",
    onSuccess: (response) => {
        Debug.Log($"Claim code: {response.claim_code}");
    },
    onError: (error) => Debug.LogError(error)
);
```

#### Claim Currency

```csharp
// Claim incoming currency transfer
await api.ClaimCurrencyAsync(
    claimCode: "ABC123",
    playerName: "ReceiverPlayer",
    playerPhone: "+0987654321",
    onSuccess: (response) => {
        Debug.Log($"New balance: {response.new_balance}");
    },
    onError: (error) => Debug.LogError(error)
);
```

### Utility Methods

```csharp
// Load sprite from URL
Sprite sprite = await APIManager.LoadSpriteAsync(imageUrl, targetImage);

// Get configuration values
string email = api.GetPlayerEmail();
string name = api.GetPlayerName();
string gameName = api.GetGameName();
string currency = api.GetGameCurrency();
```

---

## Complete API Endpoints

| Method | HTTP | Endpoint | Description |
|--------|------|----------|-------------|
| `GetPlayerBalanceAsync` | GET | `/player-balances/player/by-email/{email}` | Get player balance |
| `GetGameItemsAsync` | POST | `/v1/game-items/list` | Fetch game item catalog |
| `PurchaseItemAsync` | POST | `/item-purchases/purchase-item` | Purchase an item |
| `GetAvailableDestinationsAsync` | POST | `/currency-sends/available-destinations` | List transfer destinations |
| `InitiateSendAsync` | POST | `/currency-sends/initiate-send` | Start currency send |
| `VerifySmsAsync` | POST | `/currency-sends/verify-sms` | Verify SMS for sends |
| `VerifySmsTransferAsync` | POST | `/transfers/verify-sms` | Verify SMS for transfers |
| `ClaimCurrencyAsync` | POST | `/currency-sends/claim-currency` | Claim received currency |

---

## UI Components

### Pre-built Components

The SDK includes production-ready UI components:

```
Assets/InvoSDK/Scripts/UI/
├── Core/
│   └── InvoSDKWindowManager.cs    # Panel navigation system
├── ItemPurchase/
│   ├── ItemPurchasePanel.cs       # Item grid display
│   ├── ItemPurchaseConfirmPanel.cs # Purchase confirmation
│   └── ItemCardView.cs            # Individual item card
├── Windows/
│   ├── SenderCurrencyPanel.cs     # Currency send UI
│   ├── InvoSDKFeaturedPanel.cs    # Featured items display
│   └── FeaturedItemCard.cs        # Featured item card
├── Featured/
│   ├── FeatureDailyItemCard.cs    # Daily deal cards
│   └── FeatureWeeklyItemCard.cs   # Weekly deal cards
├── Components/
│   ├── InvoStepController.cs      # Multi-step flow controller
│   ├── InvoUIButtonBinder.cs      # Button binding helper
│   └── VerificationCodeInput.cs   # SMS code input
└── TransferCurrencyPanel.cs       # 4-step transfer wizard
```

### Transfer Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    CURRENCY TRANSFER FLOW                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐      │
│  │ Step 1  │───▶│ Step 2  │───▶│ Step 3  │───▶│ Step 4  │      │
│  │         │    │         │    │         │    │         │      │
│  │ Enter   │    │ Review  │    │ Verify  │    │ Success │      │
│  │ Details │    │ Summary │    │ SMS PIN │    │ Confirm │      │
│  │         │    │         │    │         │    │         │      │
│  │ • From  │    │ • Fees  │    │ • 6-dig │    │ • Claim │      │
│  │ • To    │    │ • Net   │    │   code  │    │   code  │      │
│  │ • Amount│    │ • Total │    │         │    │         │      │
│  └─────────┘    └─────────┘    └─────────┘    └─────────┘      │
│       │              │              │              │            │
│       ▼              ▼              ▼              ▼            │
│  [TransferCurrencyPanel.cs - Unified 4-Step Wizard]            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Window Manager

Use `InvoSDKWindowManager` to handle panel navigation:

```csharp
public class InvoSDKWindowManager : MonoBehaviour {
    public List<PanelEntry> panels;  // Configure in Inspector

    public void ShowPanel(string panelId);  // Show specific panel
    public void HideAll();                   // Hide all panels
}
```

---

## Editor Tools

### Setup Wizard

**Menu: InvoSDK → Setup Wizard**

<p align="center">
  <img src="https://docs.invo.network/images/setup-wizard-full.png" alt="Setup Wizard" width="500"/>
</p>

Features:
- Configure all SDK settings
- Quick links to documentation and console
- Test purchase and balance buttons

### Item Catalog Manager

**Menu: InvoSDK → Item Catalog**

Manage your in-game item catalog directly in Unity:
- Add/remove items
- Set prices, discounts, and rarities
- Assign sprites or image URLs
- Organize by category and tags

### Test Purchase Window

**Menu: InvoSDK → Test Purchase**

Test currency purchases without leaving Unity:
- Pre-configured test payment methods
- Auto-generated purchase references
- Direct API response feedback

### Player Balance Window

**Menu: InvoSDK → Player Balance**

Check player balances during development.

---

## Security Best Practices

### SDK Key Protection

```
┌─────────────────────────────────────────────────────────────────┐
│                    ⚠️  CRITICAL SECURITY NOTE                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Your SDK Key (sdkKey) is stored in InvoSDKConfig.asset        │
│                                                                 │
│  ❌ DO NOT ship this key in client builds                       │
│  ❌ DO NOT commit to public repositories                        │
│  ❌ DO NOT share in screenshots or videos                       │
│                                                                 │
│  ✅ Store server-side for production                            │
│  ✅ Use environment variables in CI/CD                          │
│  ✅ Rotate keys if compromised                                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Production Checklist

Before going live:

- [ ] Remove SDK key from client build
- [ ] Set `useProduction = true`
- [ ] Test full purchase flow in sandbox first
- [ ] Implement server-side API key validation
- [ ] Enable webhook notifications for transactions
- [ ] Set up error monitoring and logging

---

## Payment Integration

### Platform Support

| Platform | Method | Implementation |
|----------|--------|----------------|
| iOS | Native SDK | `InvoiOSBridge` via DllImport |
| Android | Native SDK | `InvoAndroidBridge` |
| Windows/macOS | WebView | `InvoWebViewHandler` |
| Unity Editor | WebView | Fallback for testing |

### Payment Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                      PAYMENT FLOW                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Player          Your Game       Invo Payments     Invo API    │
│    │                │                 │                │        │
│    │──Buy Item────▶│                 │                │        │
│    │                │──Create PM────▶│                │        │
│    │                │◀──PM ID───────│                │        │
│    │                │                 │                │        │
│    │                │──Purchase Request + PM ID─────▶│        │
│    │                │                 │                │        │
│    │                │                 │◀──Charge─────│        │
│    │                │                 │──Success────▶│        │
│    │                │                 │                │        │
│    │                │◀──Transaction Complete────────│        │
│    │◀──Item Granted│                 │                │        │
│    │                │                 │                │        │
└─────────────────────────────────────────────────────────────────┘
```

> **📖 Full Documentation**: For complete API reference, advanced configurations, and integration guides, visit [docs.invo.network](https://docs.invo.network)

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| "Config not found!" | Ensure `Assets/Resources/InvoSDKConfig.asset` exists. Run Setup Wizard. |
| "Player email not set" | Set `playerEmail` in config or pass email directly to API methods. |
| JSON parse errors | Verify `Newtonsoft.Json` package is installed and included in build. |
| 401/403 errors | Check SDK key is correct. Verify using correct environment (sandbox vs prod). |
| Balance not updating | Check `balancePollInterval` setting. Verify network connectivity. |
| WebView not opening | On iOS, ensure `NSAppTransportSecurity` allows HTTP. Check WebView permissions. |

### Debug Logging

All SDK logs are prefixed with `[InvoSDK]`:

```csharp
// Enable verbose logging
#if UNITY_EDITOR
Debug.unityLogger.filterLogType = LogType.Log;
#endif
```

### Network Issues

```csharp
// Check connectivity before API calls
if (Application.internetReachability == NetworkReachability.NotReachable) {
    Debug.LogError("No internet connection");
    return;
}
```

---

## Examples

### Complete Purchase Flow

```csharp
using UnityEngine;
using InvoSDK;

public class ShopController : MonoBehaviour
{
    public async void OnBuyButtonClicked(string itemId)
    {
        var api = APIManager.Instance;

        // 1. Check balance first
        var balanceResponse = await api.GetPlayerBalanceAsync(
            api.GetPlayerEmail()
        );

        float currentBalance = float.Parse(
            balanceResponse.balances[0].available_balance
        );

        // 2. Get item details
        var items = await api.GetGameItemsAsync();
        var item = items.items.Find(i => i.item_id == itemId);

        if (item == null) {
            Debug.LogError("Item not found");
            return;
        }

        // 3. Check if player can afford
        if (currentBalance < item.price_usd) {
            Debug.Log("Insufficient balance");
            // Show currency purchase UI
            return;
        }

        // 4. Make purchase
        await api.PurchaseItemAsync(
            playerEmail: api.GetPlayerEmail(),
            playerName: api.GetPlayerName(),
            itemId: item.item_id,
            itemName: item.item_name,
            quantity: 1,
            unitPrice: item.price_usd,
            totalPrice: item.price_usd,
            onSuccess: (response) => {
                Debug.Log($"Purchase successful: {response.transaction_id}");
                // Grant item to player
                GrantItem(item.item_id);
            },
            onError: (error) => {
                Debug.LogError($"Purchase failed: {error}");
            }
        );
    }

    private void GrantItem(string itemId)
    {
        // Your game logic here
    }
}
```

### Currency Transfer Flow

```csharp
using UnityEngine;
using InvoSDK;

public class TransferController : MonoBehaviour
{
    private string currentTransactionId;

    public async void StartTransfer(
        string receiverPhone,
        string targetGameId,
        string amount)
    {
        var api = APIManager.Instance;

        await api.InitiateSendAsync(
            senderName: api.GetPlayerName(),
            senderEmail: api.GetPlayerEmail(),
            senderPhone: "+1234567890",
            receivingGameId: targetGameId,
            receiverPhone: receiverPhone,
            amount: amount,
            onSuccess: (response) => {
                currentTransactionId = response.transaction_id;
                ShowSMSVerificationUI();
            },
            onError: (error) => {
                ShowError(error);
            }
        );
    }

    public async void VerifyTransfer(string smsCode)
    {
        var api = APIManager.Instance;

        await api.VerifySmsAsync(
            transactionId: currentTransactionId,
            pin: smsCode,
            onSuccess: (response) => {
                ShowSuccess($"Transfer complete! Claim code: {response.claim_code}");
            },
            onError: (error) => {
                ShowError(error);
            }
        );
    }
}
```

---

## Changelog

### Version 1.0.0

- Initial release
- Full API integration (balance, purchases, transfers)
- Pre-built UI components
- Editor tools (Setup Wizard, Catalog Manager, Test Purchase)
- Invo Payments integration (iOS, Android, WebView)
- Cross-game transfer support

---

## Support

### Resources

| Resource | Link |
|----------|------|
| Documentation | [docs.invo.network](https://docs.invo.network) |
| Developer Console | [console.invo.network](https://console.invo.network) |
| Sandbox Console | [dev.console.invo.network](https://dev.console.invo.network) |
| API Reference | [docs.invo.network/api](https://docs.invo.network/api) |

### Getting Help

- **Technical Issues**: support@invo.network
- **Account/Billing**: billing@invo.network
- **Partnership Inquiries**: partners@invo.network

### Community

- [Discord Server](https://discord.gg/invo)
- [Twitter @InvoNetwork](https://twitter.com/invonetwork)
- [GitHub Issues](https://github.com/invonetwork/unity-sdk/issues)

---

## License

This SDK is provided under the MIT License. See [LICENSE](LICENSE) for details.

---

<p align="center">
  <strong>Built with ❤️ by the Invo Network Team</strong>
</p>

<p align="center">
  <a href="https://invo.network">Website</a> •
  <a href="https://docs.invo.network">Docs</a> •
  <a href="https://twitter.com/invonetwork">Twitter</a> •
  <a href="https://discord.gg/invo">Discord</a>
</p>
