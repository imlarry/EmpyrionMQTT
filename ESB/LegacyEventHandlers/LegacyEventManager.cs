using ESB.Common;
using ESB.Interfaces;

namespace ESB
{
    public class LegacyEventManager
    {
        readonly private ContextData _cntxt;
        readonly private ILegacyPlayfieldLoadedHandler _legacyPlayfieldLoadedHandler;

        public LegacyEventManager
            (ContextData cntxt
            , ILegacyPlayfieldLoadedHandler legacyPlayfieldLoadedHandler
            )
        {
            _cntxt = cntxt;
            _legacyPlayfieldLoadedHandler = legacyPlayfieldLoadedHandler;
        }

        public void EnableEventHandlers()
        {
            _cntxt.ModBase.Event_Playfield_Loaded += _legacyPlayfieldLoadedHandler.Handle;
        }

        public void DisableEventHandlers()
        {
        }

    }
}

