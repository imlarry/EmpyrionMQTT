using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class Block : IBlock
    {
        private readonly ContextData _ctx;

        public Block(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("Block.Get",                     Get);
            _ctx.Messenger.RegisterHandler("Block.Set",                     Set);
            _ctx.Messenger.RegisterHandler("Block.SetDamage",               SetDamage);
            _ctx.Messenger.RegisterHandler("Block.GetTextures",             GetTextures);
            _ctx.Messenger.RegisterHandler("Block.SetTextures",             SetTextures);
            _ctx.Messenger.RegisterHandler("Block.SetTextureForWholeBlock", SetTextureForWholeBlock);
            _ctx.Messenger.RegisterHandler("Block.GetColors",               GetColors);
            _ctx.Messenger.RegisterHandler("Block.SetColors",               SetColors);
            _ctx.Messenger.RegisterHandler("Block.SetColorForWholeBlock",   SetColorForWholeBlock);
            _ctx.Messenger.RegisterHandler("Block.GetSwitchState",          GetSwitchState);
            _ctx.Messenger.RegisterHandler("Block.SetSwitchState",          SetSwitchState);
            _ctx.Messenger.RegisterHandler("Block.SetLockCode",             SetLockCode);
        }

        // Shared lookup: entity from cache → IStructure, or send an exception and return null.
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

        // Parse nullable int from a JObject key — absent or null → null.
        private static int? NullableInt(JObject args, string key)
            => args[key] == null || args[key].Type == JTokenType.Null
                ? (int?)null
                : args[key].Value<int>();

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
        // Block.Get — all readable IBlock properties at a position
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

        // -----------------------------------------------------------------------
        // Block.Set — mutate type/shape/rotation/active (all nullable — omit to leave unchanged)
        // Payload: { "EntityId": n, "Pos": {...}, "Type": t?, "Shape": s?, "Rotation": r?, "Active": b? }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.SetDamage — set damage value on a block (0 = fully repaired)
        // Payload: { "EntityId": n, "Pos": {...}, "Damage": d }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.GetTextures — per-side texture indices (Top/Bottom/North/South/West/East)
        // Payload: { "EntityId": n, "Pos": {...} }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.SetTextures — per-side texture indices (any side may be omitted)
        // Payload: { "EntityId": n, "Pos": {...}, "Top": t?, "Bottom": b?, "North": n?, "South": s?, "West": w?, "East": e? }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.SetTextureForWholeBlock — apply one texture index to all six sides
        // Payload: { "EntityId": n, "Pos": {...}, "TextureIndex": i }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.GetColors — per-side color indices (Top/Bottom/North/South/West/East)
        // Payload: { "EntityId": n, "Pos": {...} }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.SetColors — per-side color indices (any side may be omitted)
        // Payload: { "EntityId": n, "Pos": {...}, "Top": t?, "Bottom": b?, "North": n?, "South": s?, "West": w?, "East": e? }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.SetColorForWholeBlock — apply one color index to all six sides
        // Payload: { "EntityId": n, "Pos": {...}, "ColorIndex": i }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.GetSwitchState — switch/lever blocks only
        // Payload: { "EntityId": n, "Pos": {...}, "Index": 0? }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.SetSwitchState — switch/lever blocks only; returns new state
        // Payload: { "EntityId": n, "Pos": {...}, "State": true/false, "Index": 0? }
        // -----------------------------------------------------------------------
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

        // -----------------------------------------------------------------------
        // Block.SetLockCode — set or clear the lock code on a device block
        // Payload: { "EntityId": n, "Pos": {...}, "LockCode": 1234 }  (null = clear)
        // -----------------------------------------------------------------------
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
