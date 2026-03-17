using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class Light : ILight
    {
        private readonly ContextData _ctx;

        public Light(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("Light.Get",          Get);
            _ctx.Messenger.RegisterHandler("Light.SetColor",     SetColor);
            _ctx.Messenger.RegisterHandler("Light.SetIntensity", SetIntensity);
            _ctx.Messenger.RegisterHandler("Light.SetRange",     SetRange);
            _ctx.Messenger.RegisterHandler("Light.SetLightType", SetLightType);
            _ctx.Messenger.RegisterHandler("Light.SetBlinkData", SetBlinkData);
            _ctx.Messenger.RegisterHandler("Light.SetSpotAngle", SetSpotAngle);
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

        // -----------------------------------------------------------------------
        // Light.Get — all readable ILight properties at a position
        // Payload: { "EntityId": n, "Pos": {"X":0,"Y":0,"Z":0} }
        // Response: Color {R,G,B,A}, Intensity, Range, LightType (string),
        //           BlinkInterval, BlinkLength, BlinkOffset, SpotAngle
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

        // -----------------------------------------------------------------------
        // Light.SetColor — set the light color
        // Payload: { "EntityId": n, "Pos": {...}, "Color": {"R":1.0,"G":1.0,"B":1.0,"A":1.0} }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Light.SetIntensity — set the light intensity
        // Payload: { "EntityId": n, "Pos": {...}, "Intensity": 1.0 }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Light.SetRange — set the light range
        // Payload: { "EntityId": n, "Pos": {...}, "Range": 10.0 }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Light.SetLightType — set the light type
        // Payload: { "EntityId": n, "Pos": {...}, "LightType": "Point" }
        // LightType values (from Light.Get): e.g. "Point", "Spot", "Directional"
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Light.SetBlinkData — set blink timing parameters
        // Payload: { "EntityId": n, "Pos": {...}, "BlinkInterval": 1.0,
        //           "BlinkLength": 0.5, "BlinkOffset": 0.0 }
        // Set all three to 0.0 to disable blinking.
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Light.SetSpotAngle — set the spotlight cone angle (degrees)
        // Payload: { "EntityId": n, "Pos": {...}, "SpotAngle": 45.0 }
        // Only meaningful when LightType is "Spot".
        // -----------------------------------------------------------------------
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
