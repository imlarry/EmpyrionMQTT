using System;
using System.Threading.Tasks;
using ESB.Messaging;

namespace ESB
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
                await _ctx.Messenger.SendAsync(
                    MessageClass.Exception,
                    GetType().Name,
                    ex.ToString());
            }
        }

        protected Task EmitEmpEventAsync(string scope, string eventName, string payload)
        {
            string type   = _ctx.BusManager.ParticipantType;
            string connId = _ctx.Messenger.ClientId();
            return _ctx.Messenger.SendAsync($"EMP/{type}/{connId}/{scope}/Evt/{eventName}", payload);
        }
    }
}
