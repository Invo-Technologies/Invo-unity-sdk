using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace InvoSDK
{
    /// <summary>
    /// Awaits a <see cref="UnityWebRequest"/> on every supported Unity version.
    ///
    /// <c>await req.SendWebRequest()</c> only compiles on Unity 2023.1 and newer, where
    /// AsyncOperation became awaitable. This helper uses the operation's <c>completed</c> event
    /// instead, so the same code runs on 2021.3 and 2022.3. Do not replace it with a GetAwaiter
    /// extension: that would clash with Unity 6's built-in one.
    /// </summary>
    internal static class InvoWebRequest
    {
        /// <summary>
        /// Sends <paramref name="req"/> and completes when it finishes. Check <c>req.result</c>
        /// afterwards as usual. Cancelling aborts the request and cancels the task.
        /// </summary>
        public static Task SendAsync(UnityWebRequest req, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return Task.FromCanceled(ct);

            // RunContinuationsAsynchronously: the awaiting code (which usually disposes the request)
            // resumes on Unity's synchronization context, not inside the request's own callback.
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var op = req.SendWebRequest();

            CancellationTokenRegistration registration = default;
            if (ct.CanBeCanceled)
            {
                registration = ct.Register(() =>
                {
                    if (tcs.TrySetCanceled(ct) && !op.isDone)
                        req.Abort();
                });
            }

            if (op.isDone)
            {
                registration.Dispose();
                tcs.TrySetResult(true);
            }
            else
            {
                op.completed += _ =>
                {
                    registration.Dispose();
                    tcs.TrySetResult(true);
                };
            }

            return tcs.Task;
        }
    }
}
