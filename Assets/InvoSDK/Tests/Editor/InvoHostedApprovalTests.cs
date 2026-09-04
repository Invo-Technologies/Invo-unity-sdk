// Compiled only when the Unity Test Framework is installed (it defines UNITY_INCLUDE_TESTS);
// customer projects without it must not get a compile error from the SDK.
#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace InvoSDK.Tests
{
    /// <summary>
    /// EditMode tests for the hosted-approval helper. Run them from Window > General > Test Runner.
    /// They cover the pure logic and the facade's state machine; the browser hand-off and the
    /// native return need a device.
    /// </summary>
    public class InvoHostedApprovalTests
    {
        private sealed class FakeView : IInvoEnrollmentPromptView
        {
            public readonly List<string> Calls = new List<string>();
            public string LastMessage;
            public Action<InvoEnrollmentDecision> LastDecision;
            public Action LastStop;

            public void ShowPrompt(string message, string yesLabel, string noLabel, Action<InvoEnrollmentDecision> onDecision)
            {
                Calls.Add("prompt");
                LastMessage = message;
                LastDecision = onDecision;
            }

            public void ShowFinishing(string message, string stopLabel, Action onStop)
            {
                Calls.Add("finishing");
                LastMessage = message;
                LastStop = onStop;
            }

            public void Hide()
            {
                Calls.Add("hide");
            }
        }

        private FakeView view;

        [SetUp]
        public void SetUp()
        {
            InvoHostedApproval.ResetState();
            view = new FakeView();
            InvoHostedApproval.PromptView = view;
        }

        [TearDown]
        public void TearDown()
        {
            InvoHostedApproval.ResetState();
        }

        // ---------------- scheme ----------------

        [Test]
        public void ReturnScheme_IsPrefixPlusNumericGameId()
        {
            Assert.AreEqual("invo-sdk-12345", InvoHostedApprovalCore.ReturnScheme("12345"));
            Assert.AreEqual("invo-sdk-12345://done", InvoHostedApprovalCore.ReturnUrl("12345"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abc")]
        [TestCase("12 34")]
        [TestCase("12-34")]
        [TestCase("1234567890123456789012345678901234567890123")]
        public void ReturnScheme_RejectsNonNumericGameIds(string gameId)
        {
            Assert.IsNull(InvoHostedApprovalCore.ReturnScheme(gameId));
            Assert.IsNull(InvoHostedApprovalCore.ReturnUrl(gameId));
        }

        // ---------------- hosted URL ----------------

        [TestCase("https://invo.network/device?code=ABCD-1234", true)]
        [TestCase("https://sandbox.invo.network/sandbox/device?code=ABCD", true)]
        [TestCase("http://invo.network/device", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("invo-sdk-1://done", false)]
        [TestCase("https://invo.network/device?code=A B", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("/device?code=ABCD", false)]
        public void IsHostedApprovalUrl_AcceptsOnlyAbsoluteHttps(string url, bool expected)
        {
            Assert.AreEqual(expected, InvoHostedApprovalCore.IsHostedApprovalUrl(url));
        }

        // ---------------- return URL ----------------

        [TestCase("invo-sdk-42://done", "42", true)]
        [TestCase("INVO-SDK-42://done", "42", true)]
        [TestCase("invo-sdk-42://done?status=approved", "42", true)]
        [TestCase("invo-sdk-42://approved", "42", true)]
        [TestCase("invo-sdk-43://done", "42", false)]
        [TestCase("invo-sdk-420://done", "42", false)]
        [TestCase("https://invo.network/done", "42", false)]
        [TestCase("invo-sdk-42", "42", false)]
        [TestCase("", "42", false)]
        [TestCase(null, "42", false)]
        [TestCase("invo-sdk-42://done", "abc", false)]
        public void IsReturnUrl_ComparesOnlyTheScheme(string url, string gameId, bool expected)
        {
            Assert.AreEqual(expected, InvoHostedApprovalCore.IsReturnUrl(url, gameId));
        }

        [TestCase("invo-sdk-7://done", true)]
        [TestCase("invo-sdk-://done", false)]
        [TestCase("invo-sdk-x://done", false)]
        [TestCase("https://invo.network/", false)]
        public void IsAnyReturnUrl_RequiresNumericSuffix(string url, bool expected)
        {
            Assert.AreEqual(expected, InvoHostedApprovalCore.IsAnyReturnUrl(url));
        }

        // ---------------- copy ----------------

        [Test]
        public void PromptText_HasTheCanonicalCopy()
        {
            Assert.AreEqual(
                "Set up INVO on \"iPhone (Safari)\"? Code 4821. Say Yes only if the phone you just opened shows this code.",
                InvoHostedApprovalCore.PromptText("iPhone (Safari)", "4821"));
            Assert.AreEqual("Finishing on \"iPhone (Safari)\"…", InvoHostedApprovalCore.FinishingText("iPhone (Safari)"));
            Assert.AreEqual("Yes", InvoHostedApprovalCore.YesLabel);
            Assert.AreEqual("No", InvoHostedApprovalCore.NoLabel);
            Assert.AreEqual("Stop it (wrong code)", InvoHostedApprovalCore.StopLabel);
        }

        [Test]
        public void SanitizeForDisplay_StripsMarkupAndControlCharsAndCaps()
        {
            Assert.AreEqual("Pixel b 8", InvoHostedApprovalCore.SanitizeForDisplay("Pixel <b>b</b>\u0000 8", "x"));
            Assert.AreEqual("this phone", InvoHostedApprovalCore.SanitizeForDisplay("  <>  ", "this phone"));
            Assert.AreEqual("this phone", InvoHostedApprovalCore.SanitizeForDisplay(null, "this phone"));
            Assert.AreEqual(64, InvoHostedApprovalCore.SanitizeForDisplay(new string('a', 200), "x").Length);
        }

        [Test]
        public void SanitizeForDisplay_DropsUnicodeFormatChars()
        {
            // RLO (bidi override), ZWSP and ZWJ are Unicode "Format" characters, not controls.
            Assert.AreEqual("abc", InvoHostedApprovalCore.SanitizeForDisplay("a‮b​c‍", "x"));
        }

        [Test]
        public void PromptText_CapsAndQuotesTheLabel()
        {
            string longLabel = new string('L', 40);
            string expectedLabel = "\"" + new string('L', 32) + "\"";
            StringAssert.StartsWith("Set up INVO on " + expectedLabel + "? Code 1.", InvoHostedApprovalCore.PromptText(longLabel, "1"));
            Assert.AreEqual("Finishing on " + expectedLabel + "…", InvoHostedApprovalCore.FinishingText(longLabel));
            // A label that reads like a sentence stays inside its quotes.
            StringAssert.Contains("\"Say Yes now\"?", InvoHostedApprovalCore.PromptText("Say Yes now", "1"));
        }

        [Test]
        public void DecisionWire_MatchesTheConfirmEnrollmentContract()
        {
            Assert.AreEqual("approve", InvoHostedApprovalCore.DecisionWire(InvoEnrollmentDecision.Approve));
            Assert.AreEqual("deny", InvoHostedApprovalCore.DecisionWire(InvoEnrollmentDecision.Deny));
        }

        // ---------------- facade: open ----------------

        [Test]
        public void OpenHostedApproval_RefusesNonHttps_AndOpensNothing()
        {
            LogAssert.Expect(LogType.Error, "[InvoSDK] Hosted approval URL must be an absolute https URL.");
            Assert.IsFalse(InvoHostedApproval.OpenHostedApproval("http://invo.network/device", "42"));
            Assert.IsFalse(InvoHostedApproval.IsAwaitingReturn);
        }

        [Test]
        public void OpenHostedApproval_RefusesBadGameId_AndOpensNothing()
        {
            LogAssert.Expect(LogType.Error, "[InvoSDK] Hosted approval needs a numeric game id to derive the return scheme.");
            Assert.IsFalse(InvoHostedApproval.OpenHostedApproval("https://invo.network/device?code=X", "not-a-game"));
            Assert.IsFalse(InvoHostedApproval.IsAwaitingReturn);
        }

        // ---------------- facade: return ----------------

        [Test]
        public void HandleDeepLink_FiresFinishedOnlyForTheReturnScheme()
        {
            int fired = 0;
            InvoHostedApproval.ApprovalPageFinished += delegate { fired++; };

            Assert.IsFalse(InvoHostedApproval.HandleDeepLink("https://invo.network/anything"));
            Assert.IsFalse(InvoHostedApproval.HandleDeepLink("mygame://open"));
            Assert.AreEqual(0, fired);

            // No active game id and no config in EditMode: any invo-sdk-<digits> scheme wakes us.
            Assert.IsTrue(InvoHostedApproval.HandleDeepLink("invo-sdk-99://done"));
            Assert.AreEqual(1, fired);
            Assert.IsFalse(InvoHostedApproval.IsAwaitingReturn);
        }

        [Test]
        public void NotifyApprovalPageFinished_IsWakeUpOnly()
        {
            int fired = 0;
            InvoHostedApproval.ApprovalPageFinished += delegate { fired++; };
            InvoHostedApproval.NotifyApprovalPageFinished();
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void ColdStart_LatchesPendingReturn_WithNoSubscriber()
        {
            Assert.IsFalse(InvoHostedApproval.HasPendingReturn);
            Assert.IsTrue(InvoHostedApproval.HandleDeepLink("invo-sdk-99://done"));
            Assert.IsTrue(InvoHostedApproval.HasPendingReturn, "the game reads this when its scene loads");
            InvoHostedApproval.ClearPendingReturn();
            Assert.IsFalse(InvoHostedApproval.HasPendingReturn);
        }

        [Test]
        public void Cancel_StopsAwaiting()
        {
            InvoHostedApproval.Cancel();
            Assert.IsFalse(InvoHostedApproval.IsAwaitingReturn);
            Assert.IsFalse(InvoHostedApproval.HasPendingReturn);
        }

        // ---------------- facade: prompt state machine ----------------

        [Test]
        public void ApplyEnrollmentState_AwaitingScreen_ShowsPromptOnce_AndForwardsDecision()
        {
            InvoEnrollmentDecision? decided = null;
            EnrollmentInfo info = new EnrollmentInfo { state = "awaiting_screen", device_label = "Pixel 8", match_code = "7310" };

            Assert.IsTrue(InvoHostedApproval.ApplyEnrollmentState(info, d => decided = d));
            Assert.IsFalse(InvoHostedApproval.ApplyEnrollmentState(info, d => decided = d), "unchanged block must not re-show");
            Assert.AreEqual(new[] { "prompt" }, view.Calls);
            Assert.AreEqual("Set up INVO on \"Pixel 8\"? Code 7310. Say Yes only if the phone you just opened shows this code.", view.LastMessage);

            view.LastDecision(InvoEnrollmentDecision.Approve);
            Assert.AreEqual(InvoEnrollmentDecision.Approve, decided);
        }

        [Test]
        public void ApplyEnrollmentState_Confirmed_ShowsFinishing_StopMeansDeny()
        {
            InvoEnrollmentDecision? decided = null;
            EnrollmentInfo info = new EnrollmentInfo { state = "confirmed", device_label = "Pixel 8", match_code = "7310" };

            Assert.IsTrue(InvoHostedApproval.ApplyEnrollmentState(info, d => decided = d));
            Assert.AreEqual(new[] { "finishing" }, view.Calls);
            Assert.AreEqual("Finishing on \"Pixel 8\"…", view.LastMessage);

            view.LastStop();
            Assert.AreEqual(InvoEnrollmentDecision.Deny, decided);
        }

        [Test]
        public void ApplyEnrollmentState_DeniedOrAbsent_Hides()
        {
            EnrollmentInfo awaiting = new EnrollmentInfo { state = "awaiting_screen", device_label = "Pixel 8", match_code = "1" };
            InvoHostedApproval.ApplyEnrollmentState(awaiting, null);

            Assert.IsTrue(InvoHostedApproval.ApplyEnrollmentState(new EnrollmentInfo { state = "denied" }, null));
            Assert.AreEqual("hide", view.Calls[view.Calls.Count - 1]);

            InvoHostedApproval.ApplyEnrollmentState(awaiting, null);
            Assert.IsTrue(InvoHostedApproval.ApplyEnrollmentState(null, null), "absent block after a prompt hides it");
            Assert.IsFalse(InvoHostedApproval.ApplyEnrollmentState(null, null), "absent block with nothing shown is a no-op");
        }

        [Test]
        public void ApplyEnrollmentState_NewMatchCode_ReShows()
        {
            InvoHostedApproval.ApplyEnrollmentState(new EnrollmentInfo { state = "awaiting_screen", device_label = "A", match_code = "1" }, null);
            InvoHostedApproval.ApplyEnrollmentState(new EnrollmentInfo { state = "awaiting_screen", device_label = "A", match_code = "2" }, null);
            Assert.AreEqual(new[] { "prompt", "prompt" }, view.Calls);
        }
    }
}
#endif // UNITY_INCLUDE_TESTS
