using Newtonsoft.Json.Linq;
using System.Data.SQLite;
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
            _ctx.DBconnection?.DoWork(db =>
            {
                using var insertCommand = new SQLiteCommand("INSERT INTO EntityEventRaw (Id, Name, IsLocal, IsPoi, BelongsTo, DockedTo, Type) VALUES (@id, @name, @isLocal, @isPoi, @belongsTo, @dockedTo, @type)", db);
                insertCommand.Parameters.AddWithValue("@id", EntityEvent.GetValue("Id"));
                insertCommand.Parameters.AddWithValue("@name", EntityEvent.GetValue("Name"));
                insertCommand.Parameters.AddWithValue("@isLocal", EntityEvent.GetValue("IsLocal"));
                insertCommand.Parameters.AddWithValue("@isPoi", EntityEvent.GetValue("IsPoi"));
                insertCommand.Parameters.AddWithValue("@belongsTo", EntityEvent.GetValue("BelongsTo"));
                insertCommand.Parameters.AddWithValue("@dockedTo", EntityEvent.GetValue("DockedTo"));
                insertCommand.Parameters.AddWithValue("@type", EntityEvent.GetValue("Type"));

                insertCommand.ExecuteNonQuery();
            });
            await _ctx.Messenger.SendAsync("LogEntityLoaded", "Entity Loaded");
        }
    }
}
