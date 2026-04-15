using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Structure
    {
        private readonly ContextData _ctx;

        public Structure(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Structure.ListGlobal", ListGlobal);
            _ctx.Messenger.RegisterHandler("V1.Structure.Update",     Update);
            _ctx.Messenger.RegisterHandler("V1.Structure.Touch",      Touch);
            _ctx.Messenger.RegisterHandler("V1.Structure.BlockStats", BlockStats);
        }

        // -------------------------------------------------------------------------
        // V1.Structure.ListGlobal -- list all structures server-wide, including on
        // idle/unloaded playfields.
        // Payload: {"PlayfieldId": int?} -- 0 or omitted returns all structures
        // Response: {"Data": {"globalStructures": {playfieldName: [GlobalStructureInfo, ...]}}}
        // Note: the ModBase wrapper for Request_GlobalStructure_List has the wrong parameter
        // type (expects Timeouts). Use the Broker directly with the raw Id argument.
        // -------------------------------------------------------------------------
        public async Task ListGlobal(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var playfieldId = args["PlayfieldId"] != null
                    ? Convert.ToInt32(args["PlayfieldId"])
                    : 0;

                var requestTask = _ctx.ModBase.Broker.SendRequestAsync<GlobalStructureList>(
                    CmdId.Request_GlobalStructure_List, new Id(playfieldId));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for GlobalStructure_List"));
                    return;
                }
                var result = await requestTask;

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
        // V1.Structure.Update -- update a global structure's metadata server-side
        // Payload: {"Id": int, "Name": string?, "FactionGroup": int?, "FactionId": int?,
        //           "Powered": bool?, "Fuel": int?}
        // Response: {"Ok": true}
        // Destructive: overwrites the server-side structure record fields provided.
        // Note: the ModBase wrapper for Request_GlobalStructure_Update has the wrong
        // parameter type (expects PString). Use the Broker directly.
        // -------------------------------------------------------------------------
        public async Task Update(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var info = new GlobalStructureInfo
                {
                    id            = Convert.ToInt32(args.GetValue("Id")),
                    name          = args["Name"] != null ? (string)args["Name"] : "",
                    PlayfieldName = args["PlayfieldName"] != null ? (string)args["PlayfieldName"] : "",
                    factionGroup  = args["FactionGroup"] != null ? (byte)(int)args["FactionGroup"] : (byte)0,
                    factionId     = args["FactionId"] != null ? (int)args["FactionId"] : 0,
                    powered       = args["Powered"] != null ? (bool)args["Powered"] : false,
                    fuel          = args["Fuel"] != null ? (int)args["Fuel"] : 0,
                    type          = args["Type"] != null ? (byte)(int)args["Type"] : (byte)0,
                };

                var requestTask = _ctx.ModBase.Broker.SendRequestAsync<Id>(
                    CmdId.Request_GlobalStructure_Update, info);
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for GlobalStructure_Update"));
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
        // V1.Structure.Touch -- refresh the lastVisitedTicks timestamp server-side
        // Payload: {"EntityId": int}
        // Response: {"Ok": true}
        // Note: prevents the structure from being flagged as abandoned/decaying.
        // -------------------------------------------------------------------------
        public async Task Touch(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                var requestTask = _ctx.ModBase.Request_Structure_Touch(new Id(entityId));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for Structure_Touch of entity {entityId}"));
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
        // V1.Structure.BlockStats -- read block-type counts for a structure
        // Payload: {"EntityId": int}
        // Response: {"Data": {"id": int, "blockStatistics": {blockTypeId: count, ...}}}
        // -------------------------------------------------------------------------
        public async Task BlockStats(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                var requestTask = _ctx.ModBase.Request_Structure_BlockStatistics(new Id(entityId));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for Structure_BlockStatistics of entity {entityId}"));
                    return;
                }
                var result = await requestTask;

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
