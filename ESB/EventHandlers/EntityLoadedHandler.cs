using System;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Helpers;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public class EntityLoadedHandler : HandlerBase, IEntityLoadedHandler
    {
        public EntityLoadedHandler(ContextData context) : base(context) { }

        public async void Handle(IEntity entity)
        {
            int id;
            string name, faction, type;
            bool isLocal, isProxy, isPoi;
            int belongsTo, dockedTo;
            JObject position;
            ulong ticks;
            try
            {
                ticks     = _ctx.ModApi.Application.GameTicks;
                id        = entity.Id;
                name      = entity.Name;
                faction   = entity.Faction.ToString();
                position  = MessageHelpers.Vec(entity.Position);
                isLocal   = entity.IsLocal;
                isProxy   = entity.IsProxy;
                isPoi     = entity.IsPoi;
                belongsTo = entity.BelongsTo;
                dockedTo  = entity.DockedTo;
                type      = entity.Type.ToString();
            }
            catch { return; }

            try
            {
                var json = new JObject(
                    new JProperty("GameTicks", ticks),
                    new JProperty("Id",        id),
                    new JProperty("Name",      name),
                    new JProperty("Faction",   faction),
                    new JProperty("Position",  position),
                    new JProperty("IsLocal",   isLocal),
                    new JProperty("IsProxy",   isProxy),
                    new JProperty("IsPoi",     isPoi),
                    new JProperty("BelongsTo", belongsTo),
                    new JProperty("DockedTo",  dockedTo),
                    new JProperty("Type",      type));
                await _ctx.Bus.PublishEventAsync("Playfield", "EntityLoaded", json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync("EventHandlers", "EntityLoaded", ex.ToString()); } catch { }
            }
        }
    }
}
