using ESB.TopicHandlers.V2;
using ESB.TopicHandlers.V1;
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
            new ESB.TopicHandlers.V2.Player(_cntxt).Register();
            new Structure(_cntxt).Register();
            new Block(_cntxt).Register();
            new Lcd(_cntxt).Register();
            new Container(_cntxt).Register();
            new Light(_cntxt).Register();
            new Teleporter(_cntxt).Register();
            new Pda(_cntxt).Register();
            new Utilities(_cntxt).Register();
            // V1 handlers — only reachable on DedicatedServer in multiplayer.
            // V1 (ModBase) is never initialized in SinglePlayer, so these handlers
            // will never receive a request in SP. Safe to register unconditionally;
            // they simply won't be reached outside of a multiplayer dedicated server.
            new ESB.TopicHandlers.V1.Player(_cntxt).Register();
            new ESB.TopicHandlers.V1.Server(_cntxt).Register();
            new ESB.TopicHandlers.V1.Message(_cntxt).Register();

            await _cntxt.Messenger.SubscribeRequestsAsync();
        }
    }
}