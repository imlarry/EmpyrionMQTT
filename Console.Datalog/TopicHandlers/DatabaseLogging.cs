using ESBLog.Common;

/*
 * ISSUES: using the event handler to capture the playfield loaded event and log to db is efficient but does not capture the playfield positions on entry
 * to a new star system. In the MP game the dedicated server has the global.db file which contains the playfield positions. Certain tables (bookmarks for
 * example) don't generate events but are logged to the global.db file. Finally, the global.db file surogate keys are consistent accross the clients in
 * an MP game while using the event handler to log to the db will generate different keys on each client. 
 * 
 * Thus, if you want to use a singular mechanism to log to the db, you need to use the global.db file.
 * 
 * For this reason I am mothballing this event handler and will use the global.db file to log to the db unless I find a better approach.
 * 
 * Capture of bookmarks will require using a separate handler looking for events that imply the creation of a bookmark. The client has these but the dedi
 * and pf servers do not. As a result, the client will need to request/response query the bookmark events to the dedi/pf servers and ask for delta updates 
 * from the global.db of new and modified bookmarks or triggers will need to be added to the global.db to capture the bookmark events and send them to the
 * client.
 * 
 */

namespace ESBLog.TopicHandlers
{
    public class DatabaseLogging
    {
        private readonly LoggerSpecificContext _ctx;

        public DatabaseLogging(LoggerSpecificContext ctx)
        {
            _ctx = ctx;
        }

        //public async Task Subscribe()
        //{
        //    await _ctx.Messenger.SubscribeAsync("ESB/Client/+/ModApi.Application.OnPlayfieldLoaded/E", LogPlayfieldLoaded);
        //    await _ctx.Messenger.SubscribeAsync("ESB/Client/+/ModApi.Playfield.EntityLoaded/E", LogEntityLoaded);
        //}

        public async Task Subscribe()
        {
            //var logPlayfieldLoaded = new LogPlayfieldLoaded(_ctx);
            var logEntityLoaded = new LogEntityLoaded(_ctx);

            //await _ctx.Messenger.SubscribeAsync("ESB/Client/+/ModApi.Application.OnPlayfieldLoaded/E", logPlayfieldLoaded.Handle);
            await _ctx.Messenger.SubscribeAsync("ESB/Client/+/ModApi.Playfield.EntityLoaded/E", logEntityLoaded.Handle);
        }
    }
}