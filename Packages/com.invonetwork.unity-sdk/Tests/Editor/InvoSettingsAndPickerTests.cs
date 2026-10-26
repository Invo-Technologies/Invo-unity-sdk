using System.Collections.Generic;
using System.Linq;
using InvoSDK.Editor;
using InvoSDK.UI;
using NUnit.Framework;
using UnityEngine;

namespace InvoSDK.Tests
{
    /// <summary>EditMode tests for the 3.1 settings (per-environment IDs, per-storefront keys, build check) and picker logic.</summary>
    public class InvoSettingsAndPickerTests
    {
        private InvoSDKConfig config;

        [SetUp]
        public void SetUp() => config = ScriptableObject.CreateInstance<InvoSDKConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        // ------------------------------------------------------------------ config

        [Test]
        public void GameId_UsesTheActiveEnvironment()
        {
            config.sandboxGameId = "11";
            config.productionGameId = "22";
            config.useProduction = false;
            Assert.AreEqual("11", config.ActiveGameId);
            config.useProduction = true;
            Assert.AreEqual("22", config.ActiveGameId);
        }

        [Test]
        public void GameId_FallsBackToThe30Field()
        {
            config.gameId = "7";
            Assert.AreEqual("7", config.ActiveGameId);
            config.useProduction = true;
            Assert.AreEqual("7", config.ActiveGameId);
        }

        [Test]
        public void SandboxKey_IsPerStorefront_AndFallsBackToThe30Key()
        {
            config.sdkKey = "ivsdk_legacy";
            config.sandboxKeys = new List<InvoStorefrontKey>
            {
                new InvoStorefrontKey { storefront = InvoStorefront.Steam, sdkKey = " ivsdk_steam " },
                new InvoStorefrontKey { storefront = InvoStorefront.GooglePlay, sdkKey = "" }
            };
            Assert.AreEqual("ivsdk_steam", config.SandboxKeyFor(InvoStorefront.Steam), "trimmed");
            Assert.AreEqual("ivsdk_legacy", config.SandboxKeyFor(InvoStorefront.GooglePlay), "blank key falls back");
            Assert.AreEqual("ivsdk_legacy", config.SandboxKeyFor(InvoStorefront.Xbox), "missing storefront falls back");
        }

        [Test]
        public void ActiveSandboxKey_IsAlwaysEmptyInProduction()
        {
            config.sdkKey = "ivsdk_legacy";
            config.sandboxKeys = new List<InvoStorefrontKey> { new InvoStorefrontKey { storefront = InvoStorefront.Steam, sdkKey = "ivsdk_steam" } };
            config.useProduction = true;
            Assert.AreEqual(string.Empty, config.ActiveSandboxKey);
        }

        [Test]
        public void StorefrontMap_DefaultsAndOverrides()
        {
            Assert.AreEqual(InvoStorefront.GooglePlay, config.ResolveStorefront(InvoBuildPlatform.Android));
            Assert.AreEqual(InvoStorefront.AppStore, config.ResolveStorefront(InvoBuildPlatform.iOS));
            Assert.AreEqual(InvoStorefront.Steam, config.ResolveStorefront(InvoBuildPlatform.Desktop));
            config.storefrontMap.First(m => m.platform == InvoBuildPlatform.Desktop).storefront = InvoStorefront.EpicGamesStore;
            Assert.AreEqual(InvoStorefront.EpicGamesStore, config.ResolveStorefront(InvoBuildPlatform.Desktop));
        }

        [Test]
        public void StorefrontWireNames_AreStable()
        {
            // Game servers branch on these strings; changing one is a breaking change.
            Assert.AreEqual("steam", InvoStorefrontResolver.WireName(InvoStorefront.Steam));
            Assert.AreEqual("google_play", InvoStorefrontResolver.WireName(InvoStorefront.GooglePlay));
            Assert.AreEqual("app_store", InvoStorefrontResolver.WireName(InvoStorefront.AppStore));
            Assert.AreEqual("playstation", InvoStorefrontResolver.WireName(InvoStorefront.PlayStation));
            Assert.AreEqual("xbox", InvoStorefrontResolver.WireName(InvoStorefront.Xbox));
            Assert.AreEqual("nintendo_switch", InvoStorefrontResolver.WireName(InvoStorefront.NintendoSwitch));
            Assert.AreEqual("epic_games_store", InvoStorefrontResolver.WireName(InvoStorefront.EpicGamesStore));
        }

        [Test]
        public void ProductionBuildCheck_NeedsGameIdAndHttpsServer()
        {
            config.useProduction = true;
            Assert.AreEqual(2, InvoBuildGuard.ProductionProblems(config).Count, "no game ID, no server");
            config.productionGameId = "22";
            config.gameServerUrl = "http://api.example.com";
            Assert.IsTrue(InvoBuildGuard.ProductionProblems(config).Single().Contains("https"));
            config.gameServerUrl = "https://api.example.com";
            Assert.IsEmpty(InvoBuildGuard.ProductionProblems(config));
        }

