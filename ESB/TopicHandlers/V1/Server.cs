using Eleon.Modding;
using ESB.Common;
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
        // V1.Server.ConsoleCommand -- execute a server console command (fire-and-forget)
        // Payload: {"Command": string}
        // Response: {"Ok": true}
        // Note: the game does not return a result for console commands; Ok confirms
        //       that the request was dispatched, not that the command succeeded.
        // -------------------------------------------------------------------------
        public async Task ConsoleCommand(string topic, string payload)
        {
            try
            {
                var args    = JObject.Parse(payload);
                var command = args["Command"].Value<string>();

                await _ctx.ModBase.Request_ConsoleCommand(new PString(command));

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
