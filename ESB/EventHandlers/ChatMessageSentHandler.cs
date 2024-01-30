using Eleon;
using ESB.Common;
using ESB.Messaging;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;
using Eleon.Modding;

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

            void DialogActionHandler(int buttonIdx, string linkId, string inputContent, int playerId, int customValue)
            {
                _ = _cntxt.Messenger.SendAsync(MessageClass.Exception, "Gui.ShowDialog", "Button:" + buttonIdx.ToString());
            }

            string[] bt = { "esc", "enter", "punt" };
            var config = new DialogConfig
            {
                TitleText = "TitleText",
                BodyText = "BodyText - This is a test",
                CloseOnLinkClick = true,
                ButtonTexts = bt,
                ButtonIdxForEsc = 0,
                ButtonIdxForEnter = 1,
                MaxChars = 0
                //Placeholder = "Placeholder",
                //InitialContent = "InitialContent"
            };
            var handler = new DialogActionHandler(DialogActionHandler);
            var displayed = _cntxt.ModApi.GUI.ShowDialog(config, handler, 0);
            await _cntxt.Messenger.SendAsync(MessageClass.Response, "FromChatHandler", "Displayed: " + displayed.ToString());
        }
    }
}
