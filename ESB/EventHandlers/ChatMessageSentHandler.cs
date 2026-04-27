using Eleon;
using Eleon.Modding;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class ChatMessageSentHandler : HandlerBase, IChatMessageSentHandler
    {
        public ChatMessageSentHandler(ContextData context) : base(context) { }

        public async void Handle(MessageData chatMsgData)
        {
            await Execute(async () =>
            {
                JObject json = new JObject(
                        new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
                        new JProperty("SenderEntityId", chatMsgData.SenderEntityId),
                        new JProperty("SenderType", chatMsgData.SenderType.ToString()),
                        new JProperty("SenderNameOverride", chatMsgData.SenderNameOverride),
                        new JProperty("SenderFaction", chatMsgData.SenderFaction.ToString()),
                        new JProperty("RecipientEntityId", chatMsgData.RecipientEntityId),
                        new JProperty("RecipientFaction", chatMsgData.RecipientFaction.ToString()),
                        new JProperty("GameTime", chatMsgData.GameTime),
                        new JProperty("IsTextLocaKey", chatMsgData.IsTextLocaKey),
                        new JProperty("Arg1", chatMsgData.Arg1),
                        new JProperty("Arg2", chatMsgData.Arg2),
                        new JProperty("Channel", chatMsgData.Channel.ToString()),
                new JProperty("Text", chatMsgData.Text)
                        );
                string chatJson = json.ToString(Newtonsoft.Json.Formatting.None);
                await EmitEventAsync("App", "ChatMessageSent", chatJson);
            });
        }
    }
}
