using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Faction
    {
        private readonly ContextData _ctx;

        public Faction(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Faction.List",               List);
            _ctx.Messenger.RegisterHandler("V1.Faction.AlliancesAll",       AlliancesAll);
            _ctx.Messenger.RegisterHandler("V1.Faction.AlliancesByFaction", AlliancesByFaction);
        }

        // -------------------------------------------------------------------------
        // V1.Faction.List -- list all factions known to the server
        // Payload: {} (no parameters)
        // Response: {"Data": [{"origin": byte, "factionId": int, "name": string,
        //                      "abbrev": string}, ...]}
        // Note: the ModBase wrapper for Request_Get_Factions requires arguments.
        // Use the Broker directly with null data.
        // -------------------------------------------------------------------------
        public async Task List(string topic, string payload)
        {
            try
            {
                var requestTask = _ctx.ModBase.Broker.SendRequestAsync<FactionInfoList>(
                    CmdId.Request_Get_Factions, new Id(0));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for Get_Factions"));
                    return;
                }
                var result = await requestTask;

                var json = new JObject(new JProperty("Data",
                    result?.factions != null
                        ? JArray.FromObject(result.factions)
                        : new JArray()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Faction.AlliancesAll -- list the set of faction IDs participating in
        // any alliance on this server
        // Payload: {} (no parameters)
        // Response: {"Data": {"alliances": [factionId, ...]}}
        // -------------------------------------------------------------------------
        public async Task AlliancesAll(string topic, string payload)
        {
            try
            {
                var requestTask = _ctx.ModBase.Request_AlliancesAll();
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for AlliancesAll"));
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
        // V1.Faction.AlliancesByFaction -- query whether two specific factions are allied
        // Payload: {"Faction1Id": int, "Faction2Id": int}
        // Response: {"Data": {"faction1Id": int, "faction2Id": int, "isAllied": bool}}
        // Note: the game requires both faction IDs to be non-zero; omitting or zeroing
        // Faction2Id causes the game to return ErrorType.MissingParameter.
        // -------------------------------------------------------------------------
        public async Task AlliancesByFaction(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var faction1Id = Convert.ToInt32(args.GetValue("Faction1Id"));
                var faction2Id = Convert.ToInt32(args.GetValue("Faction2Id"));

                var requestTask = _ctx.ModBase.Request_AlliancesFaction(
                    new AlliancesFaction { faction1Id = faction1Id, faction2Id = faction2Id });
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for AlliancesFaction {faction1Id}/{faction2Id}"));
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
