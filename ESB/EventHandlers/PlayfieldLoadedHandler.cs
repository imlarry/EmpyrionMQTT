using Eleon.Modding;
using ESB.Interfaces;
using ESB.Messaging;
using ESB.Models;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class PlayfieldLoadedHandler : HandlerBase, IPlayfieldLoadedHandler
    {
        private readonly IEntityLoadedHandler _entityLoadedHandler;
        private readonly IEntityUnloadedHandler _entityUnloadedHandler;

        public PlayfieldLoadedHandler(ContextData context, IEntityLoadedHandler entityLoadedHandler, IEntityUnloadedHandler entityUnloadedHandler)
            : base(context)
        {
            _entityLoadedHandler = entityLoadedHandler;
            _entityUnloadedHandler = entityUnloadedHandler;
        }

        public async void Handle(IPlayfield playfield)
        {
            await Execute(async () =>
            {
                if (_ctx.LoadedPlayfield.ContainsKey(playfield.Name))
                {
                    JObject json2 = new JObject(
                        new JProperty("PlayfieldName", playfield.Name));
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, "Application.PlayfieldLoadedDuplicate", json2.ToString(Newtonsoft.Json.Formatting.None));
                    return;
                }

                _ctx.LoadedPlayfield.Add(playfield.Name, playfield);
                playfield.OnEntityLoaded += _entityLoadedHandler.Handle;
                playfield.OnEntityUnloaded += _entityUnloadedHandler.Handle;

                var entities = playfield.Entities;
                JArray entityArray = new JArray();
                foreach (var entity in entities)
                {
                    if (!_ctx.LoadedEntity.ContainsKey(entity.Value.Id))
                        _ctx.LoadedEntity.Add(entity.Value.Id, entity.Value);

                    JObject entityObject = new JObject(
                        new JProperty("Id", entity.Value.Id),
                        new JProperty("Name", entity.Value.Name),
                        new JProperty("Type", entity.Value.Type.ToString()),
                        new JProperty("Position", entity.Value.Position.ToString())
                    );
                    entityArray.Add(entityObject);
                }
                JObject json = new JObject(
                    new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
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

                await _ctx.Messenger.SendAsync(MessageClass.Event, "Application.OnPlayfieldLoaded", json.ToString(Newtonsoft.Json.Formatting.None));
            });
        }
    }
}
