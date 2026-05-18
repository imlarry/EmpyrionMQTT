using System;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Helpers;
using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public class PlayfieldLoadedHandler : HandlerBase, IPlayfieldLoadedHandler
    {
        private readonly IEntityLoadedHandler   _entityLoadedHandler;
        private readonly IEntityUnloadedHandler _entityUnloadedHandler;

        public PlayfieldLoadedHandler(ContextData context,
            IEntityLoadedHandler entityLoadedHandler, IEntityUnloadedHandler entityUnloadedHandler)
            : base(context)
        {
            _entityLoadedHandler   = entityLoadedHandler;
            _entityUnloadedHandler = entityUnloadedHandler;
        }

        public async void Handle(IPlayfield playfield)
        {
            // Wire entity events and record current playfield -- these must stay synchronous.
            _ctx.GameManager.CurrentPlayfield = playfield;
            playfield.OnEntityLoaded   += _entityLoadedHandler.Handle;
            playfield.OnEntityUnloaded += _entityUnloadedHandler.Handle;

            string name, playfieldType, planetType, planetClass, solarSystemName;
            bool isPvP;
            float ssX, ssY, ssZ;
            ulong ticks;
            var entityRows = new JArray();
            try
            {
                ticks           = _ctx.ModApi.Application.GameTicks;
                name            = playfield.Name;
                playfieldType   = playfield.PlayfieldType;
                planetType      = playfield.PlanetType;
                planetClass     = playfield.PlanetClass;
                solarSystemName = playfield.SolarSystemName;
                ssX             = playfield.SolarSystemCoordinates.x;
                ssY             = playfield.SolarSystemCoordinates.y;
                ssZ             = playfield.SolarSystemCoordinates.z;
                isPvP           = playfield.IsPvP;

                foreach (var kv in playfield.Entities)
                {
                    entityRows.Add(new JArray(
                        kv.Value.Id,
                        kv.Value.Name,
                        kv.Value.Type.ToString(),
                        kv.Value.Position.ToString()));
                }
            }
            catch { return; }

            try
            {
                var json = new JObject(
                    new JProperty("GameTicks",              ticks),
                    new JProperty("Name",                   name),
                    new JProperty("PlayfieldType",          playfieldType),
                    new JProperty("PlanetType",             planetType),
                    new JProperty("PlanetClass",            planetClass),
                    new JProperty("SolarSystemName",        solarSystemName),
                    new JProperty("SolarSystemCoordinates", new JObject(
                        new JProperty("X", ssX),
                        new JProperty("Y", ssY),
                        new JProperty("Z", ssZ))),
                    new JProperty("IsPvP",                  isPvP),
                    new JProperty("Entities", MessageHelpers.Tabular(
                        new[] { "Id", "Name", "Type", "Position" }, entityRows)));
                await _ctx.Bus.PublishEventAsync(_ctx.GameManager.ContextRcId, "Playfield", "Loaded", json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "EventHandlers", "PlayfieldLoaded", ex.ToString()); } catch { }
            }
        }
    }
}
