// Screenshot driver for the Invo SDK demo. Does nothing unless the player is started with
//   -invoCapture <output folder>
// It then steps through every SDK screen with view-state test data (no money moves, no approval is
// started), renders the SDK canvas into a RenderTexture of each exact capture size, saves PNGs and
// quits. Rendering to a texture means 1080x1920 works on a 1080p monitor, and the overlay canvas is
// captured. Optional: -invoProbe x,y logs every visible graphic under that pixel of the 1920x1080 shot.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InvoSDK;
using InvoSDK.UI;
using InvoSDK.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InvoCaptureDriver : MonoBehaviour
{
    private const string Player = "+15555550111";
    private const string Receiver = "+15555550112";
    private const string Email = "test@example.com";
    private const string ApproveUrl = "https://invo.network/approve?code=ABCD-EFGH";

    private static readonly Vector2Int Landscape = new Vector2Int(1920, 1080);
    private static readonly Vector2Int Small = new Vector2Int(1280, 720);
    private static readonly Vector2Int Portrait = new Vector2Int(1080, 1920);

    private string outDir;
    private Canvas canvas;
    private InvoSDKWindowManager windows;
    private SenderCurrencyPanel send;
    private TransferCurrencyPanel transfer;
    private InvoCollectPanel collect;
    private InvoPhoneSettingsPanel phone;
    private Texture2D qr;
    private int failures;

    private void Awake()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-invoCapture");
        if (i < 0 || i + 1 >= args.Length)
        {
            Destroy(gameObject);
            return;
        }
        outDir = args[i + 1];
        Directory.CreateDirectory(outDir);
        Application.logMessageReceived += OnLog;
        // Recent recipients for the test identity, before any panel reads them.
        InvoRecentRecipientsStore.Clear(Email);
        InvoRecentRecipientsStore.Remember(Email, "+15555550114");
        InvoRecentRecipientsStore.Remember(Email, "+15555550113");
        InvoRecentRecipientsStore.Remember(Email, Receiver);
    }

    private IEnumerator Start()
    {
        if (APIManager.Instance != null)
            APIManager.Instance.SetActivePlayer(Email, "Test Player", Player);
        yield return new WaitForSecondsRealtime(1f);
        windows = Find<InvoSDKWindowManager>();
        send = Find<SenderCurrencyPanel>();
        transfer = Find<TransferCurrencyPanel>();
        collect = Find<InvoCollectPanel>();
        phone = Find<InvoPhoneSettingsPanel>();
        canvas = windows.GetComponentInParent<Canvas>().rootCanvas;
        qr = InvoQrTexture.Create(ApproveUrl);

        var one = new[] { Landscape };
        var responsive = new[] { Landscape, Small, Portrait };

        // Store: top up with real money (hosted checkout), and spend the balance on items.
        yield return Show("Store", 3f);
        ShowTopUp();
        yield return Capture("01-store-top-up", responsive);
        Page("Items");
        yield return new WaitForSecondsRealtime(2f);
        yield return Capture("02-store-items", one);
        Find<ItemPurchaseConfirmPanel>().gameObject.SetActive(true);
        Find<ItemPurchaseConfirmPanel>().Setup(new InvoSDKItem { itemId = "gem_pack", itemName = "Fire Wings", itemDescription = "Leave a trail of embers.", priceUSD = 10f });
        yield return new WaitForSecondsRealtime(1f);
        yield return Capture("03-store-item-confirm", one);
        Find<ItemPurchaseConfirmPanel>().gameObject.SetActive(false);
        Page("TopUp");

        // Send
        yield return Show("Sender", 3f);
        SendStep1();
        yield return Capture("04-send-1-details", responsive);
        OpenPicker(send.gamePicker, "");
        yield return Capture("05-send-1-choose-game", responsive);
        OpenPicker(send.gamePicker, "orb");
        yield return Capture("06-send-1-search-games", one);
        OpenPicker(send.gamePicker, "zzz");
        yield return Capture("07-send-1-search-no-results", one);
        send.gamePicker.Close();
        SendStep2();
        yield return Capture("08-send-2-review", responsive);
        SendStep3();
        yield return Capture("09-send-3-approve-qr", responsive);
        InvoHostedApproval.ShowEnrollmentPrompt("this phone", "4821", _ => { });
        yield return Capture("10-send-3-match-code", one);
        InvoHostedApproval.HideEnrollmentPrompt();
        Invoke(send, "ShowWaitingPanel", send.guardianApprovalPanel, 3);
        SetText(send.guardianApprovalStatusText, "A guardian needs to approve this send. Nothing has moved yet; this screen updates on its own.");
        yield return Capture("11-send-on-hold", one);
        SendStep4();
        yield return Capture("12-send-4-sent", responsive);

        // Transfer
        yield return Show("Transfer", 3f);
        TransferStep(1);
        yield return Capture("13-transfer-1-details", one);
        TransferStep(2);
        yield return Capture("14-transfer-2-review", one);
        TransferStep(3);
        yield return Capture("15-transfer-3-approve-qr", one);
        TransferStep(4);
        yield return Capture("16-transfer-4-done", one);

        // Collect
        yield return Show("Collect", 1f);
        collect.Render(FakePending());
        SetText(collect.statusText, "");
        yield return Capture("17-collect", responsive);
        collect.listSection.SetActive(false);
        collect.approvalView.ShowQr(qr, "WXYZ-2345", "invo.network/approve", "Scan with your phone to collect", null);
        yield return Capture("18-collect-approve-qr", one);
        collect.listSection.SetActive(true);
        collect.approvalView.Hide();

        // Phone number
        yield return Show("Phone", 1f);
        phone.newPhoneInput.text = "+15555550119";
        yield return Capture("19-phone-change", one);
        SetText(phone.pendingText, "We sent a link to your email. Open it and confirm on INVO's page to switch to ••• 0119.");
        SetText(phone.pendingStatusText, "Waiting for you to confirm · expires in 59 min");
        Invoke(phone, "Show", phone.pendingSection);
        yield return Capture("20-phone-check-email", one);
        SetText(phone.doneText, "Confirmed. Your sends are now approved on ••• 0119.");
        Invoke(phone, "Show", phone.doneSection);
        yield return Capture("21-phone-done", one);
        Invoke(phone, "Show", phone.editSection);

        // Error state, and the same screen restyled from the light example theme.
        yield return Show("Sender", 2f);
        SendStep1();
        SetText(send.statusText, "We could not reach INVO. Check your connection and try again.");
        yield return Capture("22-error-offline", one);
        SetText(send.statusText, "");
        var provider = canvas.GetComponent<InvoThemeProvider>();
        provider.theme = Resources.Load<InvoTheme>("InvoLightTheme");
        provider.ApplyAll();
        yield return Capture("23-theme-light", one);
        provider.theme = null;
        provider.ApplyAll();

        Debug.Log($"[InvoCapture] finished, {failures} failure(s)");
        Application.logMessageReceived -= OnLog;
        Application.Quit(failures == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ screens

    private IEnumerator Show(string panelId, float settleSeconds)
    {
        windows.ShowPanel(panelId);
        // Let OnEnable network calls finish (or fail) before overwriting with test data.
        yield return new WaitForSecondsRealtime(settleSeconds);
        if (send != null) SetText(send.statusText, "");
    }

    private void ShowTopUp()
    {
        Page("TopUp");
        var packs = Find<InvoSDKFeaturedPanel>();
        var items = new List<GameItem>
        {
            new GameItem { item_id = "b1", display_name = "Starter Bundle", display_subtitle = "1,200 Gems + 3 boosts", item_category = "weekly", is_featured = true, price_usd = 9.99f, original_price_usd = 14.99f, discount_percentage = 33 },
            new GameItem { item_id = "p1", display_name = "Handful of Gems", display_subtitle = "500 Gems", item_category = "weekly", price_usd = 4.99f },
            new GameItem { item_id = "p2", display_name = "Bag of Gems", display_subtitle = "1,100 Gems", item_category = "weekly", price_usd = 9.99f, is_most_popular = true },
            new GameItem { item_id = "p3", display_name = "Chest of Gems", display_subtitle = "2,400 Gems", item_category = "weekly", price_usd = 19.99f, original_price_usd = 24.99f, discount_percentage = 20 },
            new GameItem { item_id = "p4", display_name = "Vault of Gems", display_subtitle = "5,000 Gems", item_category = "weekly", price_usd = 39.99f },
            new GameItem { item_id = "d1", display_name = "Daily 100 Gems", item_category = "daily", price_usd = 0.99f },
            new GameItem { item_id = "d2", display_name = "Boost bundle", item_category = "daily", price_usd = 1.99f },
        };
        SetField(packs, "allItems", items);
        Invoke(packs, "ClearMessage");
        Invoke(packs, "PopulateWeeklyFeatured");
        Invoke(packs, "PopulateWeeklyItems");
        Invoke(packs, "PopulateDailyDeals");
        var spinner = (GameObject)GetField(packs, "loadingSpinner");
        if (spinner != null) spinner.SetActive(false);
    }

    /// <summary>Switches the Store between its Top up and Items pages.</summary>
    private static void Page(string id)
    {
        foreach (InvoSDKWindowManager manager in Resources.FindObjectsOfTypeAll<InvoSDKWindowManager>())
            if (manager.gameObject.scene.IsValid() && manager.panels.Exists(p => p.panelId == id))
                manager.ShowPanel(id);
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static object GetField(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private void SendStep1()
    {
        Invoke(send, "ShowStep", 1);
        SetText(send.senderPhoneText, InvoPhone.MaskShort(Player));
        SetInput(send.receiverPhoneInput, Receiver);
        if (send.recentRecipients != null) send.recentRecipients.Select(Receiver);
        SetText(send.selectedGameNameText, "Star Raiders");
        SetText(send.selectedGameInitialsText, "SR");
        SetText(send.selectedGameDetailText, "They receive Credits");
        SetInput(send.amountInput, "100");
        SetText(send.receiverGetsText, "Balance 8,500 Gems · They get about 90 Credits");
        SetText(send.amountErrorText, "");
        SetText(send.amountLimitsText, "");
        SetText(send.receiverPhoneErrorText, "");
        SetText(send.statusText, "");
        if (send.nextButtonStep1 != null) send.nextButtonStep1.interactable = true;
    }

    private void OpenPicker(InvoGamePicker picker, string query)
    {
        picker.SetOptions(new List<InvoGameOption>
        {
            new InvoGameOption { id = "sr", name = "Star Raiders", subtitle = "Shooter · PC · Credits", detail = "1,240 Credits", isYours = true },
            new InvoGameOption { id = "fk", name = "Fable Kart", subtitle = "Racing · Switch · Coins", detail = "300 Coins", isYours = true },
            new InvoGameOption { id = "dc", name = "Deep Crawl", subtitle = "RPG · PC · Shards", detail = "55 Shards", isYours = true },
            new InvoGameOption { id = "ot", name = "Orbit Tactics", subtitle = "Strategy · PC · Ore" },
            new InvoGameOption { id = "pb", name = "Pixel Brawl", subtitle = "Fighting · Console · Tokens" },
            new InvoGameOption { id = "or", name = "Orbital Rush", subtitle = "Arcade · Mobile · Stars" },
            new InvoGameOption { id = "lh", name = "Lighthouse Saga", subtitle = "Adventure · PC", available = false, unavailableReason = "Not accepting currency right now" },
            new InvoGameOption { id = "mm", name = "Moonmine", subtitle = "Idle · Mobile · Ore" },
            new InvoGameOption { id = "tw", name = "Tidewalkers", subtitle = "Survival · PC · Shells" },
            new InvoGameOption { id = "gg", name = "Golem Garden", subtitle = "Puzzle · Mobile · Seeds" },
        }, "sr");
        picker.Open();
        if (picker.searchInput != null) picker.searchInput.text = query;
        picker.Refresh();
    }

    private void SendStep2()
    {
        Invoke(send, "ShowStep", 2);
        SetText(send.reviewSendAmount, "100 Gems");
        SetText(send.reviewToPhone, InvoPhone.MaskShort(Receiver));
        SetText(send.reviewToGame, "Star Raiders");
        SetText(send.reviewFromPhone, InvoPhone.MaskShort(Player));
        SetText(send.reviewFromGame, "Demo Game");
        SetText(send.reviewServiceFee, "10 Gems");
        SetText(send.reviewReceiverGets, "90 Credits");
    }

    private void SendStep3()
    {
        Invoke(send, "ShowStep", 3);
        SetText(send.verificationTargetText, $"Sending 100 Gems to {InvoPhone.MaskShort(Receiver)} in Star Raiders");
        send.approvalView.ShowQr(qr, "ABCD-EFGH", "invo.network/approve", "Scan with your phone to approve", null);
        send.approvalView.ShowStatus("Waiting for your phone · code expires in 9:41");
        if (send.retryApprovalButton != null) send.retryApprovalButton.gameObject.SetActive(false);
    }

    private void SendStep4()
    {
        Invoke(send, "ShowStep", 4);
        SetText(send.confirmationTitle, "Sent 100 Gems");
        SetText(send.confirmationSubtitle, $"{InvoPhone.MaskShort(Receiver)} gets 90 Credits in Star Raiders. They'll collect it with their own phone.");
    }

    private void TransferStep(int step)
    {
        Invoke(transfer, "ShowStep", step);
        SetText(transfer.ownPhoneText, InvoPhone.MaskShort(Player));
        SetText(transfer.selectedGameNameText, "Fable Kart");
        SetText(transfer.selectedGameInitialsText, "FK");
        SetText(transfer.selectedGameDetailText, "You receive Coins");
        SetInput(transfer.amountInputField, "50");
        SetText(transfer.receiverGetsText, "Balance 8,500 Gems · You get about 45 Coins");
        SetText(transfer.amountErrorText, "");
        SetText(transfer.destinationsStatusText, "");
        SetText(transfer.reviewAmountText, "50 Gems");
        SetText(transfer.reviewFromGameText, "Demo Game");
        SetText(transfer.reviewToGameText, "Fable Kart");
        SetText(transfer.reviewFeeText, "5 Gems");
        SetText(transfer.reviewReceiverGetsText, "45 Coins");
        SetText(transfer.verificationStatusText, "");
        if (transfer.step1NextButton != null) transfer.step1NextButton.interactable = true;
        if (step == 3)
        {
            if (transfer.inAppApprovalGroup != null) transfer.inAppApprovalGroup.SetActive(true);
            if (transfer.guardianApprovalGroup != null) transfer.guardianApprovalGroup.SetActive(false);
            SetText(transfer.inAppApprovalText, "Moving 50 Gems to Fable Kart");
            transfer.approvalView.ShowQr(qr, "ABCD-EFGH", "invo.network/approve", "Scan with your phone to approve", null);
            transfer.approvalView.ShowStatus("Waiting for your phone · code expires in 9:41");
            if (transfer.retryApprovalButton != null) transfer.retryApprovalButton.gameObject.SetActive(false);
        }
        if (step == 4)
        {
            SetText(transfer.confirmationTitleText, "Transfer complete");
            SetText(transfer.confirmationSubtitleText, "45 Coins are on their way to Fable Kart.");
            SetText(transfer.claimCodeText, "");
            SetText(transfer.claimCodeExpiryText, "");
        }
    }

    private static List<PendingAction> FakePending()
    {
        string In(int days) => DateTime.UtcNow.AddDays(days).AddHours(1).ToString("o");
        return new List<PendingAction>
        {
            new PendingAction { transfer_id = "t1", kind = PendingAction.KindReceivingConfirm, flow = "send", amount = "250", currency = "Gems", counterparty_game = "Fable Kart", expires_at = In(6) },
            new PendingAction { transfer_id = "t2", kind = PendingAction.KindReceivingConfirm, flow = "transfer", amount = "40", currency = "Gems", counterparty_game = "Deep Crawl", expires_at = In(2) },
            new PendingAction { transfer_id = "t3", kind = PendingAction.KindReceivingConfirm, flow = "send", amount = "75", currency = "Gems", counterparty_game = "Star Raiders", held = true, hold_reason = "guardian_pending" },
        };
    }

    // ------------------------------------------------------------------ capture

    private IEnumerator Capture(string name, Vector2Int[] sizes)
    {
        foreach (Vector2Int size in sizes)
        {
            string suffix = size == Landscape ? "" : $"-{size.x}x{size.y}";
            yield return CaptureOne(Path.Combine(outDir, name + suffix + ".png"), size);
        }
    }

    private IEnumerator CaptureOne(string file, Vector2Int size)
    {
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float oldPlane = canvas.planeDistance;

        var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
        var cam = new GameObject("InvoCaptureCamera").AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        Camera main = Camera.main;
        cam.backgroundColor = main != null ? main.backgroundColor : new Color(0.16f, 0.2f, 0.27f, 1f);
        cam.cullingMask = 1 << canvas.gameObject.layer;
        cam.targetTexture = rt;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 10f;
        // Two frames: one for the canvas to adopt the new size, one for layout groups to settle.
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Debug.Log("[InvoCapture] saved " + file);
        Probe(cam, size);

        canvas.renderMode = oldMode;
        canvas.worldCamera = oldCamera;
        canvas.planeDistance = oldPlane;
        cam.targetTexture = null;
        Destroy(cam.gameObject);
        Destroy(tex);
        rt.Release();
        Destroy(rt);
        yield return null;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>With -invoProbe x,y (pixels from the top-left), logs every visible graphic under that point.</summary>
    private void Probe(Camera cam, Vector2Int size)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-invoProbe");
        if (i < 0 || i + 1 >= args.Length || size != Landscape)
            return;
        string[] xy = args[i + 1].Split(',');
        var point = new Vector2(float.Parse(xy[0]), size.y - float.Parse(xy[1]));
        foreach (Graphic g in canvas.GetComponentsInChildren<Graphic>(false))
        {
            if (g.color.a <= 0f || !RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, point, cam))
                continue;
            string path = g.name;
            for (Transform t = g.transform.parent; t != null && t != canvas.transform; t = t.parent)
                path = t.name + "/" + path;
            Debug.Log($"[InvoCapture] probe {xy[0]},{xy[1]}: {path} ({g.GetType().Name})");
        }
    }

    /// <summary>Test tooling only: the panels expose no public step API, so drive the private methods.</summary>
    private static void Invoke(MonoBehaviour target, string method, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (m == null)
            throw new MissingMethodException(target.GetType().Name, method);
        m.Invoke(target, args);
    }

    private static T Find<T>() where T : Component
    {
        foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
            if (candidate.gameObject.scene.IsValid())
                return candidate;
        throw new InvalidOperationException("Not in scene: " + typeof(T).Name);
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label == null) return;
        label.text = value ?? string.Empty;
        label.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }

    private static void SetInput(TMP_InputField input, string value)
    {
        if (input != null) input.text = value;
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception)
            failures++;
    }
}
