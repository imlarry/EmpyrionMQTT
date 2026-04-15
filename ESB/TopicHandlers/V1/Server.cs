using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Server
    {
        private readonly ContextData _ctx;

        public Server(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Server.Stats",          Stats);
            _ctx.Messenger.RegisterHandler("V1.Server.ConsoleCommand", ConsoleCommand);
            _ctx.Messenger.RegisterHandler("V1.Server.BannedPlayers",  BannedPlayers);
        }

        // -------------------------------------------------------------------------
        // V1.Server.Stats -- read dedicated server performance metrics
        // Payload: {} (no parameters)
        // Response: {"Data": {"fps": float, "mem": int, "players": int,
        //                     "uptime": int, "ticks": ulong}}
        // -------------------------------------------------------------------------
        public async Task Stats(string topic, string payload)
        {
            try
            {
                var result = await _ctx.ModBase.Request_Dedi_Stats();

                var json = new JObject(new JProperty("Data",
                    result != null ? JObject.FromObject(result) : (JToken)JValue.CreateNull()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Server.ConsoleCommand -- execute a server console command
        // Payload: {"Command": string}
        // Response: {"Ok": true}
        // Note: bypasses the TimeSpan.Zero wrapper so we wait for Event_Ok/Event_Error.
        //       A timeout means the game silently rejected or ignored the command.
        // -------------------------------------------------------------------------
        public async Task ConsoleCommand(string topic, string payload)
        {
            try
            {
                var args    = JObject.Parse(payload);
                var command = (string)args["Command"];

                var requestTask = _ctx.ModBase.Broker.SendRequestAsync<bool>(
                    CmdId.Request_ConsoleCommand, new PString(command));
                if (await Task.WhenAny(requestTask, Task.Delay(5000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for ConsoleCommand -- command may have been silently rejected"));
                    return;
                }
                await requestTask;

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Server.BannedPlayers -- list banned players
        // Payload: {} (no parameters)
        // Response: {"Data": [{"steam64Id": ulong, "dateTime": long}, ...]}
        // Note: the broker wrapper Request_GetBannedPlayers() is typed as IdList but
        // the game actually sends BannedPlayerData. Bypass the wrong-typed wrapper
        // and call SendRequestAsync<BannedPlayerData> directly.
        // -------------------------------------------------------------------------
        public async Task BannedPlayers(string topic, string payload)
        {
            try
            {
                var result = await _ctx.ModBase.Broker.SendRequestAsync<BannedPlayerData>(
                    CmdId.Request_GetBannedPlayers, null);

                var arr = new JArray();
                if (result?.BannedPlayers != null)
                    foreach (var entry in result.BannedPlayers)
                        arr.Add(new JObject(
                            new JProperty("steam64Id", entry.steam64Id),
                            new JProperty("dateTime",  entry.dateTime)));

                var json = new JObject(new JProperty("Data", arr));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
