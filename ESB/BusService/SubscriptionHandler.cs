using ESB.TopicHandlers;
using ESB.Common;
using System.Threading.Tasks;

namespace ESB
{
    public class SubscriptionHandler
    {
        readonly private ContextData _cntxt;

        public SubscriptionHandler(ContextData context)
        {
            this._cntxt = context;
        }

        public async Task SubscribeAll()
        {
            new Application(_cntxt).Register();
            new Playfield(_cntxt).Register();
            new Gui(_cntxt).Register();
            new Player(_cntxt).Register();
            new Structure(_cntxt).Register();
            new Block(_cntxt).Register();
            new Lcd(_cntxt).Register();
            new Container(_cntxt).Register();
            new Light(_cntxt).Register();
            new Teleporter(_cntxt).Register();
            new Pda(_cntxt).Register();

            await _cntxt.Messenger.SubscribeRequestsAsync();
        }
    }
}