using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class Lcd : ILcd
    {
        private readonly ContextData _ctx;

        public Lcd(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("Lcd.Get",                 Get);
            _ctx.Messenger.RegisterHandler("Lcd.SetText",             SetText);
            _ctx.Messenger.RegisterHandler("Lcd.SetTextColor",        SetTextColor);
            _ctx.Messenger.RegisterHandler("Lcd.SetBackgroundColor",  SetBackgroundColor);
            _ctx.Messenger.RegisterHandler("Lcd.SetFontSize",         SetFontSize);
        }

        // Shared lookup: entity from cache → structure → ILcd device at pos, or send exception.
        // GetDevice<T> must be called on the main thread; callers wrap in RunOnMainThread.
        private async Task<Eleon.Modding.IStructure> GetStructure(string topic, int entityId)
        {
            var entity = _ctx.GetEntityByKey(entityId);
            if (entity == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} not found in LoadedEntity cache"));
                return null;
            }
            if (entity.Structure == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} has no Structure (not a structure entity)"));
                return null;
            }
            return entity.Structure;
        }

        // Unity Color ↔ JSON {R,G,B,A}
        private static JObject ColorJson(Color c) => new JObject(
            new JProperty("R", c.r),
            new JProperty("G", c.g),
            new JProperty("B", c.b),
            new JProperty("A", c.a));

        private static Color ParseColor(JToken t) => new Color(
            t["R"]?.Value<float>() ?? 0f,
            t["G"]?.Value<float>() ?? 0f,
            t["B"]?.Value<float>() ?? 0f,
            t["A"]?.Value<float>() ?? 1f);

        // -----------------------------------------------------------------------
        // Lcd.Get — all readable ILcd properties at a position
        // Payload: { "EntityId": n, "Pos": {"X":0,"Y":0,"Z":0} }
        // -----------------------------------------------------------------------
        public async Task Get(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var lcd = structure.GetDevice<Eleon.Modding.ILcd>(pos);
                    if (lcd == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No LCD device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    json = new JObject(
                        new JProperty("EntityId",         entityId),
                        new JProperty("Pos",              MessageHelpers.Vec(pos)),
                        new JProperty("Text",             lcd.GetText() ?? string.Empty),
                        new JProperty("TextColor",        ColorJson(lcd.GetTextColor())),
                        new JProperty("BackgroundColor",  ColorJson(lcd.GetBackgroundColor())),
                        new JProperty("FontSize",         lcd.GetFontSize()));
                    await Task.CompletedTask;
                });

                if (json != null)
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Lcd.SetText — set the displayed text
        // Payload: { "EntityId": n, "Pos": {...}, "Text": "..." }
        // -----------------------------------------------------------------------
        public async Task SetText(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                string text  = args["Text"].Value<string>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var lcd = structure.GetDevice<Eleon.Modding.ILcd>(pos);
                    if (lcd == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No LCD device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    lcd.SetText(text);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Text",     text));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Lcd.SetTextColor — set the text color
        // Payload: { "EntityId": n, "Pos": {...}, "Color": {"R":1.0,"G":1.0,"B":1.0,"A":1.0} }
        // -----------------------------------------------------------------------
        public async Task SetTextColor(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                Color color  = ParseColor(args["Color"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var lcd = structure.GetDevice<Eleon.Modding.ILcd>(pos);
                    if (lcd == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No LCD device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    lcd.SetTextColor(color);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Color",    ColorJson(color)));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Lcd.SetBackgroundColor — set the background color
        // Payload: { "EntityId": n, "Pos": {...}, "Color": {"R":0.0,"G":0.0,"B":0.0,"A":1.0} }
        // -----------------------------------------------------------------------
        public async Task SetBackgroundColor(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                Color color  = ParseColor(args["Color"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var lcd = structure.GetDevice<Eleon.Modding.ILcd>(pos);
                    if (lcd == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No LCD device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    lcd.SetBackgroundColor(color);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Color",    ColorJson(color)));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Lcd.SetFontSize — set the font size
        // Payload: { "EntityId": n, "Pos": {...}, "FontSize": 20 }
        // -----------------------------------------------------------------------
        public async Task SetFontSize(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int fontSize = args["FontSize"].Value<int>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var lcd = structure.GetDevice<Eleon.Modding.ILcd>(pos);
                    if (lcd == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No LCD device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    lcd.SetFontSize(fontSize);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("FontSize", fontSize));
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
