using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

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
            JObject json = new JObject(
                new JProperty("ButtonIdx", buttonIdx),
                new JProperty("LinkId", linkId),
                new JProperty("InputContent", inputContent),
                new JProperty("PlayerId", playerId),
                new JProperty("CustomValue", customValue)
            );

            _ = _ctx.Messenger.SendAsync(MessageClass.Information, "Gui.ShowDialog", json.ToString(Newtonsoft.Json.Formatting.None));
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
                    displayed = _ctx.ModApi.GUI.ShowDialog(config, handler, 0);  // requires Unity main thread (see: UpdateHandler.cs)
                    JObject json = new JObject(new JProperty("Displayed", displayed));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                _ = _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
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
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }
    }


}