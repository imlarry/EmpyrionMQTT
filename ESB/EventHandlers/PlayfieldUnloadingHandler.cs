using System;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public class PlayfieldUnloadingHandler : HandlerBase, IPlayfieldUnloadingHandler
    {
        private readonly IEntityLoadedHandler   _entityLoadedHandler;
        private readonly IEntityUnloadedHandler _entityUnloadedHandler;

        public PlayfieldUnloadingHandler(ContextData context,
            IEntityLoadedHandler entityLoadedHandler, IEntityUnloadedHandler entityUnloadedHandler)
            : base(context)
        {
            _entityLoadedHandler   = entityLoadedHandler;
            _entityUnloadedHandler = entityUnloadedHandler;
        }

        public async void Handle(IPlayfield playfield)
        {
            // Unwire entity events immediately -- these must stay synchronous.
            playfield.OnEntityLoaded   -= _entityLoadedHandler.Handle;
            playfield.OnEntityUnloaded -= _entityUnloadedHandler.Handle;

            ulong ticks;
            string name;
            try { ticks = _ctx.ModApi.Application.GameTicks; name = playfield.Name; }
            catch { return; }

            try
            {
                var json = new JObject(
                    new JProperty("GameTicks", ticks),
                    new JProperty("Name",      name));
                await _ctx.Bus.PublishEventAsync(_ctx.GameManager.ContextRcId, "Playfield", "Unloading", json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "EventHandlers", "PlayfieldUnloading", ex.ToString()); } catch { }
            }
        }
    }
}
