using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public class Gui : IGui
    {
        private readonly ContextData _ctx;

        public Gui(ContextData ctx)
        {
            _ctx = ctx;
        }

        public async Task Subscribe()
        {
            await _ctx.Messenger.SubscribeAsync("Edna.TestSelf", Edna_Selftest);
            await _ctx.Messenger.SubscribeAsync("Gui.ShowGameMessage", ShowGameMessage);
            await _ctx.Messenger.SubscribeAsync("Gui.ShowDialog", ShowDialog);
            await _ctx.Messenger.SubscribeAsync("Gui.IsWorldVisible", IsWorldVisible);
        }

        public async Task Edna_Selftest(string topic, string payload)
        {
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "this is the reply from Edna_Selftest(T,P)");
        }
            public async Task ShowGameMessage(string topic, string payload)
        {
            try
            {
                JObject GuiArgs = JObject.Parse(payload);
                string text = GuiArgs.GetValue("Text")?.ToString();
                int? prio = GuiArgs.GetValue("Prio")?.Value<int>();
                float? duration = GuiArgs.GetValue("Duration")?.Value<float>();
                // make the call (all variations of optional defaulted params)
                if (prio.HasValue && duration.HasValue)
                {
                    _ctx.ModApi.GUI.ShowGameMessage(text, prio.Value, duration.Value);
                }
                else if (prio.HasValue)
                {
                    _ctx.ModApi.GUI.ShowGameMessage(text, prio.Value);
                }
                else if (duration.HasValue)
                {
                    _ctx.ModApi.GUI.ShowGameMessage(text, duration: duration.Value);
                }
                else
                {
                    _ctx.ModApi.GUI.ShowGameMessage(text);
                }

                JObject json = new JObject(new JProperty("Path", "TODO"));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        void DialogActionHandler(int buttonIdx, string linkId, string inputContent, int playerId, int customValue)
        {
            _ = _ctx.Messenger.SendAsync(MessageClass.Exception, "Gui.ShowDialog", "Button:" + buttonIdx.ToString());
        }

        public async Task ShowDialog(string topic, string payload)
        // TODO: this one is causing a stack dump from inside the try/catch ... no idea why
        {
            try
            {
                string[] bt = { "dog", "cat", "duck" };
                var config = new DialogConfig
                {
                    TitleText = "TitleText: Your Title Here",
                    BodyText = "BodyText: This is a test of the emergency broadcast system.",
                    //CloseOnLinkClick = true,
                    ButtonTexts = bt,
                    //ButtonIdxForEsc = 0,
                    //ButtonIdxForEnter = 1,
                    //MaxChars = 30,
                    //Placeholder = "Placeholder",
                    //InitialContent = "InitialContent"
                };
                var handler = new DialogActionHandler(DialogActionHandler);
                var displayed = _ctx.ModApi.GUI.ShowDialog(config, handler, 0);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "Displayed: " + displayed.ToString());
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task IsWorldVisible(string topic, string payload)
        // TODO: this one is causing a stack dump from inside the try/catch ... moving on
        {
            try
            {
                var isWorldVisible = _ctx.ModApi.GUI.IsWorldVisible;
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "IsWorldVisible = " + isWorldVisible.ToString());
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }
    }


}