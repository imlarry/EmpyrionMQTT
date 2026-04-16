using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
{
    public class Lcd
    {
        private readonly ContextData _ctx;

        public Lcd(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Lcd.Get",                 Get);
            _ctx.Messenger.RegisterHandler("V2.Lcd.SetText",             SetText);
            _ctx.Messenger.RegisterHandler("V2.Lcd.SetTextColor",        SetTextColor);
            _ctx.Messenger.RegisterHandler("V2.Lcd.SetBackgroundColor",  SetBackgroundColor);
            _ctx.Messenger.RegisterHandler("V2.Lcd.SetFontSize",         SetFontSize);
        }

        private async Task<Eleon.Modding.IStructure> GetStructure(string topic, int entityId)
        {
            Eleon.Modding.IStructure structure;
            try
            {
                var playfield = _ctx.ModApi.ClientPlayfield;
                if (playfield == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("ClientPlayfield is null -- no playfield loaded on this process"));
                    return null;
                }
                IEntity entity;
                if (!playfield.Entities.TryGetValue(entityId, out entity) || entity == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"Entity {entityId} not found in ClientPlayfield.Entities"));
                    return null;
                }
                structure = entity.Structure;
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} Structure inaccessible: {ex.Message}"));
                return null;
            }
            if (structure == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} has no Structure (not a structure entity)"));
                return null;
            }
            return structure;
        }

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
