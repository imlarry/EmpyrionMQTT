using Newtonsoft.Json.Linq;
using ESBLog.Common;

namespace ESBLog.TopicHandlers
{
    public class LogEntityLoaded
    {


        private readonly LoggerSpecificContext _ctx;


        public LogEntityLoaded(LoggerSpecificContext ctx)
        {
            _ctx = ctx;
        }

        public async Task Handle(string topic, string payload)
        {
            await LogEntityLoadedX(topic, payload);
        }

        async Task LogEntityLoadedX(string topic, string payload)
        {
            JObject EntityEvent = JObject.Parse(payload);
            _ctx.DBconnection?.ExecuteCommand(
                "INSERT INTO EntityEventRaw (Id, Name, IsLocal, IsPoi, BelongsTo, DockedTo, Type) VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7)",
                EntityEvent.GetValue("Id"),
                EntityEvent.GetValue("Name"),
                EntityEvent.GetValue("IsLocal"),
                EntityEvent.GetValue("IsPoi"),
                EntityEvent.GetValue("BelongsTo"),
                EntityEvent.GetValue("DockedTo"),
                EntityEvent.GetValue("Type")
            );
            await _ctx.Messenger.SendAsync("LogEntityLoaded", "Entity Loaded");
        }
    }
}
