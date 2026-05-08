using ESB.Messaging;
using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public class RegistryHandler
    {
        private readonly ContextData _ctx;

        public RegistryHandler(ContextData ctx) { _ctx = ctx; }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("Registry/evt/BlockAndIdtemMapping", BlockAndIdtemMapping);
        }

        // BlockAndIdtemMapping ... applies a game-scoped retained ID->Name mapping from any Client.
        // Before game entry, buffers by gameId; applies immediately once GameIdentifier is known.
        private Task BlockAndIdtemMapping(MessageContext mc)
        {
            if (_ctx.GameManager != null)
                _ctx.GameManager.ApplyMappingFromJson(mc.Payload);
            return Task.CompletedTask;
        }
    }
}
