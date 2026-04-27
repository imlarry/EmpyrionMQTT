using ESB.TopicHandlers;
using System.Threading.Tasks;

namespace ESB
{
    public class SubscriptionHandler
    {
        readonly private ContextData _ctx;

        public SubscriptionHandler(ContextData context)
        {
            this._ctx = context;
        }

        public async Task SubscribeAll()
        {
            new ApplicationHandler(_ctx).Register();
            new PlayerHandler(_ctx).Register();
            new StructureHandler(_ctx).Register();

            // Standard scope: ESB/{type}/{connId}/{scope}/Req/{op}
            await _ctx.Messenger.SubscribeBrokerAsync($"ESB/+/{_ctx.Messenger.ClientId()}/+/Req/#");
            // Device sub-scope: ESB/{type}/{connId}/Structure/Device/{name}/Req/{op}
            await _ctx.Messenger.SubscribeBrokerAsync($"ESB/+/{_ctx.Messenger.ClientId()}/Structure/Device/+/Req/#");
        }
    }
}
