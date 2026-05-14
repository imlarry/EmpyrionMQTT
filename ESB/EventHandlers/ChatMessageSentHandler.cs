using System;
using System.Threading.Tasks;
using Eleon;
using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public class ChatMessageSentHandler : HandlerBase, IChatMessageSentHandler
    {
        public ChatMessageSentHandler(ContextData context) : base(context) { }

        public async void Handle(MessageData chatMsgData)
        {
            ulong ticks;
            int senderEntityId, recipientEntityId;
            string senderType, senderNameOverride, senderFaction, recipientFaction;
            float gameTime;
            bool isTextLocaKey;
            string arg1, arg2, channel, text;
            try
            {
                ticks              = _ctx.ModApi.Application.GameTicks;
                senderEntityId     = chatMsgData.SenderEntityId;
                senderType         = chatMsgData.SenderType.ToString();
                senderNameOverride = chatMsgData.SenderNameOverride;
                senderFaction      = chatMsgData.SenderFaction.ToString();
                recipientEntityId  = chatMsgData.RecipientEntityId;
                recipientFaction   = chatMsgData.RecipientFaction.ToString();
                gameTime           = chatMsgData.GameTime;
                isTextLocaKey      = chatMsgData.IsTextLocaKey;
                arg1               = chatMsgData.Arg1;
                arg2               = chatMsgData.Arg2;
                channel            = chatMsgData.Channel.ToString();
                text               = chatMsgData.Text;
            }
            catch { return; }

            try
            {
                var json = new JObject(
                    new JProperty("GameTicks",          ticks),
                    new JProperty("SenderEntityId",     senderEntityId),
                    new JProperty("SenderType",         senderType),
                    new JProperty("SenderNameOverride", senderNameOverride),
                    new JProperty("SenderFaction",      senderFaction),
                    new JProperty("RecipientEntityId",  recipientEntityId),
                    new JProperty("RecipientFaction",   recipientFaction),
                    new JProperty("GameTime",           gameTime),
                    new JProperty("IsTextLocaKey",      isTextLocaKey),
                    new JProperty("Arg1",               arg1),
                    new JProperty("Arg2",               arg2),
                    new JProperty("Channel",            channel),
                    new JProperty("Text",               text));
                var rcId = _ctx.GameManager.GameRcId ?? RoutingContextId.BroadcastValue;
                await _ctx.Bus.PublishEventAsync(rcId, "App", "ChatMessageSent", json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "EventHandlers", "ChatMessageSent", ex.ToString()); } catch { }
            }
        }
    }
}
