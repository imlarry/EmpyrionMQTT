using Eleon.Modding;
using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
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
            // unsubscribe and clear state synchronously -- must not defer into queue
            _ctx.GameManager.CurrentPlayfield = null;
            playfield.OnEntityLoaded -= _entityLoadedHandler.Handle;
            playfield.OnEntityUnloaded -= _entityUnloadedHandler.Handle;
            string name = playfield.Name;

            await Execute(async () =>
            {
                JObject json = new JObject(
                    new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
                    new JProperty("Name", name));
                string pfUnloadingJson = json.ToString(Newtonsoft.Json.Formatting.None);
                await _ctx.Messenger.SendAsync("Playfield", MessageType.Evt, "Unloading", pfUnloadingJson);
            });
        }
    }
}
