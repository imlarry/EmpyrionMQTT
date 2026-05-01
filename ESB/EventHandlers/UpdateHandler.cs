using ESB.Interfaces;


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
                _ = _ctx.Messenger.SendAsync($"ESB/{_ctx.BusManager.ParticipantType}/{_ctx.Messenger.ClientId()}/Log/App/Update", "Processing actions on main thread");
                while (_ctx.MainThreadRunner.HasActionsToProcess())
                {
                    _ctx.MainThreadRunner.ProcessActions();
                }
            }
        }
    }
}
