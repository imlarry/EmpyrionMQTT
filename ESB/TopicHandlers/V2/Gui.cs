using Eleon.Modding;
using ESB.Models;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public class Gui
    {
        private readonly ContextData _ctx;

        public Gui(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Gui.ShowGameMessage", ShowGameMessage);
            _ctx.Messenger.RegisterHandler("V2.Gui.ShowDialog",      ShowDialog);
            _ctx.Messenger.RegisterHandler("V2.Gui.IsWorldVisible",  IsWorldVisible);
        }

        public async Task ShowGameMessage(string topic, string payload)
        {
            try
            {
                JObject GuiArgs = JObject.Parse(payload);
                string text = GuiArgs.GetValue("Text")?.ToString();
                int? prio = GuiArgs.GetValue("Prio")?.Value<int>();
                float? duration = GuiArgs.GetValue("Duration")?.Value<float>();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    if (prio.HasValue && duration.HasValue)
                        _ctx.ModApi.GUI.ShowGameMessage(text, prio.Value, duration.Value);
                    else if (prio.HasValue)
                        _ctx.ModApi.GUI.ShowGameMessage(text, prio.Value);
                    else if (duration.HasValue)
                        _ctx.ModApi.GUI.ShowGameMessage(text, duration: duration.Value);
                    else
                        _ctx.ModApi.GUI.ShowGameMessage(text);

                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, new JObject(new JProperty("Text", text)).ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        void DialogActionHandler(int buttonIdx, string linkId, string inputContent, int playerId, int customValue)
        {
            JObject json = new JObject(
                new JProperty("ButtonIdx", buttonIdx),
                new JProperty("LinkId", linkId),
                new JProperty("InputContent", inputContent),
                new JProperty("PlayerId", playerId),
                new JProperty("CustomValue", customValue)
            );

            _ = _ctx.Messenger.SendAsync(MessageClass.Event, "V2.Gui.ShowDialog", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public async Task ShowDialog(string topic, string payload)
        {
            bool displayed = false;
            try
            {
                JObject dialogArgs = JObject.Parse(payload);
                string[] bt = dialogArgs.GetValue("ButtonTexts")?.ToObject<string[]>();
                var config = new DialogConfig
                {
                    TitleText = dialogArgs.GetValue("TitleText")?.ToString(),
                    BodyText = dialogArgs.GetValue("BodyText")?.ToString(),
                    CloseOnLinkClick = dialogArgs.GetValue("CloseOnLinkClick")?.Value<bool>() ?? true,
                    ButtonTexts = bt,
                    ButtonIdxForEsc = dialogArgs.GetValue("ButtonIdxForEsc")?.Value<int>() ?? 0,
                    ButtonIdxForEnter = dialogArgs.GetValue("ButtonIdxForEnter")?.Value<int>() ?? 1,
                    MaxChars = dialogArgs.GetValue("MaxChars")?.Value<int>() ?? 0,
                    Placeholder = dialogArgs.GetValue("Placeholder")?.ToString(),
                    InitialContent = dialogArgs.GetValue("InitialContent")?.ToString()
                };
                var handler = new DialogActionHandler(DialogActionHandler);

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    displayed = _ctx.ModApi.GUI.ShowDialog(config, handler, 0);
                    JObject json = new JObject(new JProperty("Displayed", displayed));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task IsWorldVisible(string topic, string payload)
        {
            try
            {
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var isWorldVisible = _ctx.ModApi.GUI.IsWorldVisible;
                    JObject json = new JObject(new JProperty("IsWorldVisible", isWorldVisible));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
