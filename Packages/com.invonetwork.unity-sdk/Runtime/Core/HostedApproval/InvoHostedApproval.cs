using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace InvoSDK
{
    /// <summary>
    /// Hosted approval on mobile: open INVO's hosted approval page in the SYSTEM browser and
    /// learn when it finishes, then show the match-code prompt for a first-time phone.
    ///
    /// Most games never call this class directly: <see cref="InvoDeviceApproval.RunAsync"/> runs
    /// begin, this browser hand-off, the poll and the settle itself with the player token (and
    /// draws a QR instead on desktop and consoles). The server-held variant below is still
    /// supported for games that keep <c>device_code</c> off the device.
    ///
    /// Server-held variant, two halves. The game SERVER calls
    /// <c>POST /api/sdk/approvals/device/begin {transaction_id, flow, "channel": "app_browser"}</c>,
    /// keeps <c>device_code</c> and polls; it hands the client only
    /// <see cref="HostedApprovalHandoff"/>. The client half is this class:
    /// <list type="number">
    /// <item><see cref="OpenHostedApproval(string)"/> opens <c>verification_uri_complete</c> in an
    /// authentication session on iOS (or Safari), Custom Tabs or the system browser on Android,
    /// and the default browser elsewhere. NEVER an embedded WebView: WebAuthn does not run in a
    /// WebView, so the passkey ceremony would fail before it started.</item>
    /// <item>When the page is done it navigates to <c>invo-sdk-&lt;game_id&gt;://done</c>. That URL
    /// carries NOTHING - approved, denied and expired all use the same bare URL - so
    /// <see cref="ApprovalPageFinished"/> means exactly "poll now", never "approved".</item>
    /// <item>While a first-time phone waits, the server's poll carries an <c>enrollment</c> block;
    /// feed it to <see cref="ApplyEnrollmentState"/> and forward the decision to your server,
    /// which posts <c>confirm-enrollment {device_code, decision}</c>.</item>
    /// </list>
    /// Money moves only when the game server calls the approve endpoint with <c>device_code</c>.
    /// Nothing in this class can settle anything.
    /// </summary>
    public static class InvoHostedApproval
    {
        /// <summary>Name of the hidden GameObject the iOS bridge targets with UnitySendMessage.</summary>
        public const string ListenerObjectName = "InvoHostedApproval";

        /// <summary>Wake-up only: the hosted page finished (or the player returned to the game).
        /// Poll your server now. Carries no status by design.</summary>
        public static event Action ApprovalPageFinished;

        /// <summary>iOS only: the player dismissed the authentication session before the page
        /// finished. Still poll; the phone may have completed on its own.</summary>
        public static event Action ApprovalPageDismissed;

        /// <summary>The prompt surface. Null means the IMGUI <see cref="InvoEnrollmentPromptOverlay"/>.</summary>
        public static IInvoEnrollmentPromptView PromptView { get; set; }

        private static bool awaitingReturn;
        private static bool hasPendingReturn;
        private static bool cancelling;
        private static string activeGameId;
        private static string lastAppliedEnrollmentKey;
        private static bool promptShowing;
        private static InvoHostedApprovalListener listener;

        /// <summary>True between <see cref="OpenHostedApproval(string)"/> and the return.</summary>
        public static bool IsAwaitingReturn { get { return awaitingReturn; } }

        /// <summary>
        /// Latched when the page finished (or the app came back) and not yet cleared. Covers the
        /// cold start: the game was not running when the browser returned, so no
        /// <see cref="ApprovalPageFinished"/> subscriber existed yet. Read it when your approval
        /// scene loads, poll, then <see cref="ClearPendingReturn"/>. Wake-up only.
        /// </summary>
        public static bool HasPendingReturn { get { return hasPendingReturn; } }

        public static void ClearPendingReturn()
        {
            hasPendingReturn = false;
        }

        /// <summary>Game id the current hosted approval was opened for, or null.</summary>
        public static string ActiveGameId { get { return activeGameId; } }

        // Creates the hidden listener at startup so a cold-start deep link is caught even before
        // any game code touches this class.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapListener()
        {
            EnsureListener();
        }

        // =========================
        // OPEN
        // =========================

        /// <summary>Opens the hosted page for the game configured in <see cref="InvoSDKConfig"/>.</summary>
        public static bool OpenHostedApproval(string url)
        {
            string gameId = APIManager.Instance != null ? APIManager.Instance.GameId : null;
            return OpenHostedApproval(url, gameId);
        }

        /// <summary>
        /// Opens <paramref name="url"/> (must be https) in the system browser session for
        /// <paramref name="gameId"/>. Returns false, and opens nothing, when either is invalid.
        /// </summary>
        public static bool OpenHostedApproval(string url, string gameId)
        {
            if (!InvoHostedApprovalCore.IsHostedApprovalUrl(url))
            {
                Debug.LogError("[InvoSDK] Hosted approval URL must be an absolute https URL.");
                return false;
            }

            string scheme = InvoHostedApprovalCore.ReturnScheme(gameId);
            if (scheme == null)
            {
                Debug.LogError("[InvoSDK] Hosted approval needs a numeric game id to derive the return scheme.");
                return false;
            }

            EnsureListener();
            activeGameId = gameId;
            awaitingReturn = true;
            hasPendingReturn = false;
            cancelling = false;

#if UNITY_IOS && !UNITY_EDITOR
            // ASWebAuthenticationSession: a top-level, system-owned browser sheet that WebAuthn
            // runs in, and that hands the custom-scheme return straight back to us.
            if (_InvoHostedApproval_OpenAuthSession(url, scheme, ListenerObjectName) == 1)
                return true;
            // Older iOS or the session refused to start: Safari, return via Info.plist scheme.
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Custom Tabs when androidx.browser is in the build; the tab is Chrome itself, so
            // WebAuthn works. Falls through to the system browser otherwise.
            if (TryOpenCustomTab(url))
                return true;
#endif
            Application.OpenURL(url);
            return true;
        }

        /// <summary>
        /// For games with their own deep-link plumbing: call this when your handler sees
        /// <c>invo-sdk-&lt;game_id&gt;://done</c>. Fires <see cref="ApprovalPageFinished"/>.
        /// </summary>
        public static void NotifyApprovalPageFinished()
        {
            awaitingReturn = false;
            hasPendingReturn = true;
            Action handler = ApprovalPageFinished;
            if (handler != null)
                handler();
        }

        /// <summary>
        /// Cancels an in-flight iOS authentication session and stops waiting for a return on
        /// every platform. Neither <see cref="ApprovalPageFinished"/> nor
        /// <see cref="ApprovalPageDismissed"/> fires for a session the game cancelled itself.
        /// </summary>
        public static void Cancel()
        {
            awaitingReturn = false;
#if UNITY_IOS && !UNITY_EDITOR
            cancelling = true;
            _InvoHostedApproval_CancelAuthSession();
#endif
        }

        // =========================
        // MATCH-CODE PROMPT
        // =========================

        /// <summary>
        /// "Set up INVO on &lt;label&gt;? Code &lt;code&gt;. Say Yes only if the phone you just opened
        /// shows this code." with Yes / No. Forward the decision to your server.
        /// </summary>
        public static void ShowEnrollmentPrompt(string deviceLabel, string matchCode, Action<InvoEnrollmentDecision> onDecision)
        {
            IInvoEnrollmentPromptView view = ResolveView();
            if (view == null)
                return;
            promptShowing = true;
            view.ShowPrompt(InvoHostedApprovalCore.PromptText(deviceLabel, matchCode),
                            InvoHostedApprovalCore.YesLabel, InvoHostedApprovalCore.NoLabel, onDecision);
        }

        /// <summary>
        /// "Finishing on &lt;label&gt;…" with a single "Stop it (wrong code)" button, for the
        /// <c>confirmed</c> state. Stop means deny: the server lets a deny override a pending confirm.
        /// </summary>
        public static void ShowEnrollmentFinishing(string deviceLabel, Action onStop)
        {
            IInvoEnrollmentPromptView view = ResolveView();
            if (view == null)
                return;
            promptShowing = true;
            view.ShowFinishing(InvoHostedApprovalCore.FinishingText(deviceLabel),
                               InvoHostedApprovalCore.StopLabel, onStop);
        }

        public static void HideEnrollmentPrompt()
        {
            lastAppliedEnrollmentKey = null;
            HideView();
        }

        private static void HideView()
        {
            promptShowing = false;
            IInvoEnrollmentPromptView view = PromptView ?? (listener != null ? listener.Overlay : null);
            if (view != null)
                view.Hide();
        }

        /// <summary>
        /// Drive the prompt straight from the poll: pass the <c>enrollment</c> block each time your
        /// server relays one (null when absent). Re-applying an unchanged block is a no-op, so
        /// calling this on every poll tick is safe. Returns true when the prompt changed.
        /// <paramref name="onDecision"/> receives Approve/Deny from the prompt and Deny from Stop.
        /// </summary>
        public static bool ApplyEnrollmentState(EnrollmentInfo enrollment, Action<InvoEnrollmentDecision> onDecision)
        {
            if (enrollment == null || string.IsNullOrEmpty(enrollment.state))
            {
                bool wasShowing = promptShowing;
                HideEnrollmentPrompt();
                return wasShowing;
            }

            string key = enrollment.state + "|" + enrollment.match_code + "|" + enrollment.device_label;
            if (key == lastAppliedEnrollmentKey)
                return false;
            lastAppliedEnrollmentKey = key;

            switch (enrollment.state)
            {
                case InvoEnrollmentState.AwaitingScreen:
                    ShowEnrollmentPrompt(enrollment.device_label, enrollment.match_code, onDecision);
                    return true;
                case InvoEnrollmentState.Confirmed:
                    ShowEnrollmentFinishing(enrollment.device_label, delegate
                    {
                        if (onDecision != null)
                            onDecision(InvoEnrollmentDecision.Deny);
                    });
                    return true;
                default:
                    // denied, or a state this SDK does not know: nothing to ask.
                    HideView();
                    return true;
            }
        }

        /// <summary>Clears static state. For tests and scene reloads.</summary>
        public static void ResetState()
        {
            awaitingReturn = false;
            hasPendingReturn = false;
            cancelling = false;
            activeGameId = null;
            lastAppliedEnrollmentKey = null;
            promptShowing = false;
            ApprovalPageFinished = null;
            ApprovalPageDismissed = null;
            PromptView = null;
        }

        // =========================
        // INTERNALS
        // =========================

        /// <summary>
        /// Handles a deep link. Only the scheme is inspected: a matching scheme means "the page
        /// finished, poll now" and nothing more. Anything else is ignored.
        /// </summary>
        public static bool HandleDeepLink(string url)
        {
            string gameId = activeGameId;
            if (gameId == null && APIManager.Instance != null)
                gameId = APIManager.Instance.GameId;

            bool matches = gameId != null
                ? InvoHostedApprovalCore.IsReturnUrl(url, gameId)
                : InvoHostedApprovalCore.IsAnyReturnUrl(url);
            if (!matches)
                return false;

            NotifyApprovalPageFinished();
            return true;
        }

        internal static void HandleNativeSessionFinished(string result)
        {
            if (cancelling)
            {
                // The game cancelled the session itself: nothing to wake up for.
                cancelling = false;
                awaitingReturn = false;
                return;
            }
            if (result == "cancelled")
            {
                awaitingReturn = false;
                Action dismissed = ApprovalPageDismissed;
                if (dismissed != null)
                    dismissed();
                // Fall through: the phone may have finished anyway, so a poll is still due.
            }
            NotifyApprovalPageFinished();
        }

        private static IInvoEnrollmentPromptView ResolveView()
        {
            if (PromptView != null)
                return PromptView;
            InvoHostedApprovalListener l = EnsureListener();
            return l != null ? l.Overlay : null;
        }

        private static InvoHostedApprovalListener EnsureListener()
        {
            if (listener != null)
                return listener;
            GameObject go = new GameObject(ListenerObjectName);
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(go);
            listener = go.AddComponent<InvoHostedApprovalListener>();
            return listener;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int _InvoHostedApproval_OpenAuthSession(string url, string scheme, string gameObjectName);

        [DllImport("__Internal")]
        private static extern void _InvoHostedApproval_CancelAuthSession();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool TryOpenCustomTab(string url)
        {
            AndroidJavaObject builder;
            try
            {
                // Throws when androidx.browser is not in the build: that is the signal to fall back.
                builder = new AndroidJavaObject("androidx.browser.customtabs.CustomTabsIntent$Builder");
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                using (builder)
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = builder.Call<AndroidJavaObject>("build"))
                using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
                using (AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", url))
                {
                    intent.Call("launchUrl", activity, uri);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[InvoSDK] Custom Tabs unavailable, using the system browser: " + e.GetType().Name);
                return false;
            }
        }
#endif
    }

    /// <summary>
    /// Hidden receiver for <c>Application.deepLinkActivated</c>, the cold-start
    /// <c>Application.absoluteURL</c>, and the iOS bridge's UnitySendMessage. Created on demand.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class InvoHostedApprovalListener : MonoBehaviour
    {
        private InvoEnrollmentPromptOverlay overlay;

        internal InvoEnrollmentPromptOverlay Overlay
        {
            get
            {
                if (overlay == null)
                    overlay = gameObject.AddComponent<InvoEnrollmentPromptOverlay>();
                return overlay;
            }
        }

        private void Awake()
        {
            Application.deepLinkActivated += OnDeepLink;
            // Cold start: the game was not running when the browser returned.
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnDeepLink(Application.absoluteURL);
        }

        private void OnDestroy()
        {
            Application.deepLinkActivated -= OnDeepLink;
        }

        // Foreground fallback. A back-press out of a Custom Tab, a browser that refuses the
        // custom-scheme navigation, or another app that owns the scheme would otherwise leave
        // the game waiting forever. Coming back to the foreground while awaiting a return is
        // treated as the return: wake-up only, the poll is still the truth.
        private void OnApplicationPause(bool paused)
        {
            if (!paused && InvoHostedApproval.IsAwaitingReturn)
                InvoHostedApproval.NotifyApprovalPageFinished();
        }

        private void OnDeepLink(string url)
        {
            InvoHostedApproval.HandleDeepLink(url);
        }

        // UnitySendMessage target for the iOS authentication session ("done" | "cancelled").
        // The payload is only which way the sheet closed; it says nothing about the approval.
        private void OnNativeSessionFinished(string result)
        {
            InvoHostedApproval.HandleNativeSessionFinished(result);
        }
    }
}
