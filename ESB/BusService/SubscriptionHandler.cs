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
            new RegistryHandler(_ctx).Register();

            // ESB/{type}/{connId}/Req/{scope}/{op}
            await _ctx.Messenger.SubscribeBrokerAsync($"ESB/+/{_ctx.Messenger.ClientId()}/Req/+/#");

            // Game-scoped retained topics published by any Client under the Registry dir.
            if (_ctx.BusManager.ParticipantType != "Ds")
                await _ctx.Messenger.SubscribeBrokerAsync("ESB/Client/+/Registry/Evt/#");
        }
    }
}
