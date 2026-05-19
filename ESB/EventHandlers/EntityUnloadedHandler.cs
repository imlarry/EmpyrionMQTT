using System;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public class EntityUnloadedHandler : HandlerBase, IEntityUnloadedHandler
    {
        public EntityUnloadedHandler(ContextData context) : base(context) { }

        public async void Handle(IEntity entity)
        {
            int id;
            string name;
            ulong ticks;
            try { id = entity.Id; name = entity.Name; ticks = _ctx.ModApi.Application.GameTicks; }
            catch { return; }

            var json = new JObject(
                new JProperty("GameTicks", ticks),
                new JProperty("Id",        id),
                new JProperty("Name",      name));

            if (!_ctx.IsReady || _ctx.IsTransitioning)
            {
                _ = Execute(() => PublishAsync(json));
                return;
            }
            await PublishAsync(json);
        }

        private async Task PublishAsync(JObject json)
        {
            try
            {
                await _ctx.Bus.PublishContextEventAsync("Entity", "EntityUnloaded", json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "EventHandlers", "EntityUnloaded", ex.ToString()); } catch { }
            }
        }
    }
}
