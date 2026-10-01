// Compiled only when the Unity Test Framework is installed (it defines UNITY_INCLUDE_TESTS);
// customer projects without it must not get a compile error from the SDK.
#if UNITY_INCLUDE_TESTS
using System.Text;
using NUnit.Framework;

namespace InvoSDK.Tests
{
    /// <summary>
    /// EditMode tests for the QR device approval: the QR encoder and the pure decision rules.
    /// The network stages need a live sandbox and a phone.
    /// </summary>
    public class InvoDeviceApprovalTests
    {
        // Produced by an independent encoder (Kazuhiko Arase's qrcode-generator 1.5.2, MIT) for the
        // same text, error correction M, mask 3. A drift here means scanners may read a different URL.
        private static readonly string[] KnownAnswerRows =
        {
            "#######.#.##...##.###.#######",
            "#.....#.#....#...##.#.#.....#",
            "#.###.#....###....###.#.###.#",
            "#.###.#.#.##.#.#.#....#.###.#",
            "#.###.#....#.##.####..#.###.#",
            "#.....#..###.##..####.#.....#",
            "#######.#.#.#.#.#.#.#.#######",
            "........##.##..#.#.#.........",
            "#.##.###.#...##.###...#..#.##",
            "..#.#..#...##..#####.##.#...#",
            ".###..##..##.#...#...##.#.##.",
            "#.##.#.#.##.#####.#....##...#",
            "....###.#.....##.#.#.....##..",
            "###.#...#..####.##.#..#...###",
            "#####.#.##.###...#.#.#..#.###",
            "####......#.#..##..#.####..#.",
            "#..#.####.##...##.#..#..##.#.",
            ".#......#..#...##...#..#.###.",
            "#.....#....###.##...#.....#..",
            "..##.....#....#.####.#..#.#..",
            ".#.#..##.######.##..#######..",
            "........#.#..#..##..#...#####",
            "#######.###..##...###.#.##.#.",
            "#.....#.###.###...#.#...##...",
            "#.###.#...#.#..#.#..#####.###",
            "#.###.#.#.....####.#.#.###..#",
            "#.###.#.###.###..#...#.#..#.#",
            "#.....#.........#..##..###.#.",
            "#######.#.#..#.#..###..##..#.",
        };

        private const string KnownAnswerText = "https://invo.network/device?code=BCDF-GHJK";

        // ---------------- QR encoder ----------------

        [Test]
        public void Qr_MatchesIndependentEncoder()
        {
            var qr = InvoQrCode.EncodeBytes(Encoding.UTF8.GetBytes(KnownAnswerText), InvoQrEcc.Medium, 3);
            Assert.AreEqual(3, qr.Version);
            Assert.AreEqual(KnownAnswerRows.Length, qr.Size);
            for (int y = 0; y < qr.Size; y++)
            {
                var row = new StringBuilder();
                for (int x = 0; x < qr.Size; x++)
                    row.Append(qr.IsDark(x, y) ? '#' : '.');
                Assert.AreEqual(KnownAnswerRows[y], row.ToString(), "row " + y);
            }
        }

        [Test]
        public void Qr_PicksSmallestVersionThatFits()
        {
            Assert.AreEqual(1, InvoQrCode.EncodeText("A", InvoQrEcc.Medium).Version);
            // 14 bytes is version 1-M's byte-mode capacity; 15 needs version 2.
            Assert.AreEqual(1, InvoQrCode.EncodeText(new string('a', 14), InvoQrEcc.Medium).Version);
            Assert.AreEqual(2, InvoQrCode.EncodeText(new string('a', 15), InvoQrEcc.Medium).Version);
        }

        [Test]
        public void Qr_AutoMaskIsAValidMask_AndOutsideReadsLight()
        {
            var qr = InvoQrCode.EncodeText(KnownAnswerText);
            Assert.That(qr.Mask, Is.InRange(0, 7));
            Assert.AreEqual(qr.Version * 4 + 17, qr.Size);
            Assert.IsFalse(qr.IsDark(-1, 0));
            Assert.IsFalse(qr.IsDark(0, qr.Size));
            // Finder centres and the always-dark module.
            Assert.IsTrue(qr.IsDark(3, 3));
            Assert.IsTrue(qr.IsDark(qr.Size - 4, 3));
            Assert.IsTrue(qr.IsDark(3, qr.Size - 4));
            Assert.IsTrue(qr.IsDark(8, qr.Size - 8));
        }

        [Test]
        public void Qr_RejectsOversizedData()
        {
            Assert.Throws<System.ArgumentException>(() =>
                InvoQrCode.EncodeBytes(new byte[3000], InvoQrEcc.High, -1));
        }

        // ---------------- Settled-status allow-list (money question: fail closed) ----------------

