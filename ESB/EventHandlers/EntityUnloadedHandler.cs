using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using ESB.Intefaces;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class EntityUnloadedHandler : IEntityUnloadedHandler
    {
        private readonly ContextData _cntxt;

        public EntityUnloadedHandler(ContextData cntxt)
        {
            _cntxt = cntxt;
        }
        public async void Handle(IEntity entity)
        {
            _cntxt.LoadedEntity.Remove(entity.Id);
            JObject json = new JObject(
                    new JProperty("GameTicks", _cntxt.ModApi.Application.GameTicks),
                    new JProperty("Id", entity.Id),
                    new JProperty("Name", entity.Name)
                    );
            await _cntxt.Messenger.SendAsync(MessageClass.Event, "Playfield.OnEntityUnloaded", json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
