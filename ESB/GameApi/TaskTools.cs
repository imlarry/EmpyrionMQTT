using System;
using System.Threading;
using System.Threading.Tasks;

namespace ESB.GameApi
{
    internal static class TaskTools
    {
        /// <summary>
        /// Awaits a task with a timeout. If timeout.Ticks == 0 the call is fire-and-forget
        /// (returns default on expiry rather than throwing).
        /// </summary>
        public static async Task<TResult> For<TResult>(TimeSpan timeout, Task<TResult> task)
        {
            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    var delay = timeout.Ticks == 0 ? new TimeSpan(0, 0, 1) : timeout;
                    var completed = await Task.WhenAny(task, Task.Delay(delay, cts.Token));
                    if (completed == task)
                    {
                        cts.Cancel();
                        return await task; // propagate exceptions
                    }
                    if (timeout.Ticks == 0) return await Task.FromResult(default(TResult));
                    throw new TimeoutException("The operation has timed out.");
                }
                catch (TimeoutException)
                {
                    if (timeout.Ticks > 0) throw;
                    return await Task.FromResult(default(TResult));
                }
            }
        }
    }
}
