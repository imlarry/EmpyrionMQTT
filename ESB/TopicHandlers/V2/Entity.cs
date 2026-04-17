using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
{
    [Flags]
    enum EntityProp : long
    {
        Id        = 1L << 0,
        Name      = 1L << 1,
        Type      = 1L << 2,
        Position  = 1L << 3,
        Rotation  = 1L << 4,
        Forward   = 1L << 5,
        Faction   = 1L << 6,
        IsLocal   = 1L << 7,
        IsProxy   = 1L << 8,
        IsPoi     = 1L << 9,
        BelongsTo = 1L << 10,
        DockedTo  = 1L << 11,
        Structure = 1L << 12,
        All       = ~0L
    }

    struct EntityPropDef
    {
        public string Name;
        public EntityProp Flag;
        public Func<IEntity, JToken> Get;
    }

    public class Entity
    {
        private readonly ContextData _ctx;

        public Entity(ContextData ctx) { _ctx = ctx; }

        static JToken Vec3(Vector3 v)    => new JObject(new JProperty("X", v.x), new JProperty("Y", v.y), new JProperty("Z", v.z));
        static JToken Vec4(Quaternion q) => new JObject(new JProperty("X", q.x), new JProperty("Y", q.y), new JProperty("Z", q.z), new JProperty("W", q.w));
        static JToken FD(FactionData fd) => new JObject(new JProperty("Group", fd.Group.ToString()), new JProperty("Id", fd.Id));

        static readonly EntityPropDef[] Props = new EntityPropDef[]
        {
            new EntityPropDef { Name = "Id",        Flag = EntityProp.Id,        Get = e => JToken.FromObject(e.Id) },
            new EntityPropDef { Name = "Name",      Flag = EntityProp.Name,      Get = e => JToken.FromObject(e.Name) },
            new EntityPropDef { Name = "Type",      Flag = EntityProp.Type,      Get = e => JToken.FromObject(e.Type.ToString()) },
            new EntityPropDef { Name = "Position",  Flag = EntityProp.Position,  Get = e => Vec3(e.Position) },
            new EntityPropDef { Name = "Rotation",  Flag = EntityProp.Rotation,  Get = e => Vec4(e.Rotation) },
            new EntityPropDef { Name = "Forward",   Flag = EntityProp.Forward,   Get = e => Vec3(e.Forward) },
            new EntityPropDef { Name = "Faction",   Flag = EntityProp.Faction,   Get = e => FD(e.Faction) },
            new EntityPropDef { Name = "IsLocal",   Flag = EntityProp.IsLocal,   Get = e => JToken.FromObject(e.IsLocal) },
            new EntityPropDef { Name = "IsProxy",   Flag = EntityProp.IsProxy,   Get = e => JToken.FromObject(e.IsProxy) },
            new EntityPropDef { Name = "IsPoi",     Flag = EntityProp.IsPoi,     Get = e => JToken.FromObject(e.IsPoi) },
            new EntityPropDef { Name = "BelongsTo", Flag = EntityProp.BelongsTo, Get = e => JToken.FromObject(e.BelongsTo) },
            new EntityPropDef { Name = "DockedTo",  Flag = EntityProp.DockedTo,  Get = e => JToken.FromObject(e.DockedTo) },
            new EntityPropDef { Name = "Structure", Flag = EntityProp.Structure,
                Get = e => e.Structure != null
                    ? (JToken)new JObject(new JProperty("Id", e.Structure.Id), new JProperty("IsReady", e.Structure.IsReady))
                    : JValue.CreateNull() },
        };

        static readonly string[] ValidPropertyNames = Props.Select(d => d.Name).ToArray();

        static bool TryParseMask(JArray requested, out EntityProp mask, out List<string> invalidNames)
        {
            mask = 0;
            invalidNames = null;
            foreach (var token in requested)
            {
                var name = token.Value<string>();
                bool found = false;
                foreach (var def in Props)
                {
                    if (def.Name == name) { mask |= def.Flag; found = true; break; }
                }
                if (!found)
                    (invalidNames ?? (invalidNames = new List<string>())).Add(name);
            }
            return invalidNames == null;
        }

        private IEntity FindEntity(int entityId)
        {
            var pf = _ctx.ModApi.ClientPlayfield;
            if (pf == null) return null;
            IEntity entity;
            pf.Entities.TryGetValue(entityId, out entity);
            return entity;
        }

        // -------------------------------------------------------------------------
        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Entity",              Properties);
            _ctx.Messenger.RegisterHandler("V2.Entity.DamageEntity", DamageEntity);
            _ctx.Messenger.RegisterHandler("V2.Entity.Move",         Move);
            _ctx.Messenger.RegisterHandler("V2.Entity.MoveForward",  MoveForward);
            _ctx.Messenger.RegisterHandler("V2.Entity.MoveStop",     MoveStop);
        }

        // -------------------------------------------------------------------------
        // V2.Entity — property getter
        //   payload: { "EntityId": int }
        //         or { "EntityId": int, "Properties": ["Name", "Position", ...] }
        // -------------------------------------------------------------------------
        public async Task Properties(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                if (args["EntityId"] == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("EntityId is required"));
                    return;
                }
                int entityId = Convert.ToInt32(args["EntityId"]);

                EntityProp mask = EntityProp.All;
                var propArray = args["Properties"] as JArray;
                if (propArray != null)
                {
                    if (!TryParseMask(propArray, out mask, out var invalid))
                    {
                        var errObj = new JObject(
                            new JProperty("Error",             "InvalidProperty"),
                            new JProperty("InvalidProperties", new JArray(invalid)),
                            new JProperty("ValidProperties",   new JArray(ValidPropertyNames)));
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            errObj.ToString(Formatting.None));
                        return;
                    }
                }

                var entity = FindEntity(entityId);
                if (entity == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"Entity {entityId} not found in ClientPlayfield.Entities"));
                    return;
                }

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    json = new JObject();
                    foreach (var def in Props)
                    {
                        if ((mask & def.Flag) != 0)
                        {
                            try   { json[def.Name] = def.Get(entity); }
                            catch { json[def.Name] = JValue.CreateNull(); }
                        }
                    }
                    await Task.CompletedTask;
                });

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                    json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V2.Entity.DamageEntity
        //   payload: { "EntityId": int, "DamageAmount": int, "DamageType": int }
        // -------------------------------------------------------------------------
        public async Task DamageEntity(string topic, string payload)
        {
            try
            {
                JObject args     = JObject.Parse(payload);
                int entityId     = Convert.ToInt32(args["EntityId"]);
                int damageAmount = args["DamageAmount"]?.Value<int>() ?? 0;
                int damageType   = args["DamageType"]?.Value<int>()   ?? 0;

                var entity = FindEntity(entityId);
                if (entity == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"Entity {entityId} not found in ClientPlayfield.Entities"));
                    return;
                }

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    entity.DamageEntity(damageAmount, damageType);
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                        new JObject(new JProperty("DamageEntity", true)).ToString(Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V2.Entity.Move
        //   payload: { "EntityId": int, "Direction": {"X":0,"Y":0,"Z":0} }
        // -------------------------------------------------------------------------
        public async Task Move(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = Convert.ToInt32(args["EntityId"]);

                if (args["Direction"] == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("Direction argument is required"));
                    return;
                }

                Vector3 direction = MessageHelpers.ParseVec3(args["Direction"]);

                var entity = FindEntity(entityId);
                if (entity == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"Entity {entityId} not found in ClientPlayfield.Entities"));
                    return;
                }

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    entity.Move(direction);
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                        new JObject(new JProperty("Move", true)).ToString(Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V2.Entity.MoveForward
        //   payload: { "EntityId": int, "Speed": float }
        // -------------------------------------------------------------------------
        public async Task MoveForward(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = Convert.ToInt32(args["EntityId"]);
                float speed  = args["Speed"]?.Value<float>() ?? 1f;

                var entity = FindEntity(entityId);
                if (entity == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"Entity {entityId} not found in ClientPlayfield.Entities"));
                    return;
                }

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    entity.MoveForward(speed);
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                        new JObject(new JProperty("MoveForward", true)).ToString(Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V2.Entity.MoveStop
        //   payload: { "EntityId": int }
        // -------------------------------------------------------------------------
        public async Task MoveStop(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = Convert.ToInt32(args["EntityId"]);

                var entity = FindEntity(entityId);
                if (entity == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"Entity {entityId} not found in ClientPlayfield.Entities"));
                    return;
                }

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    entity.MoveStop();
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                        new JObject(new JProperty("MoveStop", true)).ToString(Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
