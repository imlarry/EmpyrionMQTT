using ESB.Models;
using ESB.Interfaces;
using ESB.Messaging;

namespace ESB
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
                _ = _ctx.Messenger.SendAsync(MessageClass.Information, "Update", "Processing actions on main thread");
                while (_ctx.MainThreadRunner.HasActionsToProcess())
                {
                    _ctx.MainThreadRunner.ProcessActions();
                }
            }
        }
    }
}
