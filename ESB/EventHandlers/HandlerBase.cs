using System;
using System.Threading.Tasks;

namespace ESB.EventHandlers
{
    public abstract class HandlerBase
    {
        protected readonly ContextData _ctx;

        protected HandlerBase(ContextData context)
        {
            _ctx = context;
        }

        protected async Task Execute(Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                string type   = _ctx.BusManager.ParticipantType;
                string connId = _ctx.Messenger.ClientId();
                await _ctx.Messenger.SendAsync(
                    $"ESB/{type}/{connId}/App/Err/{GetType().Name}",
                    ex.ToString());
            }
        }

        protected Task EmitEventAsync(string scope, string eventName, string payload)
        {
            string type   = _ctx.BusManager.ParticipantType;
            string connId = _ctx.Messenger.ClientId();
            return _ctx.Messenger.SendAsync($"ESB/{type}/{connId}/{scope}/Evt/{eventName}", payload);
        }
    }
}
