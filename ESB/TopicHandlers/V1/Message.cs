using Eleon.Modding;
using ESB.Models;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Message
    {
        private readonly ContextData _ctx;

        public Message(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Message.ToPlayer",  ToPlayer);
            _ctx.Messenger.RegisterHandler("V1.Message.ToAll",     ToAll);
            _ctx.Messenger.RegisterHandler("V1.Message.ToFaction", ToFaction);
            _ctx.Messenger.RegisterHandler("V1.Message.Dialog",    Dialog);
        }

        // -------------------------------------------------------------------------
        // V1.Message.ToPlayer -- send an in-game message to a single player
        // Payload: {"EntityId": int, "Message": string, "Priority": int (0=Alarm,1=Message,2=Info),
        //           "Duration": float (seconds, optional, default 10)}
        // Response: {"Ok": true}
        // Note: bypasses the TimeSpan.Zero wrapper so we wait for Event_Ok/Event_Error.
        // -------------------------------------------------------------------------
        public async Task ToPlayer(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));
                var msg      = (string)args["Message"];
                var prio     = (byte)(args["Priority"]?.Value<int>() ?? 1);
                var time     = args["Duration"]?.Value<float>()      ?? 10f;

                var requestTask = _ctx.ModBase.Broker.SendRequestAsync<bool>(
                    CmdId.Request_InGameMessage_SinglePlayer, new IdMsgPrio(entityId, msg, prio, time));
                if (await Task.WhenAny(requestTask, Task.Delay(5000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for ToPlayer message to entity {entityId}"));
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
        // V1.Message.ToAll -- broadcast an in-game message to all connected players
        // Payload: {"Message": string, "Priority": int (0=Alarm,1=Message,2=Info),
        //           "Duration": float (seconds, optional, default 10)}
        // Response: {"Ok": true}
        // Note: bypasses the TimeSpan.Zero wrapper so we wait for Event_Ok/Event_Error.
        // -------------------------------------------------------------------------
        public async Task ToAll(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var msg  = (string)args["Message"];
                var prio = (byte)(args["Priority"]?.Value<int>() ?? 1);
                var time = args["Duration"]?.Value<float>()      ?? 10f;

                var requestTask = _ctx.ModBase.Broker.SendRequestAsync<bool>(
                    CmdId.Request_InGameMessage_AllPlayers, new IdMsgPrio(0, msg, prio, time));
                if (await Task.WhenAny(requestTask, Task.Delay(5000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for ToAll message"));
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
        // V1.Message.ToFaction -- send an in-game message to all members of a faction
        // Payload: {"FactionId": int, "Message": string, "Priority": int,
        //           "Duration": float (seconds, optional, default 10)}
        // Response: {"Ok": true}
        // Note: bypasses the TimeSpan.Zero wrapper so we wait for Event_Ok/Event_Error.
        // -------------------------------------------------------------------------
        public async Task ToFaction(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                var factionId = Convert.ToInt32(args.GetValue("FactionId"));
                var msg       = (string)args["Message"];
                var prio      = (byte)(args["Priority"]?.Value<int>() ?? 1);
                var time      = args["Duration"]?.Value<float>()      ?? 10f;

                var requestTask = _ctx.ModBase.Broker.SendRequestAsync<bool>(
                    CmdId.Request_InGameMessage_Faction, new IdMsgPrio(factionId, msg, prio, time));
                if (await Task.WhenAny(requestTask, Task.Delay(5000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for ToFaction message to faction {factionId}"));
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
        // V1.Message.Dialog -- show a dialog box to a player and wait for button click
        // Payload: {"EntityId": int, "Message": string,
        //           "PosButton": string (optional, default "OK"),
        //           "NegButton": string (optional, omit for single-button dialog)}
        // Response: {"Data": {"id": int, "value": int}}
        //   value: 0 = positive button, 1 = negative button
        // -------------------------------------------------------------------------
        public async Task Dialog(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                var entityId  = Convert.ToInt32(args.GetValue("EntityId"));
                var msg       = args["Message"].Value<string>();
                var posButton = args["PosButton"]?.Value<string>() ?? "OK";
                var negButton = args["NegButton"]?.Value<string>() ?? "";

                var result = await _ctx.ModBase.Request_ShowDialog_SinglePlayer(
                    new DialogBoxData
                    {
                        Id            = entityId,
                        MsgText       = msg,
                        PosButtonText = posButton,
                        NegButtonText = negButton,
                    });

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
