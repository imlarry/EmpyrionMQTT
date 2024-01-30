using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class EntityLoadedHandler : IEntityLoadedHandler
    {
        private readonly ContextData _cntxt;

        public EntityLoadedHandler(ContextData cntxt)
        {
            _cntxt = cntxt;
        }
        public async void Handle(IEntity entity)
        {
            if (_cntxt.LoadedEntity.ContainsKey(entity.Id))
            {

                JObject json2 = new JObject(
                    new JProperty("EntityId", entity.Id));
                await _cntxt.Messenger.SendAsync(MessageClass.Exception, "Playfield.EntityLoadedDuplicate", json2.ToString(Newtonsoft.Json.Formatting.None));
            }
            else
            {
                _cntxt.LoadedEntity.Add(entity.Id, entity);
            }
            JObject json = new JObject(
                new JProperty("GameTicks", _cntxt.ModApi.Application.GameTicks),
                new JProperty("Id", entity.Id),
                new JProperty("Name", entity.Name),
                new JProperty("Faction", entity.Faction.ToString()),
                new JProperty("Position", new JObject(
                    new JProperty("PosX", entity.Position.x),
                    new JProperty("PosY", entity.Position.y),
                    new JProperty("PosZ", entity.Position.z)
                )),
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
                new JProperty("Type", entity.Type.ToString()),
                new JProperty("Structure", null)
            );
            await _cntxt.Messenger.SendAsync(MessageClass.Event, "Playfield.EntityLoaded", json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
