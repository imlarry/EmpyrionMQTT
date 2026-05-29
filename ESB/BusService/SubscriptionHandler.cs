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

        public Task SubscribeAll()
        {
            new AppHandler(_ctx).Register();
            new PlayerHandler(_ctx).Register();
            new EntityHandler(_ctx).Register();
            new StructureHandler(_ctx).Register();
            new PlayfieldHandler(_ctx).Register();
            return Task.CompletedTask;
        }
    }
}
