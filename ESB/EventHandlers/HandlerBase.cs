using System;
using System.Threading.Tasks;
using ESB.Messaging;

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
                await _ctx.Messenger.SendAsync("App", MessageType.Log, GetType().Name, ex.ToString());
            }
        }
    }
}
