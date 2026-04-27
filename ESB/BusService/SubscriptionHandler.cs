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

            // EMP/{type}/{connId}/Req/{scope}/{op}
            await _ctx.Messenger.SubscribeBrokerAsync($"EMP/+/{_ctx.Messenger.ClientId()}/Req/+/#");
        }
    }
}
