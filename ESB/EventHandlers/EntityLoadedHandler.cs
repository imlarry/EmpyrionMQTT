using Eleon.Modding;
using ESB.Helpers;
using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public class EntityLoadedHandler : HandlerBase, IEntityLoadedHandler
    {
        public EntityLoadedHandler(ContextData context) : base(context) { }

        public async void Handle(IEntity entity)
        {
            await Execute(async () =>
            {
                JObject json = new JObject(
                    new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
                    new JProperty("Id", entity.Id),
                    new JProperty("Name", entity.Name),
                    new JProperty("Faction", entity.Faction.ToString()),
                    new JProperty("Position", MessageHelpers.Vec(entity.Position)),
                    //new JProperty("Forward", new JObject(
                    //    new JProperty("ForX", entity.Forward.x),
                    //    new JProperty("ForY", entity.Forward.y),
                    //    new JProperty("ForZ", entity.Forward.z)
                    //)),
                    //new JProperty("Rotation", new JObject(
                    //    new JProperty("RotW", entity.Rotation.w),
                    //    new JProperty("RotX", entity.Rotation.x),
                    //    new JProperty("RotY", entity.Rotation.y),
                    //    new JProperty("RotZ", entity.Rotation.z)
                    //)),
                    new JProperty("IsLocal", entity.IsLocal),
                    new JProperty("IsProxy", entity.IsProxy),
                    new JProperty("IsPoi", entity.IsPoi),
                    new JProperty("BelongsTo", entity.BelongsTo),
                    new JProperty("DockedTo", entity.DockedTo),
                    new JProperty("Type", entity.Type.ToString())
                );
                string loadedJson = json.ToString(Newtonsoft.Json.Formatting.None);
                await _ctx.Messenger.SendAsync("Entity", MessageType.Evt, "EntityLoaded", loadedJson);
            });
        }
    }
}
