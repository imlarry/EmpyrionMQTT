using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using ESB.Intefaces;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class PlayfieldLoadedHandler : IPlayfieldLoadedHandler
    {
        private readonly ContextData _cntxt;
        private readonly IEntityLoadedHandler _entityLoadedHandler;
        private readonly IEntityUnloadedHandler _entityUnloadedHandler;

        public PlayfieldLoadedHandler(ContextData cntxt, IEntityLoadedHandler entityLoadedHandler, IEntityUnloadedHandler entityUnloadedHandler)
        {
            _cntxt = cntxt;

            _entityLoadedHandler = entityLoadedHandler;
            _entityUnloadedHandler = entityUnloadedHandler;
        }
        public async void Handle(IPlayfield playfield)
        {
            if (_cntxt.LoadedPlayfield.ContainsKey(playfield.Name))
            {

                JObject json2 = new JObject(
                    new JProperty("PlayfieldName", playfield.Name));
                await _cntxt.Messenger.SendAsync(MessageClass.Exception, "Application.PlayfieldLoadedDuplicate", json2.ToString(Newtonsoft.Json.Formatting.None));
            }
            else
            {
                _cntxt.LoadedPlayfield.Add(playfield.Name, playfield);
            }

            playfield.OnEntityLoaded += _entityLoadedHandler.Handle;
            playfield.OnEntityUnloaded += _entityUnloadedHandler.Handle;

            var entities = playfield.Entities;
            JArray entityArray = new JArray();
            foreach (var entity in entities)
            {
                JObject entityObject = new JObject(
                    new JProperty("Id", entity.Value.Id),
                    new JProperty("Name", entity.Value.Name),
                    new JProperty("Type", entity.Value.Type.ToString()),
                    new JProperty("Position", entity.Value.Position.ToString())
                );
                entityArray.Add(entityObject);
            }
            JObject json = new JObject(
                new JProperty("GameTicks", _cntxt.ModApi.Application.GameTicks),
                new JProperty("Name", playfield.Name),
                new JProperty("PlayfieldType", playfield.PlayfieldType),
                new JProperty("PlanetType", playfield.PlanetType),
                new JProperty("PlanetClass", playfield.PlanetClass),
                new JProperty("SolarSystemName", playfield.SolarSystemName),
                new JProperty("SolarSystemCoordinates", new JObject(
                    new JProperty("X", playfield.SolarSystemCoordinates.x),
                    new JProperty("Y", playfield.SolarSystemCoordinates.y),
                    new JProperty("Z", playfield.SolarSystemCoordinates.z)
                )),
                new JProperty("Entities", entityArray),
            new JProperty("IsPvP", playfield.IsPvP)
            );

            await _cntxt.Messenger.SendAsync(MessageClass.Event, "Application.OnPlayfieldLoaded", json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
