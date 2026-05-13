using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Interfaces;
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
            var entitySnapshots = new List<JObject>();
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
                    entitySnapshots.Add(new JObject(
                        new JProperty("Id",       kv.Value.Id),
                        new JProperty("Name",     kv.Value.Name),
                        new JProperty("Type",     kv.Value.Type.ToString()),
                        new JProperty("Position", kv.Value.Position.ToString())));
                }
            }
            catch { return; }

            try
            {
                var entityArray = new JArray();
                foreach (var e in entitySnapshots) entityArray.Add(e);

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
                    new JProperty("Entities",               entityArray));
                await _ctx.Bus.PublishEventAsync("Playfield", "Loaded", json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync("EventHandlers", "PlayfieldLoaded", ex.ToString()); } catch { }
            }
        }
    }
}