        // ------------------------------------------------------------------ picker and recents

        private static List<InvoGameOption> Games() => new List<InvoGameOption>
        {
            new InvoGameOption { id = "1", name = "Orbit Tactics", subtitle = "Strategy" },
            new InvoGameOption { id = "2", name = "Star Orbit", subtitle = "Shooter" },
            new InvoGameOption { id = "3", name = "Deep Crawl", subtitle = "RPG · Orbital DLC" },
            new InvoGameOption { id = "4", name = "Orbiter", subtitle = "Sim", available = false },
            new InvoGameOption { id = "5", name = "Fable Kart", subtitle = "Racing" },
        };

        [Test]
        public void Search_RanksPrefixThenWordThenSubtitle_UnavailableLast()
        {
            List<string> ids = InvoGameSearch.Filter(Games(), "orb").Select(g => g.id).ToList();
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4" }, ids);
        }

        [Test]
        public void Search_EmptyQueryListsEverything_AvailableFirst_ByName()
        {
            List<string> ids = InvoGameSearch.Filter(Games(), "  ").Select(g => g.id).ToList();
            CollectionAssert.AreEqual(new[] { "3", "5", "1", "2", "4" }, ids);
        }

        [Test]
        public void Search_NoMatch_IsEmpty() => Assert.IsEmpty(InvoGameSearch.Filter(Games(), "zzz"));

        [TestCase("Star Raiders", "SR")]
        [TestCase("Moonmine", "MO")]
        [TestCase("", "?")]
        public void Initials(string name, string expected) => Assert.AreEqual(expected, InvoGameSearch.Initials(name));

        [Test]
        public void Recents_NewestFirst_NoDuplicates_Capped()
        {
            var list = new List<string>();
            foreach (string phone in new[] { "+15555550101", "+15555550102", "+15555550103", "+15555550101",
                                             "+15555550104", "+15555550105", "+15555550106" })
                list = InvoRecentRecipientsStore.Merge(list, phone, 5);
            CollectionAssert.AreEqual(new[] { "+15555550106", "+15555550105", "+15555550104", "+15555550101", "+15555550103" }, list);
        }

        [Test]
        public void Recents_NormalizePhones() =>
            Assert.AreEqual("+15555550101", InvoRecentRecipientsStore.Merge(null, "+1 (555) 555-0101", 5).Single());

        [Test]
        public void Recents_ParseIgnoresBlanksAndDuplicates() =>
            CollectionAssert.AreEqual(new[] { "+15555550101", "+15555550102" },
                InvoRecentRecipientsStore.Parse("+15555550101||+15555550102|+15555550101"));

        [Test]
        public void MaskShort_ShowsOnlyTheLastFourDigits()
        {
            Assert.AreEqual("••• 0111", InvoPhone.MaskShort("+1 555 555 0111"));
            Assert.AreEqual("•••", InvoPhone.MaskShort("123"));
            Assert.AreEqual(string.Empty, InvoPhone.MaskShort(null));
        }

        // ------------------------------------------------------------------ key prefixes

        [Test]
        public void KeyStatus_AcceptsTestAndOlderKeys_AndFlagsLiveKeys()
        {
            Assert.AreEqual("Looks valid", InvoSDKSetupWizard.KeyStatus("ivsdk_test_abc"));
            Assert.AreEqual("Looks valid", InvoSDKSetupWizard.KeyStatus("ivsdk_abc"), "older keys keep working");
            Assert.AreEqual("Live key", InvoSDKSetupWizard.KeyStatus("ivsdk_live_abc"));
            Assert.AreEqual("Not ivsdk_", InvoSDKSetupWizard.KeyStatus("sk_abc"));
            Assert.AreEqual("Has spaces", InvoSDKSetupWizard.KeyStatus(" ivsdk_live_abc "));
            Assert.AreEqual("Not set", InvoSDKSetupWizard.KeyStatus(""));
        }

        [Test]
        public void TestConnection_ExplainsAKeyForTheOtherEnvironment()
        {
            const string body = "{\"error\":\"Unauthorized\",\"error_code\":\"KEY_FOR_OTHER_ENVIRONMENT\"}";
            Assert.AreEqual(InvoApiException.KeyForOtherEnvironmentMessage, InvoSDKSetupWizard.DescribeFailure(401, body));
            StringAssert.StartsWith("The key was refused", InvoSDKSetupWizard.DescribeFailure(401, "{\"error\":\"Unauthorized\"}"));
            StringAssert.StartsWith("The key was refused", InvoSDKSetupWizard.DescribeFailure(401, "<html>proxy</html>"));
        }

        [Test]
        public void ApiException_FlagsAKeyForTheOtherEnvironment()
        {
            Assert.IsTrue(new InvoApiException("m", 401, "KEY_FOR_OTHER_ENVIRONMENT", null, null, null).IsKeyForOtherEnvironment);
            Assert.IsFalse(new InvoApiException("m", 401, "UNAUTHORIZED", null, null, null).IsKeyForOtherEnvironment);
        }
    }
}
