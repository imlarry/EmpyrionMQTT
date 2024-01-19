using ESB.Common;

namespace ESB.EventHandlers
{
    public class LegacyEventHandlerFactory
    {
        private readonly ContextData _contextData;

        public LegacyEventHandlerFactory(ContextData contextData)
        {
            _contextData = contextData;
        }

        public LegacyPlayfieldLoadedHandler CreateLegacyPlayfieldLoadedHandler()
        {
            return new LegacyPlayfieldLoadedHandler(_contextData);
        }

    }
}
