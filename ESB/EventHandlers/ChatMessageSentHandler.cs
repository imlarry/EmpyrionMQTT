using Eleon;
using Eleon.Modding;
using ESB.Interfaces;
using ESB.Messaging;
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
                await _ctx.Messenger.SendAsync(MessageClass.Event, "Application.ChatMessageSent", json.ToString(Newtonsoft.Json.Formatting.None));

                void DialogActionHandler(int buttonIdx, string linkId, string inputContent, int playerId, int customValue)
                {
                    _ = _ctx.Messenger.SendAsync(MessageClass.Exception, "Gui.ShowDialog", "Button:" + buttonIdx.ToString());
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
                var displayed = _ctx.ModApi.GUI.ShowDialog(config, handler, 0);
                await _ctx.Messenger.SendAsync(MessageClass.Response, "FromChatHandler", "Displayed: " + displayed.ToString());
            });
        }
    }
}
