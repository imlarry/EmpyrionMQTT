using ESB.Common;
using ESB.Interfaces;
using ESB.Messaging;

namespace ESB
{
    public class UpdateHandler : IUpdateHandler
    {
        private readonly ContextData _cntxt;

        public UpdateHandler(ContextData cntxt)
        {
            _cntxt = cntxt;
        }

        public void Handle()
        {
            if (_cntxt.MainThreadRunner.HasActionsToProcess())
            {
                _ = _cntxt.Messenger.SendAsync(MessageClass.Information, "Update", "Processing actions on main thread");
                while (_cntxt.MainThreadRunner.HasActionsToProcess())
                {
                    _cntxt.MainThreadRunner.ProcessActions();
                }
            }
        }
    }
}
