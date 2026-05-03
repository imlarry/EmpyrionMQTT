using Eleon.Modding;
using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public class EntityUnloadedHandler : HandlerBase, IEntityUnloadedHandler
    {
        public EntityUnloadedHandler(ContextData context) : base(context) { }

        public async void Handle(IEntity entity)
        {
            await Execute(async () =>
            {
                JObject json = new JObject(
                        new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
                        new JProperty("Id", entity.Id),
                        new JProperty("Name", entity.Name)
                        );
                string unloadedJson = json.ToString(Newtonsoft.Json.Formatting.None);
                await _ctx.Messenger.SendAsync("Playfield", MessageType.Evt, "EntityUnloaded", unloadedJson);
            });
        }
    }
}
