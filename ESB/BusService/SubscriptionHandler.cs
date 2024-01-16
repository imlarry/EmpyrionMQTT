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
            var app = new Application(_cntxt);
            await app.Subscribe();

            var playfield = new Playfield(_cntxt);
            await playfield.Subscribe();

            var gui = new Gui(_cntxt);
            await gui.Subscribe();
        }
    }
}