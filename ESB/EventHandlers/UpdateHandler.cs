using System;
using System.Threading.Tasks;
using ESB.Interfaces;
using ESB.Messaging;


namespace ESB.EventHandlers
{
    public class UpdateHandler : IUpdateHandler
    {
        private readonly ContextData _ctx;

        public UpdateHandler(ContextData context)
        {
            _ctx = context;
        }

        public void Handle()
        {
            if (_ctx.MainThreadRunner.HasActionsToProcess())
            {
                _ = _ctx.Messenger.SendAsync("App", MessageType.Log, "Update", "Processing actions on main thread");
                while (_ctx.MainThreadRunner.HasActionsToProcess())
                {
                    _ctx.MainThreadRunner.ProcessActions();
                }
            }

            if (_ctx.IsReady)
            {
                while (_ctx.EventQueue.Count > 0)
                {
                    var work = _ctx.EventQueue.Dequeue();
                    _ = DrainAsync(work);
                }
            }
        }

        private async Task DrainAsync(Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync("App", MessageType.Log, "UpdateHandler", ex.ToString());
            }
        }
    }
}
