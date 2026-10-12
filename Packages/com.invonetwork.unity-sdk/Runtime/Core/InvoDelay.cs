using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace InvoSDK
{
    /// <summary>
    /// A drop-in for <c>Task.Delay</c> that also works on WebGL.
    ///
    /// WebGL players have no threads, and Task.Delay's timer is not reliable there on every
    /// supported Unity version. In Play Mode this counts unscaled time on the main thread instead,
    /// driven by a hidden runner object, so polling keeps its timing even when Time.timeScale is 0.
    /// Outside Play Mode (editor windows, EditMode tests) it falls back to Task.Delay.
    /// Like Task.Delay, a cancelled token faults the task with <see cref="OperationCanceledException"/>.
    /// </summary>
    internal static class InvoDelay
    {
        public static Task Seconds(double seconds, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);
            if (!Application.isPlaying)
                return Task.Delay(TimeSpan.FromSeconds(Math.Max(0, seconds)), token);
            return InvoDelayRunner.Instance.Add(seconds, token);
        }
    }

    [AddComponentMenu("")]
    internal sealed class InvoDelayRunner : MonoBehaviour
    {
        private sealed class Pending
        {
            public float Due;
            public TaskCompletionSource<bool> Tcs;
            public CancellationToken Token;
        }

        private static InvoDelayRunner instance;
        private readonly List<Pending> pending = new List<Pending>();

        public static InvoDelayRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("InvoDelayRunner") { hideFlags = HideFlags.HideAndDontSave };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<InvoDelayRunner>();
                }
                return instance;
            }
        }

        public Task Add(double seconds, CancellationToken token)
        {
            var item = new Pending
            {
                Due = Time.unscaledTime + (float)Math.Max(0, seconds),
                Tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                Token = token
            };
            pending.Add(item);
            return item.Tcs.Task;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending item = pending[i];
                if (item.Token.IsCancellationRequested)
                    item.Tcs.TrySetCanceled(item.Token);
                else if (now >= item.Due)
                    item.Tcs.TrySetResult(true);
                else
                    continue;
                pending.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            // Leaving Play Mode or quitting: release anything still waiting so no task hangs.
            foreach (Pending item in pending)
                item.Tcs.TrySetCanceled();
            pending.Clear();
            if (instance == this)
                instance = null;
        }
    }
}
