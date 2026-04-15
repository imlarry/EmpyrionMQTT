using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
{
    public class Light
    {
        private readonly ContextData _ctx;

        public Light(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Light.Get",          Get);
            _ctx.Messenger.RegisterHandler("V2.Light.SetColor",     SetColor);
            _ctx.Messenger.RegisterHandler("V2.Light.SetIntensity", SetIntensity);
            _ctx.Messenger.RegisterHandler("V2.Light.SetRange",     SetRange);
            _ctx.Messenger.RegisterHandler("V2.Light.SetLightType", SetLightType);
            _ctx.Messenger.RegisterHandler("V2.Light.SetBlinkData", SetBlinkData);
            _ctx.Messenger.RegisterHandler("V2.Light.SetSpotAngle", SetSpotAngle);
        }

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
                    var light = structure.GetDevice<Eleon.Modding.ILight>(pos);
                    if (light == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Light device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    light.GetBlinkData(out float blinkInterval, out float blinkLength, out float blinkOffset);
                    json = new JObject(
                        new JProperty("EntityId",      entityId),
                        new JProperty("Pos",           MessageHelpers.Vec(pos)),
                        new JProperty("Color",         ColorJson(light.GetColor())),
                        new JProperty("Intensity",     light.GetIntensity()),
                        new JProperty("Range",         light.GetRange()),
                        new JProperty("LightType",     light.GetLightType().ToString()),
                        new JProperty("BlinkInterval", blinkInterval),
                        new JProperty("BlinkLength",   blinkLength),
                        new JProperty("BlinkOffset",   blinkOffset),
                        new JProperty("SpotAngle",     light.GetSpotAngle()));
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

        public async Task SetColor(string topic, string payload)
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
                    var light = structure.GetDevice<Eleon.Modding.ILight>(pos);
                    if (light == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Light device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    light.SetColor(color);
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

        public async Task SetIntensity(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                int entityId  = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                float intensity = args["Intensity"].Value<float>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var light = structure.GetDevice<Eleon.Modding.ILight>(pos);
                    if (light == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Light device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    light.SetIntensity(intensity);
                    var json = new JObject(
                        new JProperty("EntityId",  entityId),
                        new JProperty("Pos",       MessageHelpers.Vec(pos)),
                        new JProperty("Intensity", intensity));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SetRange(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                float range  = args["Range"].Value<float>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var light = structure.GetDevice<Eleon.Modding.ILight>(pos);
                    if (light == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Light device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    light.SetRange(range);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Range",    range));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SetLightType(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                int entityId  = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                var lightType = (LightType)Enum.Parse(typeof(LightType), args["LightType"].Value<string>());

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var light = structure.GetDevice<Eleon.Modding.ILight>(pos);
                    if (light == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Light device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    light.SetLightType(lightType);
                    var json = new JObject(
                        new JProperty("EntityId",  entityId),
                        new JProperty("Pos",       MessageHelpers.Vec(pos)),
                        new JProperty("LightType", lightType.ToString()));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SetBlinkData(string topic, string payload)
        {
            try
            {
                var args          = JObject.Parse(payload);
                int entityId      = args["EntityId"].Value<int>();
                VectorInt3 pos    = MessageHelpers.ParseVecInt3(args["Pos"]);
                float blinkInterval = args["BlinkInterval"].Value<float>();
                float blinkLength   = args["BlinkLength"].Value<float>();
                float blinkOffset   = args["BlinkOffset"].Value<float>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var light = structure.GetDevice<Eleon.Modding.ILight>(pos);
                    if (light == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Light device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    light.SetBlinkData(blinkInterval, blinkLength, blinkOffset);
                    var json = new JObject(
                        new JProperty("EntityId",      entityId),
                        new JProperty("Pos",           MessageHelpers.Vec(pos)),
                        new JProperty("BlinkInterval", blinkInterval),
                        new JProperty("BlinkLength",   blinkLength),
                        new JProperty("BlinkOffset",   blinkOffset));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SetSpotAngle(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                int entityId  = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                float spotAngle = args["SpotAngle"].Value<float>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var light = structure.GetDevice<Eleon.Modding.ILight>(pos);
                    if (light == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Light device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    light.SetSpotAngle(spotAngle);
                    var json = new JObject(
                        new JProperty("EntityId",  entityId),
                        new JProperty("Pos",       MessageHelpers.Vec(pos)),
                        new JProperty("SpotAngle", spotAngle));
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
