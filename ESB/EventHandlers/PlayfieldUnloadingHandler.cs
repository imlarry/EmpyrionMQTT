using Eleon.Modding;
using ESB.Interfaces;
using ESB.Messaging;
using ESB.Models;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class PlayfieldUnloadingHandler : HandlerBase, IPlayfieldUnloadingHandler
    {
        private readonly IEntityLoadedHandler _entityLoadedHandler;
        private readonly IEntityUnloadedHandler _entityUnloadedHandler;

        public PlayfieldUnloadingHandler(ContextData context, IEntityLoadedHandler entityLoadedHandler, IEntityUnloadedHandler entityUnloadedHandler)
            : base(context)
        {
            _entityLoadedHandler = entityLoadedHandler;
            _entityUnloadedHandler = entityUnloadedHandler;
        }

        public async void Handle(IPlayfield playfield)
        {
            await Execute(async () =>
            {
                _ctx.LoadedPlayfield.Remove(playfield.Name);
                playfield.OnEntityLoaded -= _entityLoadedHandler.Handle;
                playfield.OnEntityUnloaded -= _entityUnloadedHandler.Handle;

                JObject json = new JObject(
                    new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
                    new JProperty("Name", playfield.Name));
                await _ctx.Messenger.SendAsync(MessageClass.Event, "Application.OnPlayfieldUnloading", json.ToString(Newtonsoft.Json.Formatting.None));
            });
        }
    }
}
