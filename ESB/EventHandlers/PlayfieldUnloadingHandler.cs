using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class PlayfieldUnloadingHandler : IPlayfieldUnloadingHandler
    {
        private readonly ContextData _cntxt;
        private readonly IEntityLoadedHandler _entityLoadedHandler;
        private readonly IEntityUnloadedHandler _entityUnloadedHandler;

        public PlayfieldUnloadingHandler(ContextData cntxt, IEntityLoadedHandler entityLoadedHandler, IEntityUnloadedHandler entityUnloadedHandler)
        {
            _cntxt = cntxt;
            _entityLoadedHandler = entityLoadedHandler;
            _entityUnloadedHandler = entityUnloadedHandler;
        }
        public async void Handle(IPlayfield playfield)
        {
            _cntxt.LoadedPlayfield.Remove(playfield.Name);
            playfield.OnEntityLoaded -= _entityLoadedHandler.Handle;
            playfield.OnEntityUnloaded -= _entityUnloadedHandler.Handle;

            JObject json = new JObject(
                new JProperty("GameTicks", _cntxt.ModApi.Application.GameTicks),
                new JProperty("Name", playfield.Name));
            await _cntxt.Messenger.SendAsync(MessageClass.Event, "Application.OnPlayfieldUnloading", json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
