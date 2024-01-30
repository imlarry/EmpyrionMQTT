using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ESB.Common
{
    public class MainThreadRunner
    {
        private readonly ConcurrentQueue<Func<Task>> _actions = new ConcurrentQueue<Func<Task>>();
        public Task RunOnMainThread(Func<Task> func)
        {
            var tcs = new TaskCompletionSource<object>();
            _actions.Enqueue(async () =>
            {
                try
                {
                    await func();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public void ProcessActions()
        {
            while (_actions.TryDequeue(out var action))
            {
                action();
            }
        }
        public bool HasActionsToProcess()
        {
            return !_actions.IsEmpty;
        }
    }
}
