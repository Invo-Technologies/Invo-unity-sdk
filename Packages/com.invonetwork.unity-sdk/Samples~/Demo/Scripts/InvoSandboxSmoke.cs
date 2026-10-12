// Sandbox smoke test for the Invo SDK demo. Does nothing unless the player is started with
//   -invoSmoke <result file>
// and the config is in Sandbox mode with a Game ID and a key for this storefront. It then runs, against
// INVO's sandbox, the calls a player's first session makes: balance, item catalog, one item purchase
// with currency, and a send and a transfer as far as the QR approval (begun, never approved). Each step
// is written to the result file as PASS, FAIL or SKIP; the player then quits.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InvoSDK;
using UnityEngine;
using static InvoSDK.TransferResponse;

public class InvoSandboxSmoke : MonoBehaviour
{
    private const string Email = "test@example.com";
    private const string Phone = "+15555550111";
    private const string Receiver = "+15555550112";
    private const string Name = "Test Player";

    private readonly List<string> lines = new List<string>();
    private string outFile;
    private int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-invoSmoke");
        if (i < 0 || i + 1 >= args.Length)
            return;
        var go = new GameObject("InvoSandboxSmoke");
        DontDestroyOnLoad(go);
        go.AddComponent<InvoSandboxSmoke>().outFile = args[i + 1];
    }

    private async void Start()
    {
        try
        {
            await Run();
        }
        catch (Exception ex)
        {
            Fail("driver", ex.GetType().Name + ": " + ex.Message);
        }
        Write($"finished, {failures} failure(s)");
        Debug.Log($"[InvoSmoke] finished, {failures} failure(s)");
        Application.Quit(failures == 0 ? 0 : 1);
    }

    private async Task Run()
    {
        await Task.Yield();
        APIManager api = APIManager.Instance;
        var config = Resources.Load<InvoSDKConfig>("InvoSDKConfig");
        if (api == null || config == null)
        {
            Fail("setup", "no APIManager or InvoSDKConfig in the scene");
            return;
        }
        if (config.useProduction || string.IsNullOrEmpty(config.ActiveSandboxKey) || string.IsNullOrEmpty(config.ActiveGameId))
        {
            Fail("setup", "config is not Sandbox with a Game ID and a key for " + InvoStorefrontResolver.WireName(config.ActiveStorefront));
            return;
        }
        InvoStorefront own = config.ActiveStorefront;
        Pass("setup", $"sandbox, game {config.ActiveGameId}, storefront {InvoStorefrontResolver.WireName(own)}, api {api.ApiBase}");
        api.SetActivePlayer(Email, Name, Phone);

        // Every storefront that has a key: the same read-only call as Settings > Test connection.
        InvoStorefront flow = own;
        bool ownHasDestinations = false;
        InvoStorefront? firstWithDestinations = null;
        foreach (InvoStorefront storefront in Enum.GetValues(typeof(InvoStorefront)))
        {
            if (config.sandboxKeys == null || !config.sandboxKeys.Any(k => k != null && k.storefront == storefront && !string.IsNullOrWhiteSpace(k.sdkKey)))
                continue;
            UseStorefront(config, storefront);
            var sends = await Step($"key {InvoStorefrontResolver.WireName(storefront)}: send destinations", () => api.GetSendDestinationsAsync(),
                r => $"{r.available_games?.Count ?? 0} game(s): {Names(r.available_games)}");
            var transfers = await Step($"key {InvoStorefrontResolver.WireName(storefront)}: transfer destinations", () => api.GetTransferDestinationsAsync(),
                r => $"{r.available_games?.Count ?? 0} game(s): {Names(r.available_games)}");
            bool any = (sends?.available_games?.Count ?? 0) > 0 && (transfers?.available_games?.Count ?? 0) > 0;
            if (storefront == own)
                ownHasDestinations = any;
            if (any && firstWithDestinations == null)
                firstWithDestinations = storefront;
        }
        if (!ownHasDestinations && firstWithDestinations.HasValue)
            flow = firstWithDestinations.Value;
        UseStorefront(config, flow);
        Write($"INFO flows below use the {InvoStorefrontResolver.WireName(flow)} key" +
              (flow == own ? "" : $" ({InvoStorefrontResolver.WireName(own)} has no send and transfer destinations)"));

        AvailableDestinationsResponse sendTo = await Step("send destinations", () => api.GetSendDestinationsAsync(),
            r => $"{r.available_games?.Count ?? 0} game(s)");
        AvailableDestinationsResponse transferTo = await Step("transfer destinations", () => api.GetTransferDestinationsAsync(),
            r => $"{r.available_games?.Count ?? 0} game(s)");

        GameItemListResponse catalog = await Step("item catalog (API)", () => api.GetGameItemsAsync(),
            r => $"{r.items?.Count ?? 0} item(s), {r.items?.Count(x => x.game_id == config.ActiveGameId) ?? 0} for this game");

        PlayerBalanceResponse balance = await Step("player balance", () => api.GetPlayerBalanceAsync(Email),
            r => "balances: " + string.Join(", ", (r.balances ?? new List<Balance>()).Select(b => $"{b.available_balance} {b.currency_name}")),
            treat404AsSkip: "player not created yet in this game");
        decimal available = 0;
        if (balance?.balances != null && balance.balances.Count > 0)
            decimal.TryParse(balance.balances[0].available_balance, System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out available);

        // One item purchase with currency: the cheapest item in the demo catalog, as the Items page does it.
        var local = Resources.Load<InvoSDKItemCatalog>("InvoSDKItemCatalog");
        InvoSDKItem item = local?.items?.Where(x => x != null && x.priceUSD > 0).OrderBy(x => x.priceUSD).FirstOrDefault();
        if (item == null)
            Skip("item purchase", "no item in the demo catalog");
        else if (available < (decimal)item.priceUSD)
            Skip("item purchase", $"balance {available} is below the price {item.priceUSD} of {item.itemName}");
        else
            await Step("item purchase", () => api.PurchaseItemAsync(APIManager.NewClientRequestId(), Email, Name,
                    item.itemId, item.itemName, 1, (decimal)item.priceUSD, (decimal)item.priceUSD, item.category, item.itemDescription),
                r => r.IsSuccess ? $"{item.itemName}: {r.previous_balance} -> {r.new_balance} {r.currency_name}" : throw new Exception("status " + r.status));

        // Send and transfer: initiate, then begin the QR approval. Nothing is approved, so nothing moves.
        AvailableGame sendGame = sendTo?.available_games?.FirstOrDefault();
        if (sendGame == null)
            Skip("send to QR", "no send destination");
        else
        {
            InitiateSendResponse send = await Step("send: initiate", () => api.InitiateSendAsync(APIManager.NewClientRequestId(),
                    Name, Email, Phone, Receiver, null, sendGame.game_id, MinAmount(sendGame.minimum_transfer)),
                r => $"to {sendGame.game_name}: {r.status}, transaction {r.transaction_id}");
            if (send != null && !string.IsNullOrEmpty(send.transaction_id))
                await Step("send: QR approval begun", () => api.BeginDeviceApprovalAsync(send.transaction_id, InvoApprovalFlow.Send, InvoApprovalChannel.Qr),
                    g => string.IsNullOrEmpty(g.verification_uri_complete) ? throw new Exception("no approval URL") : "approval URL issued (not approved)");
        }

        AvailableGame transferGame = transferTo?.available_games?.FirstOrDefault();
        if (transferGame == null)
            Skip("transfer to QR", "no transfer destination");
        else
        {
            InitiateTransferResponse transfer = await Step("transfer: initiate", () => api.InitiateTransferAsync(APIManager.NewClientRequestId(),
                    Name, Email, Phone, Phone, null, transferGame.game_id, MinAmount(transferGame.minimum_transfer)),
                r => $"to {transferGame.game_name}: {r.status}, transaction {r.transaction_id}");
            if (transfer != null && !string.IsNullOrEmpty(transfer.transaction_id))
                await Step("transfer: QR approval begun", () => api.BeginDeviceApprovalAsync(transfer.transaction_id, InvoApprovalFlow.Transfer, InvoApprovalChannel.Qr),
                    g => string.IsNullOrEmpty(g.verification_uri_complete) ? throw new Exception("no approval URL") : "approval URL issued (not approved)");
        }

        await Step("player destinations", () => api.GetPlayerDestinationsAsync(), r => $"{r.destinations?.Count ?? 0} game(s)");
        await Step("pending actions (Collect)", () => api.GetPendingActionsAsync(), r => $"{r.pending?.Count ?? 0} item(s)");
        UseStorefront(config, own);
    }

    /// <summary>Points this platform at another storefront (in memory only), so its sandbox key is used.</summary>
    private static void UseStorefront(InvoSDKConfig config, InvoStorefront storefront)
    {
        InvoBuildPlatform platform = InvoStorefrontResolver.CurrentPlatform;
        config.storefrontMap = config.storefrontMap ?? new List<InvoStorefrontMapping>();
        config.storefrontMap.RemoveAll(m => m == null || m.platform == platform);
        config.storefrontMap.Add(new InvoStorefrontMapping { platform = platform, storefront = storefront });
    }

    private async Task<T> Step<T>(string name, Func<Task<T>> call, Func<T, string> describe, string treat404AsSkip = null) where T : class
    {
        try
        {
            T result = await call();
            if (result == null)
            {
                Fail(name, "empty response");
                return null;
            }
            Pass(name, describe(result));
            return result;
        }
        catch (InvoApiException ex) when (treat404AsSkip != null && ex.StatusCode == 404)
        {
            Skip(name, treat404AsSkip);
        }
        catch (InvoApiException ex)
        {
            Fail(name, $"HTTP {ex.StatusCode} {ex.ErrorCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Fail(name, ex.Message);
        }
        return null;
    }

    private static string MinAmount(string minimum)
    {
        return decimal.TryParse(minimum, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal m) && m > 1
            ? m.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "1";
    }

    private static string Names(List<AvailableGame> games)
    {
        return games == null ? "" : string.Join(", ", games.Take(5).Select(g => g.game_name));
    }

    private void Pass(string step, string detail) => Write($"PASS {step}: {detail}");
    private void Skip(string step, string detail) => Write($"SKIP {step}: {detail}");

    private void Fail(string step, string detail)
    {
        failures++;
        Write($"FAIL {step}: {detail}");
    }

    private void Write(string line)
    {
        lines.Add(line);
        Debug.Log("[InvoSmoke] " + line);
        try { File.WriteAllLines(outFile, lines); }
        catch (Exception) { }
    }
}
