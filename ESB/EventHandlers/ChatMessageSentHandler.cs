using Eleon;
using ESB.Common;
using ESB.Messaging;
using ESB.Intefaces;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class ChatMessageSentHandler : IChatMessageSentHandler
    {
        private readonly ContextData _cntxt;

        public ChatMessageSentHandler(ContextData cntxt)
        {
            _cntxt = cntxt;
        }
        public async void Handle(MessageData chatMsgData)
        {
            JObject json = new JObject(
                    new JProperty("GameTicks", _cntxt.ModApi.Application.GameTicks),
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
            await _cntxt.Messenger.SendAsync(MessageClass.Event, "Application.ChatMessageSent", json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
