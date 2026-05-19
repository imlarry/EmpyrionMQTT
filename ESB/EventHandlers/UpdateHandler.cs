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
                _ = _ctx.Messenger.SendAsync(_ctx.Messenger.MachineId(), "App", MessageType.Log, "Update", "Processing actions on main thread");
                while (_ctx.MainThreadRunner.HasActionsToProcess())
                {
                    _ctx.MainThreadRunner.ProcessActions();
                }
            }

            // Drain events that were enqueued before _ctx.IsReady flipped true (BusManager.Init)
            // or while _ctx.IsTransitioning was true (lobby->game swap). The latter ensures queued
            // events publish on the new ContextRcId once the swap is fully done.
            if (_ctx.IsReady && !_ctx.IsTransitioning)
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
                await _ctx.Messenger.SendAsync(_ctx.Messenger.MachineId(), "App", MessageType.Log, "UpdateHandler", ex.ToString());
            }
        }
    }
}
