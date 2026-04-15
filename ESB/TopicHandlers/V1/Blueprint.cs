using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Blueprint
    {
        private readonly ContextData _ctx;

        public Blueprint(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Blueprint.Finish",    Finish);
            _ctx.Messenger.RegisterHandler("V1.Blueprint.Resources", Resources);
        }

        // -------------------------------------------------------------------------
        // V1.Blueprint.Finish -- instantly complete the in-progress blueprint in
        // a player's factory. EntityId is the player entity ID (one active blueprint per player).
        // Payload: {"EntityId": int}  -- player entity ID, not a factory/constructor entity ID
        // Response: {"Ok": true}
        // Destructive: immediately completes any in-progress blueprint in the factory.
        // Prerequisites (game-enforced -- generic Exception if either condition is not met):
        //   1. The player must have a blueprint selected and queued in their factory.
        //   2. All required resources must already be present in the factory inventory.
        // Note: bypasses the TimeSpan.Zero wrapper so we wait for Event_Ok/Event_Error.
        // -------------------------------------------------------------------------
        public async Task Finish(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                var requestTask = _ctx.ModBase.Broker.SendRequestAsync<bool>(
                    CmdId.Request_Blueprint_Finish, new Id(entityId));
                if (await Task.WhenAny(requestTask, Task.Delay(5000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for Blueprint.Finish for entity {entityId}"));
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
        // V1.Blueprint.Resources -- set the resource list for a player's blueprint
        // factory, optionally replacing existing items
        // Payload: {"PlayerId": int, "Items": [{"id": int, "count": int}, ...],
        //           "ReplaceExisting": bool (optional, default false)}
        // Response: {"Data": null} -- game returns no payload for this setter command
        // Note: the ModBase wrapper for Request_Blueprint_Resources returns void.
        // Use the Broker directly to obtain the typed response.
        // -------------------------------------------------------------------------
        public async Task Resources(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                var playerId = Convert.ToInt32(args.GetValue("PlayerId"));
                var replace  = args["ReplaceExisting"]?.Value<bool>() ?? false;
                var items    = ParseItemStacks(args["Items"] as JArray);

                var result = await _ctx.ModBase.Broker.SendRequestAsync<BlueprintResources>(
                    CmdId.Request_Blueprint_Resources,
                    new BlueprintResources(playerId, items, replace));

                var json = new JObject(new JProperty("Data",
                    result != null ? JObject.FromObject(result) : (JToken)JValue.CreateNull()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        private static List<ItemStack> ParseItemStacks(JArray arr)
        {
            if (arr == null) return new List<ItemStack>();
            return arr.Select(t => new ItemStack(t["id"].Value<int>(), t["count"].Value<int>())).ToList();
        }
    }
}
