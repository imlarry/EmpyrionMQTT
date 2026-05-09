using Eleon.Modding;
using ESB.Interfaces;
using ESB.Payloads;

namespace ESB.EventHandlers
{
    public class EntityLoadedHandler : HandlerBase, IEntityLoadedHandler
    {
        public EntityLoadedHandler(ContextData context) : base(context) { }

        public async void Handle(IEntity entity)
        {
            await Execute(async () =>
            {
                var payload = new EntityLoadedPayload
                {
                    GameTicks = _ctx.ModApi.Application.GameTicks,
                    Id        = entity.Id,
                    Name      = entity.Name,
                    Faction   = entity.Faction.ToString(),
                    Position  = new Vec3Payload { X = entity.Position.x, Y = entity.Position.y, Z = entity.Position.z },
                    IsLocal   = entity.IsLocal,
                    IsProxy   = entity.IsProxy,
                    IsPoi     = entity.IsPoi,
                    BelongsTo = entity.BelongsTo,
                    DockedTo  = entity.DockedTo,
                    Type      = entity.Type.ToString()
                };
                await _ctx.Bus.PublishEventAsync("Entity", "EntityLoaded", payload);
            });
        }
    }
}
