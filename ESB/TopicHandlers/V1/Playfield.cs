using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Playfield
    {
        private readonly ContextData _ctx;

        public Playfield(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Playfield.List",       List);
            _ctx.Messenger.RegisterHandler("V1.Playfield.Stats",      Stats);
            _ctx.Messenger.RegisterHandler("V1.Playfield.Load",       Load);
            _ctx.Messenger.RegisterHandler("V1.Playfield.EntityList", EntityList);
        }

        // -------------------------------------------------------------------------
        // V1.Playfield.List -- list all playfield names known to the server
        // Payload: {} (no parameters)
        // Response: {"Data": ["playfieldName", ...]}
        // -------------------------------------------------------------------------
        public async Task List(string topic, string payload)
        {
            try
            {
                var result = await _ctx.ModBase.Broker.Request_Playfield_List();

                var json = new JObject(new JProperty("Data",
                    result?.playfields != null
                        ? JArray.FromObject(result.playfields)
                        : new JArray()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Playfield.Stats -- read performance stats for a specific playfield
        // Payload: {"Playfield": string}
        // Response: {"Data": {"playfield": string, "fps": float, "mem": int,
        //                     "players": int, "mobs": int, "structs": int, ...}}
        // -------------------------------------------------------------------------
        public async Task Stats(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var name = args["Playfield"].Value<string>();

                var result = await _ctx.ModBase.Broker.Request_Playfield_Stats(new PString(name));

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
        // V1.Playfield.Load -- force-load an idle playfield server
        // Payload: {"Playfield": string, "Seconds": float (optional, default 0),
        //           "ProcessId": int (optional, default 0)}
        // Response: {"Ok": true}
        // Note: the game returns PlayfieldAlreadyLoaded if the playfield is already running.
        //       The caller receives X/ with that error -- it is not an ESB failure.
        // -------------------------------------------------------------------------
        public async Task Load(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var name = args["Playfield"].Value<string>();
                var sec  = args["Seconds"]?.Value<float>()   ?? 0f;
                var pid  = args["ProcessId"]?.Value<int>()   ?? 0;

                var requestTask = _ctx.ModBase.Broker.Request_Load_Playfield(new PlayfieldLoad(sec, name, pid));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for Load_Playfield '{name}'"));
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
        // V1.Playfield.EntityList -- list entities on a specific playfield
        // Payload: {"Playfield": string}
        // Response: {"Data": {"playfield": string, "entities": [EntityInfo, ...]}}
        // -------------------------------------------------------------------------
        public async Task EntityList(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var name = args["Playfield"].Value<string>();

                var result = await _ctx.ModBase.Broker.Request_Playfield_Entity_List(new PString(name));

                var json = new JObject(new JProperty("Data",
                    result != null ? JObject.FromObject(result) : (JToken)JValue.CreateNull()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
