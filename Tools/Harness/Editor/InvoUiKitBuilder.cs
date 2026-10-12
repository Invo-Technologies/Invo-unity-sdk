// Harness-only. Run in Unity 2021.3 so every asset it saves upgrades forward cleanly to 2022.3 and 6.
// Built and run on Unity 2021.3; the APIs it uses are only obsolete on newer versions.
#pragma warning disable CS0618
//
// Builds the Invo SDK 3.1 UI from code, themed through InvoTheme / InvoThemed, and saves:
//   Runtime/Resources/InvoDefaultTheme.asset, Runtime/Resources/InvoLightTheme.asset
//   Runtime/Prefabs/Components/InvoItemCard.prefab
//   Runtime/Prefabs/Panels/{Store,Send,Transfer,Collect,Phone}Panel.prefab
//   Runtime/Prefabs/InvoSDKWindow.prefab, Runtime/Prefabs/InvoAPI.prefab
//   Assets/InvoDemoBuild/InvoDemo.unity (+ Resources), exported to the package's Samples~/Demo
//
// Usage (separate editor runs; 2021.3 can crash creating a scene right after saving prefabs):
//   -executeMethod InvoSDK.Harness.InvoUiKitBuilder.RegenerateFont   (only when the font asset needs it)
//   -executeMethod InvoSDK.Harness.InvoUiKitBuilder.Build
//   -executeMethod InvoSDK.Harness.InvoUiKitBuilder.BuildDemo
using System;
using System.IO;
using InvoSDK.UI;
using InvoSDK.UI.Components;
using InvoSDK.UI.Core;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InvoSDK.Harness
{
    public static class InvoUiKitBuilder
    {
        private const string Pkg = "Packages/com.invonetwork.unity-sdk";
        private const string DemoFolder = "Assets/InvoDemoBuild";
        private const float SheetWidth = 560f;
        private const float SheetHeight = 780f;

        private static TMP_FontAsset font;
        private static InvoTheme theme;
        private static Sprite rounded, outline, circle;
        private static Sprite gemPink, gemGreen;
        private static Sprite iconClose, iconBack, iconChevron, iconSearch, iconCheck, iconPhone, iconMail, iconClock, iconLock;
        private static InvoSDKWindowManager windowManager;

        // ================================================================== entry points

        public static void Build() => Guard(() =>
        {
            LoadAssets();
            CreateThemes();
            ItemCardView card = BuildItemCardPrefab();
            PackCards packs = BuildPackCardPrefabs();
            BuildWindow(card, packs);
            AssetDatabase.SaveAssets();
            Debug.Log("[InvoUiKit] Done.");
        });

        public static void BuildDemo() => Guard(() =>
        {
            if (!AssetDatabase.IsValidFolder(DemoFolder)) AssetDatabase.CreateFolder("Assets", "InvoDemoBuild");
            if (!AssetDatabase.IsValidFolder(DemoFolder + "/Resources")) AssetDatabase.CreateFolder(DemoFolder, "Resources");
            foreach (string asset in new[] { "InvoSDKItemCatalog.asset", "InvoSDKWindowConfig.asset" })
            {
                string dst = DemoFolder + "/Resources/" + asset;
                if (AssetDatabase.LoadMainAssetAtPath(dst) == null)
                    AssetDatabase.CopyAsset("Assets/InvoSDK/Resources/" + asset, dst);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.2f, 0.27f);
            // Old Input Manager module. With "Input System Package (New)" only, replace it with
            // InputSystemUIInputModule (see the package README).
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pkg + "/Runtime/Prefabs/InvoAPI.prefab"));
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pkg + "/Runtime/Prefabs/InvoSDKWindow.prefab"));
            // The repo root's sample project gets the same scene without the screenshot driver.
            EditorSceneManager.SaveScene(scene, DemoFolder + "/RootMainScene.unity", true);
            var driver = AssetDatabase.LoadAssetAtPath<MonoScript>(DemoFolder + "/Scripts/InvoCaptureDriver.cs");
            if (driver != null && driver.GetClass() != null)
                new GameObject("InvoCaptureDriver", driver.GetClass());
            EditorSceneManager.SaveScene(scene, DemoFolder + "/InvoDemo.unity");
            Debug.Log("[InvoUiKit] Demo done.");
        });

        /// <summary>
        /// Rebuilds the Inter SDF font asset from the TTF in this editor's TextMeshPro, into a temporary
        /// asset; build-ui.sh then copies it over the package's asset keeping its GUID and local IDs.
        /// </summary>
        public static void RegenerateFont() => Guard(() =>
        {
            const string fontDir = Pkg + "/Runtime/Art/Inter/";
            const string target = fontDir + "Inter-VariableFont_slnt,wght SDF.asset";
            const string temp = "Assets/InvoBuild/InterRegenerated.asset";
            if (!AssetDatabase.IsValidFolder("Assets/InvoBuild")) AssetDatabase.CreateFolder("Assets", "InvoBuild");
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(fontDir + "Inter-VariableFont_slnt,wght.ttf");
            var asset = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "Inter-VariableFont_slnt,wght SDF";
            AssetDatabase.CreateAsset(asset, temp);
            asset.atlasTextures[0].name = "Inter-VariableFont_slnt,wght Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = "Inter-VariableFont_slnt,wght Atlas Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            var so = new SerializedObject(asset);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            File.Copy(Path.GetFullPath(temp), FileUtil.GetPhysicalPath(target), true);
            AssetDatabase.DeleteAsset(temp);
            AssetDatabase.Refresh();
            Debug.Log("[InvoUiKit] Regenerated " + target);
        });

        /// <summary>Lays the window prefab out at 1920x1080 and logs where the main sections end up.</summary>
        public static void DumpLayout() => Guard(() =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pkg + "/Runtime/Prefabs/InvoSDKWindow.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1920, 1080);
            var sheet = (RectTransform)go.transform.Find("Content/SafeArea/Sheet");
            go.transform.Find("Content/SafeArea/Sheet/Panels/SendPanel").gameObject.SetActive(true);
            go.transform.Find("Content/SafeArea/Sheet/Panels/StorePanel").gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(sheet);
            }
            void Log(string path)
            {
                var t = (RectTransform)go.transform.Find(path);
                if (t == null) { Debug.Log("[InvoUiKit] layout " + path + ": missing"); return; }
                Vector3[] c = new Vector3[4];
                t.GetWorldCorners(c);
                Vector3[] s0 = new Vector3[4];
                sheet.GetWorldCorners(s0);
                Debug.Log($"[InvoUiKit] layout {path}: top={s0[1].y - c[1].y:0} bottom={s0[1].y - c[0].y:0} h={t.rect.height:0} " +
                          $"pref={LayoutUtility.GetPreferredHeight(t):0} min={LayoutUtility.GetMinHeight(t):0}");
            }
            Log("Content/SafeArea/Sheet");
            Log("Content/SafeArea/Sheet/Header");
            Log("Content/SafeArea/Sheet/Panels");
            Log("Content/SafeArea/Sheet/Panels/SendPanel");
            Log("Content/SafeArea/Sheet/Panels/SendPanel/Details");
            foreach (Transform child in go.transform.Find("Content/SafeArea/Sheet/Panels/SendPanel/Details"))
                Log("Content/SafeArea/Sheet/Panels/SendPanel/Details/" + child.name);
            UnityEngine.Object.DestroyImmediate(go);
        });

        private static void Guard(Action work)
        {
            try { work(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        // ================================================================== assets and themes

        private static void LoadAssets()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Pkg + "/Runtime/Art/Inter/Inter-VariableFont_slnt,wght SDF.asset");
            rounded = Sprite("UI/InvoRounded.png");
            outline = Sprite("UI/InvoRoundedOutline.png");
            circle = Sprite("UI/InvoCircle.png");
            iconClose = Sprite("UI/Icons/IconClose.png");
            iconBack = Sprite("UI/Icons/IconBack.png");
            iconChevron = Sprite("UI/Icons/IconChevron.png");
            iconSearch = Sprite("UI/Icons/IconSearch.png");
            iconCheck = Sprite("UI/Icons/IconCheck.png");
            iconPhone = Sprite("UI/Icons/IconPhone.png");
            iconMail = Sprite("UI/Icons/IconMail.png");
            iconClock = Sprite("UI/Icons/IconClock.png");
            iconLock = Sprite("UI/Icons/IconLock.png");
            gemPink = Sprite("Sprites/Dashboard/Group 1321316395.png");
            gemGreen = Sprite("Sprites/Dashboard/Group 1321316393.png");
        }

        private static Sprite Sprite(string rel)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Pkg + "/Runtime/Art/" + rel);
            if (s == null) throw new InvalidOperationException("Sprite not found: " + rel);
            return s;
        }

        private static void CreateThemes()
        {
            EnsureFolder(Pkg + "/Runtime/Resources");
            
            theme = LoadOrCreate<InvoTheme>(Pkg + "/Runtime/Resources/InvoDefaultTheme.asset");
            var defaults = ScriptableObject.CreateInstance<InvoTheme>();
            EditorUtility.CopySerialized(defaults, theme);
            theme.name = "InvoDefaultTheme";
            theme.roundedSprite = rounded;
            theme.roundedOutlineSprite = outline;
            theme.circleSprite = circle;
            EditorUtility.SetDirty(theme);

            // A light example, to show studios what one asset changes.
            var light = LoadOrCreate<InvoTheme>(Pkg + "/Runtime/Resources/InvoLightTheme.asset");
            EditorUtility.CopySerialized(theme, light);
            light.name = "InvoLightTheme";
            light.backdrop = new Color32(30, 27, 22, 140);
            light.surface = new Color32(255, 255, 255, 255);
            light.surfaceRaised = new Color32(244, 241, 234, 255);
            light.field = new Color32(244, 241, 234, 255);
            light.border = new Color32(226, 221, 210, 255);
            light.accent = new Color32(180, 71, 46, 255);
            light.accentSoft = new Color32(250, 233, 226, 255);
            light.onAccent = Color.white;
            light.textPrimary = new Color32(30, 27, 22, 255);
            light.textSecondary = new Color32(95, 90, 80, 255);
            light.textMuted = new Color32(120, 114, 103, 255);
            light.link = new Color32(160, 60, 38, 255);
            light.success = new Color32(47, 107, 79, 255);
            light.successSoft = new Color32(224, 240, 230, 255);
            light.warning = new Color32(166, 104, 16, 255);
            light.danger = new Color32(178, 50, 40, 255);
            light.cornerRadius = 12f;
            light.smallCornerRadius = 8f;
            EditorUtility.SetDirty(light);
            AssetDatabase.SaveAssets();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        // ================================================================== window

        private class PackCards
        {
            public GameObject hero, pack, deal;
        }

        private static void BuildWindow(ItemCardView itemCard, PackCards packs)
        {
            EnsureFolder(Pkg + "/Runtime/Prefabs/Panels");

            // Root: canvas, theme, match-code prompt, controller.
            var root = new GameObject("InvoSDKWindow", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = 5;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<InvoAdaptiveScaler>();
            root.AddComponent<InvoThemeProvider>().theme = null; // null = InvoDefaultTheme

            RectTransform content = Node(root.transform, "Content");
            Stretch(content);
            Image backdrop = Box(content, "Backdrop", InvoThemeRole.Backdrop, InvoThemeShape.Rectangle);
            Stretch(backdrop.rectTransform);

            RectTransform safe = Node(content, "SafeArea");
            Stretch(safe);
            safe.gameObject.AddComponent<InvoSafeArea>();
            Image sheet = Box(safe, "Sheet", InvoThemeRole.Surface, InvoThemeShape.Rounded);
            RectTransform sheetRt = sheet.rectTransform;
            sheetRt.anchorMin = sheetRt.anchorMax = new Vector2(0.5f, 0.5f);
            sheetRt.sizeDelta = new Vector2(SheetWidth, SheetHeight);
            Image sheetBorder = Box(sheetRt, "Border", InvoThemeRole.Border, InvoThemeShape.RoundedOutline);
            Stretch(sheetBorder.rectTransform);
            sheetBorder.raycastTarget = false;
            Ignore(sheetBorder.rectTransform);
            var sheetLayout = VStack(sheetRt, 16, 24, 28, 24, 28);

            // Header: tabs, phone, close.
            RectTransform header = Node(sheetRt, "Header");
            HStack(header, 8, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Size(header, -1, 44);
            RectTransform tabsRt = Box(header, "Tabs", InvoThemeRole.Field, InvoThemeShape.RoundedSmall).rectTransform;
            HStack(tabsRt, 4, 4, 4, 4, 4, TextAnchor.MiddleLeft);
            Size(tabsRt, -1, 44);

            RectTransform panels = Node(sheetRt, "Panels");
            Flex(panels, 1, 1);
            windowManager = panels.gameObject.AddComponent<InvoSDKWindowManager>();
            var tabBar = header.gameObject.AddComponent<InvoTabBar>();
            tabBar.windowManager = windowManager;
            InvoTabBar.Tab storeTab = AddTab(tabsRt, tabBar, "Store", "Store");
            AddTab(tabsRt, tabBar, "Send", "Sender");
            AddTab(tabsRt, tabBar, "Transfer", "Transfer");
            InvoTabBar.Tab collectTab = AddTab(tabsRt, tabBar, "Collect", "Collect");
            TMP_Text collectBadge = Badge(collectTab.button.transform);

            Spacer(header);
            Button phoneButton = IconButton(header, "PhoneButton", iconPhone, "Your phone number");
            ((RectTransform)phoneButton.transform.Find("Icon")).sizeDelta = new Vector2(22, 22);
            BindPanel(phoneButton, "Phone");
            Button closeButton = IconButton(header, "CloseButton", iconClose, "Close");
            collectTab.button.gameObject.name = "CollectTab";

            // Panels.
            GameObject store = BuildStorePanel(panels, itemCard, packs);
            GameObject send = BuildSendPanel(panels);
            GameObject transfer = BuildTransferPanel(panels);
            GameObject collect = BuildCollectPanel(panels, collectBadge);
            GameObject phone = BuildPhonePanel(panels);
            windowManager.panels.Clear();
            Register("Store", store);
            Register("Sender", send);
            Register("Transfer", transfer);
            Register("Collect", collect);
            Register("Phone", phone);

            // Watermark, match-code prompt, controller.
            BuildWatermark(safe);
            BuildEnrollmentPrompt(root.transform);
            var controller = root.AddComponent<InvoSDKWindowController>();
            controller.windowManager = windowManager;
            controller.content = content.gameObject;
            controller.defaultPanelId = "Store";
            UnityEventTools.AddPersistentListener(closeButton.onClick, controller.Close);

            // Panel prefabs first (nested), then the window.
            SavePrefab(store, "Panels/StorePanel");
            SavePrefab(send, "Panels/SendPanel");
            SavePrefab(transfer, "Panels/TransferPanel");
            SavePrefab(collect, "Panels/CollectPanel");
            SavePrefab(phone, "Panels/PhonePanel");
            root.GetComponent<InvoThemeProvider>().ApplyAll();
            SavePrefab(root, "InvoSDKWindow");

            var api = new GameObject("InvoAPI");
            api.AddComponent<APIManager>();
            SavePrefab(api, "InvoAPI");
        }

        private static InvoTabBar.Tab AddTab(RectTransform parent, InvoTabBar bar, string label, string panelId)
        {
            Image bg = Box(parent, label + "Tab", InvoThemeRole.Field, InvoThemeShape.RoundedSmall);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            HStack(bg.rectTransform, 6, 14, 0, 14, 0, TextAnchor.MiddleCenter);
            Size(bg.rectTransform, -1, 36);
            TMP_Text text = Text(bg.rectTransform, "Label", label, InvoTextStyle.BodyStrong, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center);
            Fit(text);
            var tab = new InvoTabBar.Tab
            {
                panelId = panelId,
                button = button,
                background = bg.GetComponent<InvoThemed>(),
                label = text.GetComponent<InvoThemed>()
            };
            bar.tabs.Add(tab);
            return tab;
        }

        private static TMP_Text Badge(Transform parent)
        {
            Image bg = Box(parent, "Badge", InvoThemeRole.Accent, InvoThemeShape.Pill);
            Size(bg.rectTransform, 22, 22);
            HStack(bg.rectTransform, 0, 6, 0, 6, 0, TextAnchor.MiddleCenter);
            TMP_Text t = Text(bg.rectTransform, "Count", "0", InvoTextStyle.Overline, InvoThemeRole.OnAccent, TextAlignmentOptions.Center);
            t.characterSpacing = 0;
            Fit(t);
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bg.gameObject.SetActive(false);
            return t;
        }

        private static void Register(string id, GameObject panel)
        {
            windowManager.panels.Add(new InvoSDKWindowManager.PanelEntry { panelId = id, panel = panel });
            panel.SetActive(id == "Store");
        }

        // ================================================================== store

        /// <summary>
        /// Store = two pages under one tab: Top up (real-money currency packs through INVO hosted
        /// checkout, InvoSDKFeaturedPanel) and Items (spend the balance, ItemPurchasePanel).
        /// </summary>
        private static GameObject BuildStorePanel(RectTransform parent, ItemCardView itemCard, PackCards packs)
        {
            RectTransform panel = PanelRoot(parent, "StorePanel");
            VStack(panel, 12, 0, 0, 0, 0);

            RectTransform head = Node(panel, "Header");
            HStack(head, 8, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Size(head, -1, 36);
            Fit(Text(head, "Title", "Store", InvoTextStyle.Title, InvoThemeRole.TextPrimary));
            Spacer(head);
            TMP_Text balance = Text(head, "Balance", "Balance -", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.MidlineRight);
            balance.gameObject.AddComponent<InvoBalanceLabel>().label = balance;

            // Sub-tabs.
            RectTransform tabsRt = Box(panel, "Pages", InvoThemeRole.Field, InvoThemeShape.RoundedSmall).rectTransform;
            HStack(tabsRt, 4, 4, 4, 4, 4, TextAnchor.MiddleLeft);
            Size(tabsRt, -1, 44);
            RectTransform pages = Node(panel, "PageArea");
            Flex(pages, 1, 1);
            var pageManager = pages.gameObject.AddComponent<InvoSDKWindowManager>();
            var pageTabs = tabsRt.gameObject.AddComponent<InvoTabBar>();
            pageTabs.windowManager = pageManager;
            foreach (InvoTabBar.Tab tab in new[] { AddTab(tabsRt, pageTabs, "Top up", "TopUp"), AddTab(tabsRt, pageTabs, "Items", "Items") })
                Flex((RectTransform)tab.button.transform, 1, 0);

            // Top up: real-money packs.
            RectTransform topUp = PanelRoot(pages, "TopUp");
            VStack(topUp, 10, 0, 0, 0, 0);
            ScrollRect packScroll = Scroll(topUp, "Packs", out RectTransform packContent);
            Flex((RectTransform)packScroll.transform, 1, 1);
            ((VerticalLayoutGroup)packContent.GetComponent<LayoutGroup>()).spacing = 12;
            Fit(Text(packContent, "FeaturedHeader", "FEATURED", InvoTextStyle.Overline, InvoThemeRole.TextSecondary));
            RectTransform heroContainer = Node(packContent, "Featured");
            VStack(heroContainer, 10, 0, 0, 0, 0);
            Fit(Text(packContent, "PacksHeader", "PACKS", InvoTextStyle.Overline, InvoThemeRole.TextSecondary));
            RectTransform packGrid = Node(packContent, "PackGrid");
            var gl = packGrid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(244, 196);
            gl.spacing = new Vector2(12, 12);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            var gridFit = packGrid.gameObject.AddComponent<ContentSizeFitter>();
            gridFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Fit(Text(packContent, "DealsHeader", "DAILY DEALS", InvoTextStyle.Overline, InvoThemeRole.TextSecondary));
            RectTransform deals = Node(packContent, "Deals");
            VStack(deals, 8, 0, 0, 0, 0);
            TMP_Text packError = Text(topUp, "Error", "", InvoTextStyle.Caption, InvoThemeRole.Warning, TextAlignmentOptions.Center);
            TMP_Text loading = Text(topUp, "Loading", "Loading packs...", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center);
            Notice(topUp, iconLock, "Paying opens INVO's secure checkout in your browser. Your balance updates when the payment clears.", InvoThemeRole.Link);
            var featured = topUp.gameObject.AddComponent<InvoSDKFeaturedPanel>();
            Wire(featured, "weeklyFeaturedContainer", heroContainer);
            Wire(featured, "weeklyItemsContainer", packGrid);
            Wire(featured, "dailyDealsContainer", deals);
            Wire(featured, "weeklyFeaturedPrefab", packs.hero);
            Wire(featured, "weeklyItemPrefab", packs.pack);
            Wire(featured, "dailyDealPrefab", packs.deal);
            Wire(featured, "errorText", packError);
            Wire(featured, "loadingSpinner", loading.gameObject);

            // Items: spend the balance.
            RectTransform items = PanelRoot(pages, "Items");
            VStack(items, 10, 0, 0, 0, 0);
            ScrollRect scroll = Scroll(items, "Catalog", out RectTransform scrollContent);
            Flex((RectTransform)scroll.transform, 1, 1);
            DestroyVertical(scrollContent);
            var grid = scrollContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(244, 250);
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;
            TMP_Text empty = Text(items, "EmptyState", "The store is empty right now.", InvoTextStyle.Body, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center);
            empty.gameObject.SetActive(false);
            ItemPurchaseConfirmPanel confirm = BuildConfirmOverlay(panel);
            var store = items.gameObject.AddComponent<ItemPurchasePanel>();
            Wire(store, "gridContainer", scrollContent);
            Wire(store, "itemCardPrefab", itemCard);
            Wire(store, "confirmPanel", confirm);
            Wire(store, "emptyStateText", empty);

            pageManager.panels.Add(new InvoSDKWindowManager.PanelEntry { panelId = "TopUp", panel = topUp.gameObject });
            pageManager.panels.Add(new InvoSDKWindowManager.PanelEntry { panelId = "Items", panel = items.gameObject });
            items.gameObject.SetActive(false);
            pages.gameObject.AddComponent<InvoDefaultPage>().pageId = "TopUp";
            return panel.gameObject;
        }

        private static PackCards BuildPackCardPrefabs()
        {
            EnsureFolder(Pkg + "/Runtime/Prefabs/Components");
            var canvasGo = new GameObject("TempCanvas", typeof(RectTransform), typeof(Canvas));
            var result = new PackCards();

            // Featured hero: wide card, art left, copy and price right.
            Image hero = Box(canvasGo.transform, "InvoPackHeroCard", InvoThemeRole.AccentSoft, InvoThemeShape.Rounded);
            hero.rectTransform.sizeDelta = new Vector2(500, 150);
            HStack(hero.rectTransform, 14, 14, 14, 14, 14, TextAnchor.MiddleLeft);
            Size(hero.rectTransform, -1, 150);
            Border(hero.rectTransform, InvoThemeRole.Accent);
            Image heroArt = Box(hero.rectTransform, "Image", InvoThemeRole.None, InvoThemeShape.Keep);
            heroArt.color = Color.white; heroArt.preserveAspect = true; heroArt.sprite = gemPink;
            Size(heroArt.rectTransform, 120, 120);
            RectTransform heroInfo = Node(hero.rectTransform, "Info");
            VStack(heroInfo, 4, 0, 0, 0, 0);
            Flex(heroInfo, 1, 0);
            var heroCard = hero.gameObject.AddComponent<FeaturedItemCard>();
            heroCard.itemImage = heroArt;
            heroCard.discountBadgeText = Text(heroInfo, "Tag", "-20%", InvoTextStyle.Overline, InvoThemeRole.Warning);
            heroCard.titleText = Text(heroInfo, "Title", "Pack", InvoTextStyle.Heading, InvoThemeRole.TextPrimary);
            heroCard.subtitleText = Text(heroInfo, "Subtitle", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary);
            RectTransform heroPrice = Node(heroInfo, "PriceRow");
            HStack(heroPrice, 8, 0, 4, 0, 0, TextAnchor.MiddleLeft);
            Size(heroPrice, -1, 44);
            heroCard.priceText = Text(heroPrice, "Price", "$9.99", InvoTextStyle.Heading, InvoThemeRole.TextPrimary);
            heroCard.oldPriceText = Text(heroPrice, "OldPrice", "", InvoTextStyle.Caption, InvoThemeRole.TextMuted);
            heroCard.oldPriceText.fontStyle = FontStyles.Strikethrough;
            Spacer(heroPrice);
            heroCard.buyButton = PrimaryButton(heroPrice, "Buy", "Buy", 96, 40);
            result.hero = SaveComponentPrefab(hero.gameObject, "InvoPackHeroCard");

            // Pack: grid card.
            Image pack = Box(canvasGo.transform, "InvoPackCard", InvoThemeRole.Field, InvoThemeShape.Rounded);
            pack.rectTransform.sizeDelta = new Vector2(244, 196);
            VStack(pack.rectTransform, 6, 12, 12, 12, 12, TextAnchor.UpperCenter);
            Border(pack.rectTransform, InvoThemeRole.Border);
            Image packArt = Box(pack.rectTransform, "Icon", InvoThemeRole.None, InvoThemeShape.Keep);
            packArt.color = Color.white; packArt.preserveAspect = true; packArt.sprite = gemPink;
            Size(packArt.rectTransform, 64, 64);
            TMP_Text packName = Text(pack.rectTransform, "Name", "Pack", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center);
            TMP_Text packSub = Text(pack.rectTransform, "Subtitle", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center);
            TMP_Text packTag = Text(pack.rectTransform, "Tag", "", InvoTextStyle.Overline, InvoThemeRole.Warning, TextAlignmentOptions.Center);
            RectTransform packRow = Node(pack.rectTransform, "PriceRow");
            HStack(packRow, 6, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            Size(packRow, -1, 22);
            TMP_Text packPrice = Text(packRow, "Price", "$4.99", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary);
            TMP_Text packOld = Text(packRow, "OldPrice", "", InvoTextStyle.Caption, InvoThemeRole.TextMuted);
            packOld.fontStyle = FontStyles.Strikethrough;
            Spacer(pack.rectTransform);
            Button packBuy = PrimaryButton(pack.rectTransform, "Buy", "Buy", -1, 38);
            var packCard = pack.gameObject.AddComponent<FeatureWeeklyItemCard>();
            Wire(packCard, "iconImage", packArt);
            Wire(packCard, "itemNameText", packName);
            Wire(packCard, "subtitleText", packSub);
            Wire(packCard, "newPriceText", packPrice);
            Wire(packCard, "oldPriceText", packOld);
            Wire(packCard, "discountTagText", packTag);
            Wire(packCard, "purchaseButton", packBuy);
            result.pack = SaveComponentPrefab(pack.gameObject, "InvoPackCard");

            // Daily deal: list row.
            Image deal = Box(canvasGo.transform, "InvoDealCard", InvoThemeRole.Field, InvoThemeShape.RoundedSmall);
            deal.rectTransform.sizeDelta = new Vector2(500, 64);
            HStack(deal.rectTransform, 12, 12, 10, 12, 10, TextAnchor.MiddleLeft);
            Size(deal.rectTransform, -1, 64);
            Image dealArt = Box(deal.rectTransform, "Icon", InvoThemeRole.None, InvoThemeShape.Keep);
            dealArt.color = Color.white; dealArt.preserveAspect = true; dealArt.sprite = gemGreen;
            Size(dealArt.rectTransform, 44, 44);
            TMP_Text dealName = Text(deal.rectTransform, "Name", "Deal", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary);
            Flex(dealName.rectTransform, 1, 0);
            TMP_Text dealPrice = Text(deal.rectTransform, "Price", "$0.99", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary, TextAlignmentOptions.MidlineRight);
            Button dealBuy = PrimaryButton(deal.rectTransform, "Buy", "Buy", 80, 38);
            var dealCard = deal.gameObject.AddComponent<FeatureDailyItemCard>();
            Wire(dealCard, "iconImage", dealArt);
            Wire(dealCard, "itemNameText", dealName);
            Wire(dealCard, "priceText", dealPrice);
            Wire(dealCard, "buyButton", dealBuy);
            result.deal = SaveComponentPrefab(deal.gameObject, "InvoDealCard");

            UnityEngine.Object.DestroyImmediate(canvasGo);
            return result;
        }

        private static GameObject SaveComponentPrefab(GameObject go, string name)
        {
            go.transform.SetParent(null, false);
            ApplyThemeTree(go);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, $"{Pkg}/Runtime/Prefabs/Components/{name}.prefab", out bool ok);
            if (!ok) throw new InvalidOperationException("Could not save " + name);
            UnityEngine.Object.DestroyImmediate(go);
            return saved;
        }

        private static ItemCardView BuildItemCardPrefab()
        {
            EnsureFolder(Pkg + "/Runtime/Prefabs/Components");
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvasGo = new GameObject("TempCanvas", typeof(RectTransform), typeof(Canvas));
            Image card = Box(canvasGo.transform, "InvoItemCard", InvoThemeRole.SurfaceRaised, InvoThemeShape.Rounded);
            card.rectTransform.sizeDelta = new Vector2(240, 250);
            VStack(card.rectTransform, 8, 10, 10, 10, 12);
            Image art = Box(card.rectTransform, "Image", InvoThemeRole.None, InvoThemeShape.Keep);
            art.color = Color.white;
            art.preserveAspect = true;
            Size(art.rectTransform, -1, 130);
            TMP_Text name = Text(card.rectTransform, "Name", "Item", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary);
            Fit(name);
            RectTransform row = Node(card.rectTransform, "PriceRow");
            HStack(row, 8, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Size(row, -1, 40);
            TMP_Text price = Text(row, "Price", "0", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary);
            Fit(price);
            TMP_Text old = Text(row, "OldPrice", "0", InvoTextStyle.Caption, InvoThemeRole.TextMuted);
            old.fontStyle = FontStyles.Strikethrough;
            Fit(old);
            Spacer(row);
            Button buy = PrimaryButton(row, "BuyButton", "Buy", 88, 38);
            var view = card.gameObject.AddComponent<ItemCardView>();
            Wire(view, "itemImage", art);
            Wire(view, "nameText", name);
            Wire(view, "newPriceText", price);
            Wire(view, "oldPriceText", old);
            Wire(view, "purchaseButton", buy);
            card.transform.SetParent(null, false);
            ApplyThemeTree(card.gameObject);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(card.gameObject, Pkg + "/Runtime/Prefabs/Components/InvoItemCard.prefab", out bool ok);
            if (!ok) throw new InvalidOperationException("Could not save the item card prefab");
            return saved.GetComponent<ItemCardView>();
        }

        private static ItemPurchaseConfirmPanel BuildConfirmOverlay(RectTransform panel)
        {
            Image overlay = Box(panel, "ConfirmPurchase", InvoThemeRole.Surface, InvoThemeShape.Rectangle);
            overlay.gameObject.AddComponent<InvoGamepadFocus>();
            Stretch(overlay.rectTransform);
            Ignore(overlay.rectTransform);
            VStack(overlay.rectTransform, 14, 0, 0, 0, 0);
            TMP_Text title = Text(overlay.rectTransform, "Title", "Confirm purchase", InvoTextStyle.Title, InvoThemeRole.TextPrimary);
            Fit(title);

            RectTransform itemRow = Node(overlay.rectTransform, "Item");
            HStack(itemRow, 14, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Size(itemRow, -1, 96);
            Image art = Box(itemRow, "Image", InvoThemeRole.None, InvoThemeShape.Keep);
            art.color = Color.white; art.preserveAspect = true; art.sprite = gemPink;
            Size(art.rectTransform, 96, 96);
            RectTransform info = Node(itemRow, "Info");
            VStack(info, 4, 0, 0, 0, 0);
            Flex(info, 1, 0);
            TMP_Text name = Text(info, "Name", "Item", InvoTextStyle.Heading, InvoThemeRole.TextPrimary); Fit(name);
            TMP_Text rarity = Text(info, "Rarity", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary); Fit(rarity);
            TMP_Text desc = Text(info, "Description", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary); Fit(desc);

            RectTransform card = Card(overlay.rectTransform, "Details");
            TMP_Text price = SummaryRow(card, "Price");
            TMP_Text player = SummaryRow(card, "Player");
            TMP_Text game = SummaryRow(card, "Game");
            TMP_Text balance = SummaryRow(card, "Your balance", last: true);

            TMP_Text info1 = Text(overlay.rectTransform, "Info", "Paid from your in-game balance.", InvoTextStyle.Caption, InvoThemeRole.TextSecondary); Fit(info1);
            TMP_Text warn = Text(overlay.rectTransform, "Warning", "This can't be undone.", InvoTextStyle.Caption, InvoThemeRole.Warning); Fit(warn);
            Spacer(overlay.rectTransform);
            RectTransform buttons = Node(overlay.rectTransform, "Buttons");
            HStack(buttons, 10, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            Size(buttons, -1, 50);
            Button cancel = SecondaryButton(buttons, "CancelButton", "Cancel", -1, 50); Flex((RectTransform)cancel.transform, 1, 0);
            Button confirm = PrimaryButton(buttons, "ConfirmButton", "Buy", -1, 50); Flex((RectTransform)confirm.transform, 1, 0);

            var view = overlay.gameObject.AddComponent<ItemPurchaseConfirmPanel>();
            Wire(view, "itemImage", art);
            Wire(view, "itemNameText", name);
            Wire(view, "itemRarityText", rarity);
            Wire(view, "itemDescriptionText", desc);
            Wire(view, "priceText", price);
            Wire(view, "playerNameText", player);
            Wire(view, "gameNameText", game);
            Wire(view, "balanceText", balance);
            Wire(view, "cancelButton", cancel);
            Wire(view, "confirmButton", confirm);
            Wire(view, "confirmButtonText", confirm.GetComponentInChildren<TMP_Text>(true));
            Wire(view, "warningText", warn);
            Wire(view, "infoText", info1);
            overlay.gameObject.SetActive(false);
            return view;
        }

        // ================================================================== send

        private static GameObject BuildSendPanel(RectTransform parent)
        {
            RectTransform panel = PanelRoot(parent, "SendPanel");
            var send = panel.gameObject.AddComponent<SenderCurrencyPanel>();

            // Step 1: who and how much.
            RectTransform s1 = Step(panel, "Details");
            TMP_Text title = Text(s1, "Title", "Send", InvoTextStyle.Title, InvoThemeRole.TextPrimary); Fit(title);
            RectTransform fromRow = Node(s1, "From");
            HStack(fromRow, 6, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Size(fromRow, -1, 24);
            Fit(Text(fromRow, "Label", "From your phone", InvoTextStyle.Caption, InvoThemeRole.TextSecondary));
            send.senderPhoneText = Text(fromRow, "OwnPhone", "••• 0111", InvoTextStyle.Caption, InvoThemeRole.TextPrimary);
            send.senderPhoneText.fontStyle = FontStyles.Bold;
            Fit(send.senderPhoneText);
            Button change = LinkButton(fromRow, "ChangePhone", "Change");
            BindPanel(change, "Phone");

            send.recentRecipients = RecentRow(s1);
            send.receiverPhoneInput = LabeledInput(s1, "Recipient", "Their phone number", "+1 555 555 0100", out send.receiverPhoneErrorText);
            send.receiverPhoneInput.contentType = TMP_InputField.ContentType.Custom;
            send.receiverPhoneInput.characterValidation = TMP_InputField.CharacterValidation.None;
            send.receiverPhoneInput.keyboardType = TouchScreenKeyboardType.PhonePad;

            GameRow(s1, out send.chooseGameButton, out send.selectedGameInitialsText, out send.selectedGameNameText, out send.selectedGameDetailText);

            AmountBlock(s1, out send.amountInput, out send.receiverGetsText, out send.amountErrorText, out send.amountLimitsText);
            Spacer(s1);
            send.nextButtonStep1 = PrimaryButton(s1, "Continue", "Continue", -1, 52);
            send.step1Panel = s1.gameObject;

            // Step 2: review.
            RectTransform s2 = Step(panel, "Review");
            send.backButtonStep2 = BackRow(s2, "Review");
            TMP_Text sending = Text(s2, "Sending", "You're sending", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(sending);
            send.reviewSendAmount = Text(s2, "Amount", "0", InvoTextStyle.Display, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center); Fit(send.reviewSendAmount);
            RectTransform reviewCard = Card(s2, "Summary");
            send.reviewToPhone = SummaryRow(reviewCard, "To");
            send.reviewToGame = SummaryRow(reviewCard, "In game");
            send.reviewFromPhone = SummaryRow(reviewCard, "From");
            send.reviewFromGame = SummaryRow(reviewCard, "From game");
            send.reviewServiceFee = SummaryRow(reviewCard, "Fee");
            send.reviewReceiverGets = SummaryRow(reviewCard, "They receive", last: true, strong: true);
            Notice(s2, iconLock, "Check the number. Once you approve on your phone, a send can't be undone.", InvoThemeRole.Warning);
            Spacer(s2);
            send.confirmButtonStep2 = PrimaryButton(s2, "Approve", "Approve on my phone", -1, 52);
            send.step2Panel = s2.gameObject;

            // Step 3: approve on the phone.
            RectTransform s3 = Step(panel, "Approve");
            send.backButtonStep3 = BackRow(s3, "Approve on your phone");
            send.verificationTargetText = Text(s3, "Instruction", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(send.verificationTargetText);
            send.approvalView = ApprovalView(s3, "ApprovalView", 220);
            send.retryApprovalButton = SecondaryButton(s3, "ShowCodeAgain", "Show code again", -1, 48);
            send.retryApprovalButton.gameObject.SetActive(false);
            send.step3Panel = s3.gameObject;

            // Holds (alternatives to step 3).
            send.inAppApprovalPanel = HoldPanel(panel, "RecipientHold", "Confirming the recipient", out send.inAppApprovalStatusText, out send.inAppApprovalCancelButton);
            send.guardianApprovalPanel = HoldPanel(panel, "GuardianHold", "Waiting for a guardian", out send.guardianApprovalStatusText, out send.guardianApprovalCancelButton);

            // Step 4: sent.
            RectTransform s4 = Step(panel, "Sent");
            ((VerticalLayoutGroup)s4.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperCenter;
            Spacer(s4, 20);
            SuccessMark(s4);
            send.confirmationTitle = Text(s4, "Title", "Sent", InvoTextStyle.Title, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center); Fit(send.confirmationTitle);
            send.confirmationSubtitle = Text(s4, "Subtitle", "", InvoTextStyle.Body, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(send.confirmationSubtitle);
            send.claimCodeLabel = Text(s4, "ClaimCode", "", InvoTextStyle.Heading, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center); Fit(send.claimCodeLabel);
            send.claimCodeExpiryLabel = Text(s4, "ClaimCodeExpiry", "", InvoTextStyle.Caption, InvoThemeRole.TextMuted, TextAlignmentOptions.Center); Fit(send.claimCodeExpiryLabel);
            send.copyClaimCodeButton = SecondaryButton(s4, "CopyCode", "Copy code", 180, 44);
            send.claimCodeLabel.gameObject.SetActive(false);
            send.claimCodeExpiryLabel.gameObject.SetActive(false);
            send.copyClaimCodeButton.gameObject.SetActive(false);
            Spacer(s4);
            send.doneButton = PrimaryButton(s4, "Done", "Done", -1, 52);
            send.step4Panel = s4.gameObject;

            send.statusText = StatusLine(panel);
            send.gamePicker = GamePicker(panel);
            send.serviceFeePercent = 0.10f;
            HideAllSteps(s1, s2, s3, s4);
            s1.gameObject.SetActive(true);
            return panel.gameObject;
        }

        // ================================================================== transfer

        private static GameObject BuildTransferPanel(RectTransform parent)
        {
            RectTransform panel = PanelRoot(parent, "TransferPanel");
            var transfer = panel.gameObject.AddComponent<TransferCurrencyPanel>();

            RectTransform s1 = Step(panel, "Details");
            Fit(Text(s1, "Title", "Transfer to another game", InvoTextStyle.Title, InvoThemeRole.TextPrimary));
            RectTransform ownRow = Node(s1, "Own");
            HStack(ownRow, 6, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Size(ownRow, -1, 24);
            Fit(Text(ownRow, "Label", "Move your own balance. Approved on", InvoTextStyle.Caption, InvoThemeRole.TextSecondary));
            transfer.ownPhoneText = Text(ownRow, "OwnPhone", "••• 0111", InvoTextStyle.Caption, InvoThemeRole.TextPrimary);
            transfer.ownPhoneText.fontStyle = FontStyles.Bold;
            Fit(transfer.ownPhoneText);
            GameRow(s1, out transfer.chooseGameButton, out transfer.selectedGameInitialsText, out transfer.selectedGameNameText, out transfer.selectedGameDetailText);
            AmountBlock(s1, out transfer.amountInputField, out transfer.receiverGetsText, out transfer.amountErrorText, out TMP_Text limits);
            transfer.feeEstimateNoticeText = limits;
            transfer.destinationsStatusText = Text(s1, "DestinationsStatus", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(transfer.destinationsStatusText);
            Spacer(s1);
            transfer.step1NextButton = PrimaryButton(s1, "Continue", "Continue", -1, 52);
            transfer.step1Panel = s1.gameObject;

            RectTransform s2 = Step(panel, "Review");
            transfer.step2PrevButton = BackRow(s2, "Review");
            transfer.reviewAmountText = Text(s2, "Amount", "0", InvoTextStyle.Display, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center); Fit(transfer.reviewAmountText);
            RectTransform card = Card(s2, "Summary");
            transfer.reviewFromGameText = SummaryRow(card, "From");
            transfer.reviewToGameText = SummaryRow(card, "To");
            transfer.reviewFeeText = SummaryRow(card, "Fee");
            transfer.reviewReceiverGetsText = SummaryRow(card, "You receive", last: true, strong: true);
            Notice(s2, iconLock, "You approve on your phone. A transfer can't be undone once approved.", InvoThemeRole.Warning);
            Spacer(s2);
            transfer.step2NextButton = PrimaryButton(s2, "Approve", "Approve on my phone", -1, 52);
            transfer.step2Panel = s2.gameObject;

            RectTransform s3 = Step(panel, "Approve");
            transfer.step3PrevButton = BackRow(s3, "Approve on your phone");
            RectTransform group = Node(s3, "PhoneApproval");
            VStack(group, 12, 0, 0, 0, 0);
            transfer.inAppApprovalGroup = group.gameObject;
            transfer.inAppApprovalText = Text(group, "Instruction", "Approve this transfer on your phone.", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(transfer.inAppApprovalText);
            transfer.approvalView = ApprovalView(group, "ApprovalView", 220);
            RectTransform guardian = Node(s3, "GuardianHold");
            VStack(guardian, 8, 0, 0, 0, 0);
            transfer.guardianApprovalGroup = guardian.gameObject;
            transfer.guardianApprovalText = Text(guardian, "Status", "Waiting for a guardian to approve.", InvoTextStyle.Body, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(transfer.guardianApprovalText);
            guardian.gameObject.SetActive(false);
            transfer.verificationStatusText = Text(s3, "Status", "", InvoTextStyle.Caption, InvoThemeRole.Warning, TextAlignmentOptions.Center); Fit(transfer.verificationStatusText);
            transfer.retryApprovalButton = SecondaryButton(s3, "ShowCodeAgain", "Show code again", -1, 48);
            transfer.retryApprovalButton.gameObject.SetActive(false);
            transfer.step3Panel = s3.gameObject;

            RectTransform s4 = Step(panel, "Done");
            ((VerticalLayoutGroup)s4.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperCenter;
            Spacer(s4, 20);
            SuccessMark(s4);
            transfer.confirmationTitleText = Text(s4, "Title", "Transfer complete", InvoTextStyle.Title, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center); Fit(transfer.confirmationTitleText);
            transfer.confirmationSubtitleText = Text(s4, "Subtitle", "", InvoTextStyle.Body, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(transfer.confirmationSubtitleText);
            transfer.claimCodeText = Text(s4, "FallbackCode", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(transfer.claimCodeText);
            transfer.claimCodeExpiryText = Text(s4, "FallbackCodeExpiry", "", InvoTextStyle.Caption, InvoThemeRole.TextMuted, TextAlignmentOptions.Center); Fit(transfer.claimCodeExpiryText);
            Spacer(s4);
            transfer.step4CloseButton = PrimaryButton(s4, "Done", "Done", -1, 52);
            transfer.step4Panel = s4.gameObject;

            transfer.gamePicker = GamePicker(panel);
            transfer.serviceFeePercent = 0.1f;
            HideAllSteps(s1, s2, s3, s4);
            s1.gameObject.SetActive(true);
            return panel.gameObject;
        }

        // ================================================================== collect

        private static GameObject BuildCollectPanel(RectTransform parent, TMP_Text tabBadge)
        {
            RectTransform panel = PanelRoot(parent, "CollectPanel");
            VStack(panel, 14, 0, 0, 0, 0);
            var collect = panel.gameObject.AddComponent<InvoCollectPanel>();
            collect.tabBadgeText = tabBadge;

            RectTransform list = Node(panel, "List");
            VStack(list, 14, 0, 0, 0, 0);
            Flex(list, 1, 1);
            collect.listSection = list.gameObject;
            Fit(Text(list, "Title", "Waiting for you", InvoTextStyle.Title, InvoThemeRole.TextPrimary));
            Fit(Text(list, "Subtitle", "Approve on your phone to add it to your balance.", InvoTextStyle.Caption, InvoThemeRole.TextSecondary));
            ScrollRect scroll = Scroll(list, "Items", out RectTransform items);
            Flex((RectTransform)scroll.transform, 1, 1);
            collect.listContainer = items;
            collect.itemTemplate = PendingTemplate(items);
            collect.emptyText = Text(list, "Empty", "Nothing is waiting for you right now.", InvoTextStyle.Body, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center);
            Fit(collect.emptyText);
            Notice(list, iconMail, "Expecting something that isn't here? If you don't play this game yet, INVO emailed you a link to collect it.", InvoThemeRole.Link);

            collect.approvalView = ApprovalView(panel, "ApprovalView", 220);
            collect.statusText = Text(panel, "Status", "", InvoTextStyle.Caption, InvoThemeRole.Warning, TextAlignmentOptions.Center);
            Fit(collect.statusText);
            return panel.gameObject;
        }

        private static InvoPendingItemView PendingTemplate(RectTransform parent)
        {
            Image row = Box(parent, "ItemTemplate", InvoThemeRole.Field, InvoThemeShape.Rounded);
            HStack(row.rectTransform, 12, 14, 12, 14, 12, TextAnchor.MiddleLeft);
            Size(row.rectTransform, -1, 72);
            var view = row.gameObject.AddComponent<InvoPendingItemView>();
            view.canvasGroup = row.gameObject.AddComponent<CanvasGroup>();
            Image av = Box(row.rectTransform, "Avatar", InvoThemeRole.SurfaceRaised, InvoThemeShape.RoundedSmall);
            Size(av.rectTransform, 44, 44);
            view.initialsText = Text(av.rectTransform, "Initials", "FK", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center);
            Stretch(view.initialsText.rectTransform);
            RectTransform info = Node(row.rectTransform, "Info");
            VStack(info, 2, 0, 0, 0, 0);
            Flex(info, 1, 0);
            view.amountText = Text(info, "Amount", "0", InvoTextStyle.Heading, InvoThemeRole.TextPrimary); Fit(view.amountText);
            view.detailText = Text(info, "Detail", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary); Fit(view.detailText);
            view.collectButton = PrimaryButton(row.rectTransform, "Collect", "Collect", 104, 40);
            Image held = Icon(row.rectTransform, "Held", iconClock, InvoThemeRole.Warning, 24);
            view.heldMark = held.gameObject;
            return view;
        }

        // ================================================================== phone

        private static GameObject BuildPhonePanel(RectTransform parent)
        {
            RectTransform panel = PanelRoot(parent, "PhonePanel");
            var phone = panel.gameObject.AddComponent<InvoPhoneSettingsPanel>();

            RectTransform edit = Step(panel, "Edit");
            Button back = BackRow(edit, "Your phone number");
            BindPanel(back, "Sender");
            RectTransform current = Card(edit, "Current");
            RectTransform currentRow = Node(current, "Row");
            HStack(currentRow, 8, 16, 14, 16, 14, TextAnchor.MiddleLeft);
            Size(currentRow, -1, 64);
            RectTransform cur = Node(currentRow, "Text");
            VStack(cur, 2, 0, 0, 0, 0);
            Flex(cur, 1, 0);
            Fit(Text(cur, "Label", "Current", InvoTextStyle.Caption, InvoThemeRole.TextSecondary));
            phone.currentPhoneText = Text(cur, "Phone", "••• 0111", InvoTextStyle.Heading, InvoThemeRole.TextPrimary); Fit(phone.currentPhoneText);
            Fit(Text(currentRow, "Role", "Approves your sends", InvoTextStyle.Caption, InvoThemeRole.Success));
            phone.newPhoneInput = LabeledInput(edit, "NewPhone", "New number", "+1 555 555 0100", out phone.errorText);
            phone.newPhoneInput.contentType = TMP_InputField.ContentType.Custom;
            phone.newPhoneInput.keyboardType = TouchScreenKeyboardType.PhonePad;
            Fit(Text(edit, "Explain", "To keep your account safe, INVO may email you a link to confirm the change. No text message is sent.",
                InvoTextStyle.Caption, InvoThemeRole.TextSecondary));
            Spacer(edit);
            phone.updateButton = PrimaryButton(edit, "Update", "Update number", -1, 52);
            phone.editSection = edit.gameObject;

            RectTransform pending = Step(panel, "Pending");
            ((VerticalLayoutGroup)pending.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperCenter;
            Spacer(pending, 30);
            IconMark(pending, iconMail, InvoThemeRole.Link, InvoThemeRole.AccentSoft);
            Fit(Text(pending, "Title", "Check your email", InvoTextStyle.Title, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center));
            phone.pendingText = Text(pending, "Body", "", InvoTextStyle.Body, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(phone.pendingText);
            phone.pendingStatusText = Text(pending, "Status", "", InvoTextStyle.Caption, InvoThemeRole.Warning, TextAlignmentOptions.Center); Fit(phone.pendingStatusText);
            Spacer(pending);
            RectTransform pb = Node(pending, "Buttons");
            HStack(pb, 10, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            Size(pb, -1, 50);
            phone.cancelButton = SecondaryButton(pb, "Cancel", "Cancel change", -1, 50); Flex((RectTransform)phone.cancelButton.transform, 1, 0);
            phone.resendButton = SecondaryButton(pb, "Resend", "Resend email", -1, 50); Flex((RectTransform)phone.resendButton.transform, 1, 0);
            phone.pendingSection = pending.gameObject;

            RectTransform done = Step(panel, "Done");
            ((VerticalLayoutGroup)done.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperCenter;
            Spacer(done, 30);
            SuccessMark(done);
            phone.doneText = Text(done, "Body", "", InvoTextStyle.Body, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center); Fit(phone.doneText);
            Spacer(done);
            phone.doneButton = PrimaryButton(done, "Done", "Done", -1, 52);
            phone.doneSection = done.gameObject;

            pending.gameObject.SetActive(false);
            done.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // ================================================================== shared pieces

        private static InvoDeviceApprovalPanelView ApprovalView(RectTransform parent, string name, float qr)
        {
            RectTransform root = Node(parent, name);
            // Children keep their own width (a square QR), capped at the panel's.
            VStack(root, 12, 0, 4, 0, 4, TextAnchor.UpperCenter).childForceExpandWidth = false;
            var view = root.gameObject.AddComponent<InvoDeviceApprovalPanelView>();
            view.root = root.gameObject;
            view.headlineText = Text(root, "Headline", "Scan with your phone to approve", InvoTextStyle.Heading, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center);
            Fit(view.headlineText);
            Image frame = Box(root, "QRFrame", InvoThemeRole.None, InvoThemeShape.Rounded);
            frame.color = Color.white;
            Size(frame.rectTransform, qr + 24, qr + 24);
            RectTransform qrRt = Node(frame.rectTransform, "QR");
            qrRt.anchorMin = qrRt.anchorMax = new Vector2(0.5f, 0.5f);
            qrRt.sizeDelta = new Vector2(qr, qr);
            view.qrImage = qrRt.gameObject.AddComponent<RawImage>();
            view.manualEntryText = Text(root, "ManualEntry", "No camera? Go to invo.network/approve and enter ABCD-EFGH", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center);
            Fit(view.manualEntryText);
            Image pill = Box(root, "StatusPill", InvoThemeRole.Field, InvoThemeShape.Pill);
            HStack(pill.rectTransform, 8, 14, 8, 14, 8, TextAnchor.MiddleCenter);
            var pillFit = pill.gameObject.AddComponent<ContentSizeFitter>();
            pillFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            pillFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image dot = Box(pill.rectTransform, "Dot", InvoThemeRole.Accent, InvoThemeShape.Circle);
            Size(dot.rectTransform, 8, 8);
            view.statusText = Text(pill.rectTransform, "Status", "Waiting for your phone", InvoTextStyle.Caption, InvoThemeRole.TextSecondary);
            Fit(view.statusText);
            var hide = pill.gameObject.AddComponent<InvoHideWhenEmpty>();
            hide.text = view.statusText;
            RectTransform buttons = Node(root, "Buttons");
            HStack(buttons, 10, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            Size(buttons, -1, 46);
            view.reopenButton = PrimaryButton(buttons, "OpenAgain", "Open again", 170, 46);
            view.cancelButton = SecondaryButton(buttons, "Cancel", "Cancel", 150, 46);
            return view;
        }

        private static InvoGamePicker GamePicker(RectTransform panel)
        {
            Image overlay = Box(panel, "GamePicker", InvoThemeRole.Surface, InvoThemeShape.Rectangle);
            Stretch(overlay.rectTransform);
            Ignore(overlay.rectTransform);
            VStack(overlay.rectTransform, 14, 0, 0, 0, 0);
            overlay.gameObject.AddComponent<InvoGamepadFocus>();
            var picker = overlay.gameObject.AddComponent<InvoGamePicker>();
            picker.root = overlay.gameObject;
            picker.closeButton = BackRow(overlay.rectTransform, "Choose a game");

            Image search = Box(overlay.rectTransform, "Search", InvoThemeRole.Field, InvoThemeShape.RoundedSmall);
            HStack(search.rectTransform, 10, 14, 0, 14, 0, TextAnchor.MiddleLeft);
            Size(search.rectTransform, -1, 50);
            Border(search.rectTransform, InvoThemeRole.Accent);
            Icon(search.rectTransform, "Icon", iconSearch, InvoThemeRole.TextSecondary, 20);
            picker.searchInput = BareInput(search.rectTransform, "Input", "Search games");
            Flex((RectTransform)picker.searchInput.transform, 1, 0);

            RectTransform yours = Node(overlay.rectTransform, "YourGames");
            VStack(yours, 8, 0, 0, 0, 0);
            picker.yourGamesSection = yours.gameObject;
            Fit(Text(yours, "Header", "GAMES YOU PLAY", InvoTextStyle.Overline, InvoThemeRole.TextSecondary));
            RectTransform grid = Node(yours, "Grid");
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(160, 118);
            gl.spacing = new Vector2(12, 12);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            var gridFit = grid.gameObject.AddComponent<ContentSizeFitter>();
            gridFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            picker.yourGamesContainer = grid;
            picker.tileTemplate = GameTile(grid);
            picker.maxYourGames = 3;

            picker.listHeader = Text(overlay.rectTransform, "ListHeader", "MORE GAMES ON INVO", InvoTextStyle.Overline, InvoThemeRole.TextSecondary); Fit(picker.listHeader);
            ScrollRect scroll = Scroll(overlay.rectTransform, "List", out RectTransform list);
            Flex((RectTransform)scroll.transform, 1, 1);
            picker.listContainer = list;
            picker.rowTemplate = GameRowTemplate(list);
            picker.maxRowsBeforeSearch = 6;
            picker.emptyText = Text(overlay.rectTransform, "Empty", "", InvoTextStyle.Body, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(picker.emptyText);
            picker.footerText = Text(overlay.rectTransform, "Footer", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(picker.footerText);
            overlay.gameObject.SetActive(false);
            return picker;
        }

        private static InvoGameEntryView GameTile(RectTransform parent)
        {
            Image tile = Box(parent, "TileTemplate", InvoThemeRole.Field, InvoThemeShape.Rounded);
            // Children keep their own width so the avatar stays square.
            VStack(tile.rectTransform, 6, 8, 12, 8, 10, TextAnchor.UpperCenter).childForceExpandWidth = false;
            var view = tile.gameObject.AddComponent<InvoGameEntryView>();
            view.button = tile.gameObject.AddComponent<Button>();
            view.button.targetGraphic = tile;
            view.canvasGroup = tile.gameObject.AddComponent<CanvasGroup>();
            Avatar(tile.rectTransform, 44, out view.icon, out view.initialsBackground, out view.initialsText);
            view.nameText = Text(tile.rectTransform, "Name", "Game", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center); Fit(view.nameText);
            view.nameText.enableWordWrapping = false; view.nameText.overflowMode = TextOverflowModes.Ellipsis;
            view.detailText = Text(tile.rectTransform, "Detail", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(view.detailText);
            Image sel = Box(tile.rectTransform, "Selected", InvoThemeRole.Accent, InvoThemeShape.RoundedOutline);
            Stretch(sel.rectTransform); Ignore(sel.rectTransform); sel.raycastTarget = false;
            view.selectedMark = sel.gameObject;
            return view;
        }

        private static InvoGameEntryView GameRowTemplate(RectTransform parent)
        {
            Image row = Box(parent, "RowTemplate", InvoThemeRole.Surface, InvoThemeShape.RoundedSmall);
            HStack(row.rectTransform, 12, 8, 8, 8, 8, TextAnchor.MiddleLeft);
            Size(row.rectTransform, -1, 60);
            var view = row.gameObject.AddComponent<InvoGameEntryView>();
            view.button = row.gameObject.AddComponent<Button>();
            view.button.targetGraphic = row;
            view.canvasGroup = row.gameObject.AddComponent<CanvasGroup>();
            Avatar(row.rectTransform, 40, out view.icon, out view.initialsBackground, out view.initialsText);
            RectTransform info = Node(row.rectTransform, "Info");
            VStack(info, 2, 0, 0, 0, 0);
            Flex(info, 1, 0);
            view.nameText = Text(info, "Name", "Game", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary); Fit(view.nameText);
            view.subtitleText = Text(info, "Subtitle", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary); Fit(view.subtitleText);
            Icon(row.rectTransform, "Chevron", iconChevron, InvoThemeRole.TextMuted, 16);
            Image sel = Box(row.rectTransform, "Selected", InvoThemeRole.Accent, InvoThemeShape.RoundedSmallOutline);
            Stretch(sel.rectTransform); Ignore(sel.rectTransform); sel.raycastTarget = false;
            view.selectedMark = sel.gameObject;
            return view;
        }

        private static void Avatar(RectTransform parent, float size, out Image icon, out Image initialsBg, out TMP_Text initials)
        {
            RectTransform holder = Node(parent, "Avatar");
            Size(holder, size, size);
            initialsBg = Box(holder, "Initials", InvoThemeRole.SurfaceRaised, InvoThemeShape.RoundedSmall);
            Stretch(initialsBg.rectTransform);
            initialsBg.GetComponent<InvoThemed>().role = InvoThemeRole.None; // tinted per game at runtime
            initials = Text(initialsBg.rectTransform, "Text", "SR", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center);
            Stretch(initials.rectTransform);
            icon = Box(holder, "Icon", InvoThemeRole.None, InvoThemeShape.RoundedSmall);
            icon.color = Color.white;
            Stretch(icon.rectTransform);
            icon.gameObject.SetActive(false);
        }

        private static InvoRecentRecipients RecentRow(RectTransform parent)
        {
            RectTransform section = Node(parent, "Recent");
            VStack(section, 8, 0, 0, 0, 0);
            var recents = section.gameObject.AddComponent<InvoRecentRecipients>();
            recents.section = section.gameObject;
            Fit(Text(section, "Header", "RECENT", InvoTextStyle.Overline, InvoThemeRole.TextSecondary));
            RectTransform row = Node(section, "Row");
            HStack(row, 10, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Size(row, -1, 84);
            recents.container = row;

            Image chip = Box(row, "ChipTemplate", InvoThemeRole.Field, InvoThemeShape.Rounded);
            VStack(chip.rectTransform, 6, 4, 10, 4, 8, TextAnchor.UpperCenter);
            Size(chip.rectTransform, 84, 84);
            var c = chip.gameObject.AddComponent<InvoRecipientChip>();
            c.button = chip.gameObject.AddComponent<Button>();
            c.button.targetGraphic = chip;
            Image badge = Box(chip.rectTransform, "Badge", InvoThemeRole.AccentSoft, InvoThemeShape.Circle);
            Size(badge.rectTransform, 38, 38);
            c.badgeText = Text(badge.rectTransform, "Digits", "12", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center);
            Stretch(c.badgeText.rectTransform);
            c.label = Text(chip.rectTransform, "Label", "••• 0112", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center);
            c.label.fontSize = 12; Fit(c.label);
            Image sel = Box(chip.rectTransform, "Selected", InvoThemeRole.Accent, InvoThemeShape.RoundedOutline);
            Stretch(sel.rectTransform); Ignore(sel.rectTransform); sel.raycastTarget = false;
            c.selectedMark = sel.gameObject;
            recents.chipTemplate = c;
            section.gameObject.SetActive(false);
            return recents;
        }

        private static void GameRow(RectTransform parent, out Button button, out TMP_Text initials, out TMP_Text name, out TMP_Text detail)
        {
            Image row = Box(parent, "Game", InvoThemeRole.Field, InvoThemeShape.RoundedSmall);
            HStack(row.rectTransform, 12, 14, 10, 14, 10, TextAnchor.MiddleLeft);
            Size(row.rectTransform, -1, 66);
            Border(row.rectTransform, InvoThemeRole.Border);
            button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row;
            Image av = Box(row.rectTransform, "Avatar", InvoThemeRole.AccentSoft, InvoThemeShape.RoundedSmall);
            Size(av.rectTransform, 42, 42);
            initials = Text(av.rectTransform, "Initials", "SR", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center);
            Stretch(initials.rectTransform);
            RectTransform info = Node(row.rectTransform, "Info");
            VStack(info, 2, 0, 0, 0, 0);
            Flex(info, 1, 0);
            name = Text(info, "Name", "Choose a game", InvoTextStyle.BodyStrong, InvoThemeRole.TextPrimary); Fit(name);
            detail = Text(info, "Detail", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary); Fit(detail);
            Fit(Text(row.rectTransform, "Change", "Change game", InvoTextStyle.Caption, InvoThemeRole.Link));
        }

        private static void AmountBlock(RectTransform parent, out TMP_InputField amount, out TMP_Text net, out TMP_Text error, out TMP_Text limits)
        {
            RectTransform block = Node(parent, "Amount");
            VStack(block, 4, 0, 4, 0, 4, TextAnchor.UpperCenter);
            amount = BareInput(block, "Input", "0");
            amount.contentType = TMP_InputField.ContentType.DecimalNumber;
            amount.characterValidation = TMP_InputField.CharacterValidation.Decimal;
            amount.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            var text = (TMP_Text)amount.textComponent;
            text.alignment = TextAlignmentOptions.Center;
            text.GetComponent<InvoThemed>().textStyle = InvoTextStyle.Display;
            var ph = (TMP_Text)amount.placeholder;
            ph.alignment = TextAlignmentOptions.Center;
            ph.GetComponent<InvoThemed>().textStyle = InvoTextStyle.Display;
            Size((RectTransform)amount.transform, 300, 64);
            net = Text(block, "Net", "", InvoTextStyle.Caption, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center); Fit(net);
            limits = Text(block, "Limits", "", InvoTextStyle.Caption, InvoThemeRole.TextMuted, TextAlignmentOptions.Center); Fit(limits);
            error = Text(block, "Error", "", InvoTextStyle.Caption, InvoThemeRole.Danger, TextAlignmentOptions.Center); Fit(error);
        }

        private static TMP_InputField LabeledInput(RectTransform parent, string name, string label, string placeholder, out TMP_Text error)
        {
            RectTransform group = Node(parent, name);
            VStack(group, 6, 0, 0, 0, 0);
            Fit(Text(group, "Label", label, InvoTextStyle.Caption, InvoThemeRole.TextSecondary));
            Image bg = Box(group, "Field", InvoThemeRole.Field, InvoThemeShape.RoundedSmall);
            Size(bg.rectTransform, -1, 50);
            Border(bg.rectTransform, InvoThemeRole.Border);
            TMP_InputField input = BareInput(bg.rectTransform, "Input", placeholder);
            Stretch((RectTransform)input.transform);
            ((RectTransform)input.transform).offsetMin = new Vector2(14, 0);
            ((RectTransform)input.transform).offsetMax = new Vector2(-14, 0);
            error = Text(group, "Error", "", InvoTextStyle.Caption, InvoThemeRole.Danger); Fit(error);
            return input;
        }

        /// <summary>A borderless TMP input; the caller draws the field.</summary>
        private static TMP_InputField BareInput(RectTransform parent, string name, string placeholder)
        {
            RectTransform rt = Node(parent, name);
            var input = rt.gameObject.AddComponent<TMP_InputField>();
            RectTransform area = Node(rt, "Text Area");
            Stretch(area);
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text ph = Text(area, "Placeholder", placeholder, InvoTextStyle.Body, InvoThemeRole.TextMuted, TextAlignmentOptions.MidlineLeft);
            Stretch(ph.rectTransform);
            ph.enableWordWrapping = false;
            TMP_Text text = Text(area, "Text", "", InvoTextStyle.Body, InvoThemeRole.TextPrimary, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            text.enableWordWrapping = false;
            input.textViewport = area;
            input.textComponent = (TextMeshProUGUI)text;
            input.placeholder = ph;
            input.fontAsset = font;
            input.caretColor = theme.textPrimary;
            input.customCaretColor = true;
            input.selectionColor = new Color(theme.accent.r, theme.accent.g, theme.accent.b, 0.35f);
            Size(rt, -1, 50);
            Flex(rt, 1, 0);
            return input;
        }

        private static Button BackRow(RectTransform parent, string title)
        {
            RectTransform row = Node(parent, "Header");
            HStack(row, 10, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Size(row, -1, 40);
            Button back = IconButton(row, "Back", iconBack, "Back");
            Fit(Text(row, "Title", title, InvoTextStyle.Heading, InvoThemeRole.TextPrimary));
            return back;
        }

        private static GameObject HoldPanel(RectTransform parent, string name, string title, out TMP_Text status, out Button cancel)
        {
            RectTransform hold = Step(parent, name);
            ((VerticalLayoutGroup)hold.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperCenter;
            Spacer(hold, 40);
            IconMark(hold, iconClock, InvoThemeRole.Warning, InvoThemeRole.SurfaceRaised);
            Fit(Text(hold, "Title", title, InvoTextStyle.Title, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center));
            status = Text(hold, "Status", "Nothing has moved yet. This screen updates on its own.", InvoTextStyle.Body, InvoThemeRole.TextSecondary, TextAlignmentOptions.Center);
            Fit(status);
            Spacer(hold);
            cancel = SecondaryButton(hold, "Cancel", "Stop waiting", -1, 50);
            hold.gameObject.SetActive(false);
            return hold.gameObject;
        }

        private static void SuccessMark(RectTransform parent) => IconMark(parent, iconCheck, InvoThemeRole.Success, InvoThemeRole.SuccessSoft);

        private static void IconMark(RectTransform parent, Sprite icon, InvoThemeRole fg, InvoThemeRole bg)
        {
            Image circleBg = Box(parent, "Mark", bg, InvoThemeShape.Circle);
            Size(circleBg.rectTransform, 72, 72);
            Image glyph = Icon(circleBg.rectTransform, "Icon", icon, fg, 34);
            Ignore(glyph.rectTransform);
            glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glyph.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void Notice(RectTransform parent, Sprite icon, string text, InvoThemeRole iconRole)
        {
            Image box = Box(parent, "Notice", InvoThemeRole.Field, InvoThemeShape.RoundedSmall);
            HStack(box.rectTransform, 10, 14, 12, 14, 12, TextAnchor.UpperLeft);
            var fit = box.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Icon(box.rectTransform, "Icon", icon, iconRole, 18);
            TMP_Text t = Text(box.rectTransform, "Text", text, InvoTextStyle.Caption, InvoThemeRole.TextSecondary);
            Fit(t);
            Flex(t.rectTransform, 1, 0);
        }

        private static RectTransform Card(RectTransform parent, string name)
        {
            Image card = Box(parent, name, InvoThemeRole.Field, InvoThemeShape.Rounded);
            VStack(card.rectTransform, 0, 0, 0, 0, 0);
            Border(card.rectTransform, InvoThemeRole.Border);
            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card.rectTransform;
        }

        private static TMP_Text SummaryRow(RectTransform card, string label, bool last = false, bool strong = false)
        {
            RectTransform row = Node(card, label.Replace(" ", "") + "Row");
            HStack(row, 8, 16, 12, 16, 12, TextAnchor.MiddleLeft);
            Size(row, -1, 46);
            Fit(Text(row, "Label", label, strong ? InvoTextStyle.BodyStrong : InvoTextStyle.Caption, strong ? InvoThemeRole.TextPrimary : InvoThemeRole.TextSecondary));
            Spacer(row);
            TMP_Text value = Text(row, "Value", "-", strong ? InvoTextStyle.Heading : InvoTextStyle.BodyStrong, strong ? InvoThemeRole.Success : InvoThemeRole.TextPrimary, TextAlignmentOptions.MidlineRight);
            Fit(value);
            if (!last)
            {
                Image divider = Box(card, "Divider", InvoThemeRole.Border, InvoThemeShape.Rectangle);
                Size(divider.rectTransform, -1, 1);
            }
            return value;
        }

        private static TMP_Text StatusLine(RectTransform panel)
        {
            TMP_Text t = Text(panel, "Status", "", InvoTextStyle.Caption, InvoThemeRole.Warning, TextAlignmentOptions.Center);
            RectTransform rt = t.rectTransform;
            Ignore(rt);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 40);
            rt.anchoredPosition = new Vector2(0, 60);
            return t;
        }

        private static void BuildWatermark(RectTransform content)
        {
            Image pill = Box(content, "SandboxWatermark", InvoThemeRole.Warning, InvoThemeShape.Pill);
            pill.GetComponent<InvoThemed>().alpha = 0.9f;
            RectTransform rt = pill.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(20, 20);
            HStack(rt, 0, 12, 6, 12, 6, TextAnchor.MiddleCenter);
            var fit = pill.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text t = Text(rt, "Label", "SANDBOX", InvoTextStyle.Overline, InvoThemeRole.None);
            t.color = new Color32(30, 20, 5, 255);
            Fit(t);
            var wm = pill.gameObject.AddComponent<InvoSandboxWatermark>();
            wm.label = pill.gameObject;
            wm.labelText = t;
        }

        private static void BuildEnrollmentPrompt(Transform root)
        {
            var prompt = root.gameObject.AddComponent<InvoEnrollmentPromptPanel>();
            Image dim = Box(root, "EnrollmentPrompt", InvoThemeRole.Backdrop, InvoThemeShape.Rectangle);
            Stretch(dim.rectTransform);
            prompt.dialog = dim.gameObject;
            dim.gameObject.AddComponent<InvoGamepadFocus>();
            Image card = Box(dim.rectTransform, "Card", InvoThemeRole.Surface, InvoThemeShape.Rounded);
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            card.rectTransform.sizeDelta = new Vector2(460, 300);
            Border(card.rectTransform, InvoThemeRole.Border);
            VStack(card.rectTransform, 16, 28, 28, 28, 28, TextAnchor.UpperCenter);
            IconMark(card.rectTransform, iconPhone, InvoThemeRole.Link, InvoThemeRole.AccentSoft);
            prompt.messageText = Text(card.rectTransform, "Message", "Set up INVO on this phone? Code 1234. Say Yes only if the phone you just opened shows this code.",
                InvoTextStyle.Body, InvoThemeRole.TextPrimary, TextAlignmentOptions.Center);
            Fit(prompt.messageText);
            Spacer(card.rectTransform);
            RectTransform buttons = Node(card.rectTransform, "Buttons");
            HStack(buttons, 10, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            Size(buttons, -1, 50);
            prompt.secondaryButton = SecondaryButton(buttons, "No", "No, it's different", -1, 50);
            Flex((RectTransform)prompt.secondaryButton.transform, 1, 0);
            prompt.secondaryLabel = prompt.secondaryButton.GetComponentInChildren<TMP_Text>(true);
            prompt.primaryButton = PrimaryButton(buttons, "Yes", "Yes, it matches", -1, 50);
            Flex((RectTransform)prompt.primaryButton.transform, 1, 0);
            prompt.primaryLabel = prompt.primaryButton.GetComponentInChildren<TMP_Text>(true);
            dim.gameObject.SetActive(false);
        }

        // ================================================================== primitives

        private static RectTransform PanelRoot(RectTransform parent, string name)
        {
            RectTransform rt = Node(parent, name);
            Stretch(rt);
            Ignore(rt);
            rt.gameObject.AddComponent<InvoGamepadFocus>();
            return rt;
        }

        private static RectTransform Step(RectTransform panel, string name)
        {
            RectTransform rt = Node(panel, name);
            Stretch(rt);
            VStack(rt, 14, 0, 0, 0, 0);
            rt.gameObject.AddComponent<InvoGamepadFocus>();
            return rt;
        }

        private static void HideAllSteps(params RectTransform[] steps)
        {
            foreach (RectTransform s in steps) s.gameObject.SetActive(false);
        }

        private static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image Box(Transform parent, string name, InvoThemeRole role, InvoThemeShape shape)
        {
            RectTransform rt = Node(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            var themed = rt.gameObject.AddComponent<InvoThemed>();
            themed.role = role;
            themed.shape = shape;
            themed.Apply(theme);
            return image;
        }

        private static Image Icon(Transform parent, string name, Sprite sprite, InvoThemeRole role, float size)
        {
            Image img = Box(parent, name, role, InvoThemeShape.Keep);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Size(img.rectTransform, size, size);
            return img;
        }

        private static void Border(RectTransform parent, InvoThemeRole role)
        {
            bool small = parent.GetComponent<InvoThemed>()?.shape == InvoThemeShape.RoundedSmall;
            Image b = Box(parent, "Border", role, small ? InvoThemeShape.RoundedSmallOutline : InvoThemeShape.RoundedOutline);
            Stretch(b.rectTransform);
            Ignore(b.rectTransform);
            b.raycastTarget = false;
        }

        private static TMP_Text Text(Transform parent, string name, string value, InvoTextStyle style, InvoThemeRole role,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            RectTransform rt = Node(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = value;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            var themed = rt.gameObject.AddComponent<InvoThemed>();
            themed.role = role;
            themed.textStyle = style;
            themed.Apply(theme);
            return t;
        }

        private static Button PrimaryButton(Transform parent, string name, string label, float w, float h) =>
            MakeButton(parent, name, label, w, h, InvoThemeRole.Accent, InvoThemeRole.OnAccent, false);

        private static Button SecondaryButton(Transform parent, string name, string label, float w, float h) =>
            MakeButton(parent, name, label, w, h, InvoThemeRole.Field, InvoThemeRole.TextPrimary, true);

        private static Button MakeButton(Transform parent, string name, string label, float w, float h,
            InvoThemeRole bgRole, InvoThemeRole textRole, bool bordered)
        {
            Image bg = Box(parent, name, bgRole, InvoThemeShape.RoundedSmall);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            Size(bg.rectTransform, w, h);
            if (bordered) Border(bg.rectTransform, InvoThemeRole.Border);
            TMP_Text t = Text(bg.rectTransform, "Label", label, InvoTextStyle.BodyStrong, textRole, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            t.enableWordWrapping = false;
            return button;
        }

        private static Button LinkButton(Transform parent, string name, string label)
        {
            RectTransform rt = Node(parent, name);
            TMP_Text t = Text(rt, "Label", label, InvoTextStyle.Caption, InvoThemeRole.Link);
            t.fontStyle = FontStyles.Bold;
            t.raycastTarget = true;
            Stretch(t.rectTransform);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = t;
            Size(rt, 64, 24);
            return button;
        }

        private static Button IconButton(Transform parent, string name, Sprite icon, string label)
        {
            Image bg = Box(parent, name, InvoThemeRole.Surface, InvoThemeShape.RoundedSmall);
            Size(bg.rectTransform, 40, 40);
            Border(bg.rectTransform, InvoThemeRole.Border);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            Image glyph = Icon(bg.rectTransform, "Icon", icon, InvoThemeRole.TextSecondary, 18);
            glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            Ignore(glyph.rectTransform);
            return button;
        }

        private static void BindPanel(Button button, string panelId)
        {
            var binder = button.gameObject.AddComponent<InvoUIButtonBinder>();
            binder.panelId = panelId;
            binder.windowManager = windowManager;
        }

        private static ScrollRect Scroll(RectTransform parent, string name, out RectTransform content)
        {
            RectTransform rt = Node(parent, name);
            var scroll = rt.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Node(rt, "Viewport");
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            VStack(content, 8, 0, 0, 0, 0);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        private static void DestroyVertical(RectTransform rt)
        {
            var v = rt.GetComponent<VerticalLayoutGroup>();
            if (v != null) UnityEngine.Object.DestroyImmediate(v);
        }

        private static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int left, int top, int right, int bottom,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(left, right, top, bottom);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        private static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, int left, int top, int right, int bottom,
            TextAnchor align)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(left, right, top, bottom);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        /// <summary>Fixed preferred size; -1 leaves that axis to the layout.</summary>
        private static void Size(RectTransform rt, float w, float h)
        {
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            rt.sizeDelta = new Vector2(w >= 0 ? w : rt.sizeDelta.x, h >= 0 ? h : rt.sizeDelta.y);
        }

        private static void Flex(RectTransform rt, float w, float h)
        {
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = w;
            le.flexibleHeight = h;
        }

        private static void Fit(TMP_Text t)
        {
            // Text in a layout group sizes from its preferred size; nothing to add.
        }

        /// <summary>
        /// Takes the free space along its parent's axis only: a spacer that also grew across the axis
        /// made a header row claim half the sheet's height.
        /// </summary>
        private static void Spacer(RectTransform parent, float height = -1)
        {
            RectTransform s = Node(parent, "Spacer");
            var le = s.gameObject.AddComponent<LayoutElement>();
            bool horizontal = parent.GetComponent<HorizontalLayoutGroup>() != null;
            if (height >= 0) { le.minHeight = height; le.preferredHeight = height; }
            else if (horizontal) le.flexibleWidth = 1;
            else le.flexibleHeight = 1;
        }

        private static void Ignore(RectTransform rt)
        {
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyThemeTree(GameObject go)
        {
            foreach (InvoThemed t in go.GetComponentsInChildren<InvoThemed>(true))
                t.Apply(theme);
        }

        private static void SavePrefab(GameObject go, string name)
        {
            string path = $"{Pkg}/Runtime/Prefabs/{name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction, out bool ok);
            if (!ok) throw new InvalidOperationException("Could not save " + path);
            Debug.Log("[InvoUiKit] Saved " + path);
        }

        private static void EnsureFolder(string assetPath)
        {
            Directory.CreateDirectory(FileUtil.GetPhysicalPath(assetPath));
            AssetDatabase.Refresh();
        }
    }
}
