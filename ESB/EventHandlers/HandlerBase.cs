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

        protected Task Execute(Func<Task> work)
        {
            _ctx.EventQueue.Enqueue(work);
            return Task.CompletedTask;
        }
    }
}
