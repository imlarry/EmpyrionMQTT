using ESB.Models;
using ESB.Interfaces;

namespace ESB
{
    public class LegacyEventManager
    {
        readonly private ContextData _ctx;
        readonly private ILegacyPlayfieldLoadedHandler _legacyPlayfieldLoadedHandler;

        public LegacyEventManager
            (ContextData context
            , ILegacyPlayfieldLoadedHandler legacyPlayfieldLoadedHandler
            )
        {
            _ctx = context;
            _legacyPlayfieldLoadedHandler = legacyPlayfieldLoadedHandler;
        }

        public void EnableEventHandlers()
        {
            _ctx.ModBase.Event_Playfield_Loaded += _legacyPlayfieldLoadedHandler.Handle;
        }

        public void DisableEventHandlers()
        {
        }

    }
}

