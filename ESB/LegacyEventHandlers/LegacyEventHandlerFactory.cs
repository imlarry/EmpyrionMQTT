using ESB.Models;

namespace ESB.LegacyEventHandlers
{
    public class LegacyEventHandlerFactory
    {
        private readonly ContextData _ctx;

        public LegacyEventHandlerFactory(ContextData context)
        {
            _ctx = context;
        }

        public LegacyPlayfieldLoadedHandler CreateLegacyPlayfieldLoadedHandler()
        {
            return new LegacyPlayfieldLoadedHandler(_ctx);
        }

    }
}
