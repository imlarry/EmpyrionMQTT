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

            // ESB/{requesterType}/{requesterId}/{scope}/req/{op}
            await _ctx.Messenger.SubscribeBrokerAsync(msgType: ESB.Messaging.MessageType.Req);

        }
    }
}
