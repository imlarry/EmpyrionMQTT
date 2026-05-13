using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ESB
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

        public Task<T> RunOnMainThread<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            _actions.Enqueue(() =>
            {
                try   { tcs.SetResult(func()); }
                catch (Exception ex) { tcs.SetException(ex); }
                return Task.CompletedTask;
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
