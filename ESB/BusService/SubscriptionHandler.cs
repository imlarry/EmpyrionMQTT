using ESB.TopicHandlers.V2;
using ESB.TopicHandlers.V1;
using ESB.Models;
using System.Threading.Tasks;

namespace ESB
{
    public class SubscriptionHandler : ISubscriptionHandler
    {
        readonly private ContextData _ctx;

        public SubscriptionHandler(ContextData context)
        {
            this._ctx = context;
        }

        public async Task SubscribeAll()
        {
            new Application(_ctx).Register();
            new ESB.TopicHandlers.V2.Playfield(_ctx).Register();
            new Gui(_ctx).Register();
            new ESB.TopicHandlers.V2.Player(_ctx).Register();
            new ESB.TopicHandlers.V2.Structure(_ctx).Register();
            new Block(_ctx).Register();
            new Lcd(_ctx).Register();
            new Container(_ctx).Register();
            new Light(_ctx).Register();
            new Teleporter(_ctx).Register();
            new Pda(_ctx).Register();
            new ESB.TopicHandlers.V2.Utilities(_ctx).Register();
            // V1 handlers — only reachable on DedicatedServer in multiplayer.
            // V1 (ModBase) is never initialized in SinglePlayer, so these handlers
            // will never receive a request in SP. Safe to register unconditionally;
            // they simply won't be reached outside of a multiplayer dedicated server.
            new ESB.TopicHandlers.V1.Player(_ctx).Register();
            new ESB.TopicHandlers.V1.Entity(_ctx).Register();
            new ESB.TopicHandlers.V1.Server(_ctx).Register();
            new ESB.TopicHandlers.V1.Message(_ctx).Register();
            new ESB.TopicHandlers.V1.Structure(_ctx).Register();
            new ESB.TopicHandlers.V1.Playfield(_ctx).Register();
            new ESB.TopicHandlers.V1.Faction(_ctx).Register();
            new ESB.TopicHandlers.V1.Blueprint(_ctx).Register();

            await _ctx.Messenger.SubscribeRequestsAsync();
        }
    }
}