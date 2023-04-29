using System;
using Eleon.Modding;
using Newtonsoft.Json.Linq;
using ESBGameMod;

namespace ModApi
{
    public class Gui
    {
        private readonly ContextData _ctx;

        public Gui(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void ShowGameMessage(string topic, string payload)
        {
            // parse args
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
        }

        void DialogActionHandler(int buttonIdx, string linkId, string inputContent, int playerId, int customValue)
        {
            _ = _ctx.Messenger.SendAsync("ModApi.Application.ShowDialog/X", "Button:" + buttonIdx.ToString());
        }

        public async void ShowDialog(string topic, string payload)
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
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), "Displayed: " + displayed.ToString());
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void IsWorldVisible(string topic, string payload)
        // TODO: this one is causing a stack dump from inside the try/catch ... moving on
        {
            try
            {
                var isWorldVisible = _ctx.ModApi.GUI.IsWorldVisible;
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), "IsWorldVisible = " + isWorldVisible.ToString());
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }
    }


}