        [TestCase(InvoApprovalFlow.Transfer, "pending_claim", true)]
        [TestCase(InvoApprovalFlow.Send, "verified", true)]
        [TestCase(InvoApprovalFlow.Send, "completed", true)]
        [TestCase(InvoApprovalFlow.Send, "pending_pin_verification", false)]
        [TestCase(InvoApprovalFlow.Transfer, "expired", false)]
        [TestCase(InvoApprovalFlow.SendReceipt, "completed", true)]
        [TestCase(InvoApprovalFlow.SendReceipt, "pending_claim", false)]
        [TestCase(InvoApprovalFlow.TransferReceipt, "pending_pin_verification", false)]
        [TestCase(InvoApprovalFlow.Send, "some_future_status", false)]
        [TestCase(InvoApprovalFlow.Send, "", false)]
        [TestCase(InvoApprovalFlow.Send, null, false)]
        [TestCase("unknown_flow", "completed", false)]
        public void IsPastStep(string flow, string status, bool expected)
        {
            Assert.AreEqual(expected, InvoDeviceApprovalCore.IsPastStep(flow, status));
        }

        [Test]
        public void IsPastStep_IgnoresCaseAndWhitespace()
        {
            Assert.IsTrue(InvoDeviceApprovalCore.IsPastStep(InvoApprovalFlow.Send, "  Completed "));
        }

        // ---------------- Poll pacing (RFC 8628 section 3.5) ----------------

        [Test]
        public void Interval_DefaultsAndSlowDown()
        {
            Assert.AreEqual(5, InvoDeviceApprovalCore.InitialInterval(0));
            Assert.AreEqual(7, InvoDeviceApprovalCore.InitialInterval(7));
            Assert.AreEqual(9, InvoDeviceApprovalCore.IntervalAfterPending(5, 9));
            Assert.AreEqual(5, InvoDeviceApprovalCore.IntervalAfterPending(5, 0));
            Assert.AreEqual(10, InvoDeviceApprovalCore.IntervalAfterSlowDown(5, 0));
            Assert.AreEqual(20, InvoDeviceApprovalCore.IntervalAfterSlowDown(5, 20));
        }

        [Test]
        public void Channel_PhonesUseTheBrowser_EverythingElseTheQr()
        {
            Assert.AreEqual(InvoApprovalChannel.AppBrowser, InvoDeviceApprovalCore.DefaultChannel(true));
            Assert.AreEqual(InvoApprovalChannel.Qr, InvoDeviceApprovalCore.DefaultChannel(false));
        }

        [TestCase(0, true)]
        [TestCase(429, true)]
        [TestCase(503, true)]
        [TestCase(400, false)]
        [TestCase(401, false)]
        [TestCase(403, false)]
        public void TransientPollFailures(long status, bool expected)
        {
            Assert.AreEqual(expected, InvoDeviceApprovalCore.IsTransientPollFailure(status));
        }

        [Test]
        public void MootEnrollmentAnswers()
        {
            Assert.IsTrue(InvoDeviceApprovalCore.IsMootEnrollmentAnswer("DEVICE_APPROVAL_ENROLLMENT_ALREADY_DECIDED"));
            Assert.IsTrue(InvoDeviceApprovalCore.IsMootEnrollmentAnswer("expired_token"));
            Assert.IsFalse(InvoDeviceApprovalCore.IsMootEnrollmentAnswer("SDK_TOKEN_EXPIRED"));
            Assert.IsFalse(InvoDeviceApprovalCore.IsMootEnrollmentAnswer(null));
        }

        // ---------------- Pending item routing ----------------

        [TestCase(PendingAction.KindIdentityGate, "send", InvoApprovalFlow.Send)]
        [TestCase(PendingAction.KindIdentityGate, "transfer", InvoApprovalFlow.Transfer)]
        [TestCase(PendingAction.KindReceivingConfirm, "send", InvoApprovalFlow.SendReceipt)]
        [TestCase(PendingAction.KindReceivingConfirm, "transfer", InvoApprovalFlow.TransferReceipt)]
        public void PendingAction_MapsToApprovalFlow(string kind, string flow, string expected)
        {
            Assert.AreEqual(expected, new PendingAction { kind = kind, flow = flow }.ApprovalFlow);
        }

        [Test]
        public void ManualEntryText()
        {
            Assert.AreEqual("Or go to https://invo.network/device and enter ABCD-EFGH",
                InvoDeviceApproval.ManualEntryText("https://invo.network/device", "ABCD-EFGH"));
            Assert.AreEqual("Code: ABCD-EFGH", InvoDeviceApproval.ManualEntryText(null, "ABCD-EFGH"));
            Assert.AreEqual(string.Empty, InvoDeviceApproval.ManualEntryText("https://x", null));
        }
    }
}
#endif
