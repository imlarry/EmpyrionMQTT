using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
{
    public class Block
    {
        private readonly ContextData _ctx;

        public Block(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Block.Get",                     Get);
            _ctx.Messenger.RegisterHandler("V2.Block.Set",                     Set);
            _ctx.Messenger.RegisterHandler("V2.Block.SetDamage",               SetDamage);
            _ctx.Messenger.RegisterHandler("V2.Block.GetTextures",             GetTextures);
            _ctx.Messenger.RegisterHandler("V2.Block.SetTextures",             SetTextures);
            _ctx.Messenger.RegisterHandler("V2.Block.SetTextureForWholeBlock", SetTextureForWholeBlock);
            _ctx.Messenger.RegisterHandler("V2.Block.GetColors",               GetColors);
            _ctx.Messenger.RegisterHandler("V2.Block.SetColors",               SetColors);
            _ctx.Messenger.RegisterHandler("V2.Block.SetColorForWholeBlock",   SetColorForWholeBlock);
            _ctx.Messenger.RegisterHandler("V2.Block.GetSwitchState",          GetSwitchState);
            _ctx.Messenger.RegisterHandler("V2.Block.SetSwitchState",          SetSwitchState);
            _ctx.Messenger.RegisterHandler("V2.Block.SetLockCode",             SetLockCode);
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

        private static int? NullableInt(JObject args, string key)
            => args[key] == null || args[key].Type == JTokenType.Null
                ? (int?)null
                : args[key].Value<int>();

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
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        json = new JObject(
                            new JProperty("EntityId", entityId),
                            new JProperty("Pos",      MessageHelpers.Vec(pos)),
                            new JProperty("Block",    null));
                    }
                    else
                    {
                        block.Get(out int type, out int shape, out int rotation, out bool active);
                        json = new JObject(
                            new JProperty("EntityId",   entityId),
                            new JProperty("Pos",        MessageHelpers.Vec(pos)),
                            new JProperty("Type",       type),
                            new JProperty("Shape",      shape),
                            new JProperty("Rotation",   rotation),
                            new JProperty("Active",     active),
                            new JProperty("Damage",     block.GetDamage()),
                            new JProperty("HitPoints",  block.GetHitPoints()),
                            new JProperty("LockCode",   block.LockCode),
                            new JProperty("CustomName", block.CustomName));
                    }
                    await Task.CompletedTask;
                });

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task Set(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                int? type     = NullableInt(args, "Type");
                int? shape    = NullableInt(args, "Shape");
                int? rotation = NullableInt(args, "Rotation");
                bool? active  = args["Active"] == null || args["Active"].Type == JTokenType.Null
                    ? (bool?)null
                    : args["Active"].Value<bool>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.Set(type, shape, rotation, active);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Type",     type),
                        new JProperty("Shape",    shape),
                        new JProperty("Rotation", rotation),
                        new JProperty("Active",   active));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SetDamage(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int damage   = args["Damage"].Value<int>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.SetDamage(damage);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Damage",   damage));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetTextures(string topic, string payload)
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
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.GetTextures(out int top, out int bottom, out int north, out int south, out int west, out int east);
                    json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Top",      top),
                        new JProperty("Bottom",   bottom),
                        new JProperty("North",    north),
                        new JProperty("South",    south),
                        new JProperty("West",     west),
                        new JProperty("East",     east));
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

        public async Task SetTextures(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                int? top    = NullableInt(args, "Top");
                int? bottom = NullableInt(args, "Bottom");
                int? north  = NullableInt(args, "North");
                int? south  = NullableInt(args, "South");
                int? west   = NullableInt(args, "West");
                int? east   = NullableInt(args, "East");

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.SetTextures(top, bottom, north, south, west, east);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Top",      top),
                        new JProperty("Bottom",   bottom),
                        new JProperty("North",    north),
                        new JProperty("South",    south),
                        new JProperty("West",     west),
                        new JProperty("East",     east));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SetTextureForWholeBlock(string topic, string payload)
        {
            try
            {
                var args         = JObject.Parse(payload);
                int entityId     = args["EntityId"].Value<int>();
                VectorInt3 pos   = MessageHelpers.ParseVecInt3(args["Pos"]);
                int textureIndex = args["TextureIndex"].Value<int>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.SetTextureForWholeBlock(textureIndex);
                    var json = new JObject(
                        new JProperty("EntityId",     entityId),
                        new JProperty("Pos",          MessageHelpers.Vec(pos)),
                        new JProperty("TextureIndex", textureIndex));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetColors(string topic, string payload)
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
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.GetColors(out int top, out int bottom, out int north, out int south, out int west, out int east);
                    json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Top",      top),
                        new JProperty("Bottom",   bottom),
                        new JProperty("North",    north),
                        new JProperty("South",    south),
                        new JProperty("West",     west),
                        new JProperty("East",     east));
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

        public async Task SetColors(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                int? top    = NullableInt(args, "Top");
                int? bottom = NullableInt(args, "Bottom");
                int? north  = NullableInt(args, "North");
                int? south  = NullableInt(args, "South");
                int? west   = NullableInt(args, "West");
                int? east   = NullableInt(args, "East");

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.SetColors(top, bottom, north, south, west, east);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Top",      top),
                        new JProperty("Bottom",   bottom),
                        new JProperty("North",    north),
                        new JProperty("South",    south),
                        new JProperty("West",     west),
                        new JProperty("East",     east));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SetColorForWholeBlock(string topic, string payload)
        {
            try
            {
                var args       = JObject.Parse(payload);
                int entityId   = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int colorIndex = args["ColorIndex"].Value<int>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.SetColorForWholeBlock(colorIndex);
                    var json = new JObject(
                        new JProperty("EntityId",   entityId),
                        new JProperty("Pos",        MessageHelpers.Vec(pos)),
                        new JProperty("ColorIndex", colorIndex));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetSwitchState(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int index    = args["Index"]?.Value<int>() ?? 0;

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    bool? state = block.GetSwitchState(index);
                    json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Index",    index),
                        new JProperty("State",    state.HasValue ? (JToken)state.Value : JValue.CreateNull()));
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

        public async Task SetSwitchState(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                bool newState  = args["State"].Value<bool>();
                int index      = args["Index"]?.Value<int>() ?? 0;

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    bool? result = block.SetSwitchState(newState, index);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Index",    index),
                        new JProperty("State",    result.HasValue ? (JToken)result.Value : JValue.CreateNull()));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SetLockCode(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int? lockCode  = NullableInt(args, "LockCode");

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var block = structure.GetBlock(pos);
                    if (block == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No block at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    block.LockCode = lockCode;
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("LockCode", lockCode));
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
