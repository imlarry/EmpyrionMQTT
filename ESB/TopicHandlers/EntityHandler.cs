using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;
using ESB.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class EntityHandler : TopicHandlerBase
    {
        public EntityHandler(ContextData ctx) : base(ctx) { }

        public void Register()
        {
            _ctx.Bus.OnRequest("Entity", "GetProperties", OnMain(GetProperties));
            _ctx.Bus.OnRequest("Entity", "List",          OnMain(List));
            _ctx.Bus.OnRequest("Entity", "SetPosition",   OnMain(SetPosition));
            _ctx.Bus.OnRequest("Entity", "SetRotation",   OnMain(SetRotation));
            _ctx.Bus.OnRequest("Entity", "DamageEntity",  OnMain(DamageEntity));
            _ctx.Bus.OnRequest("Entity", "Move",          OnMain(Move));
            _ctx.Bus.OnRequest("Entity", "MoveForward",   OnMain(MoveForward));
            _ctx.Bus.OnRequest("Entity", "MoveStop",      OnMain(MoveStop));
        }

        private static readonly string[] ListColumns =
            { "EntityId", "Name", "Type", "FactionId", "FactionGroup", "BelongsTo", "DockedTo", "X", "Y", "Z", "IsPoi" };

        private IEntity GetEntity(int entityId)
        {
            var pf = _ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield;
            if (pf == null)
                throw new InvalidOperationException("No active playfield");
            IEntity entity;
            if (!pf.Entities.TryGetValue(entityId, out entity) || entity == null)
                throw new InvalidOperationException("Entity " + entityId + " not found on current playfield");
            return entity;
        }

        // =========================================================================
        // Entity/GetProperties -- { "EntityId": int }
        // All safe IEntity getters plus HasStructure so callers know whether to
        // switch to Structure/* ops for that EntityId.
        // =========================================================================
        public Task<string> GetProperties(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var entity = GetEntity(entityId);

                JToken S(Func<object> getter)
                {
                    try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
                    catch { return JValue.CreateNull(); }
                }

                var obj = new JObject();
                obj["Id"]           = S(() => entity.Id);
                obj["Name"]         = S(() => entity.Name);
                obj["Type"]         = S(() => entity.Type.ToString());
                obj["Position"]     = S(() => MessageHelpers.Vec(entity.Position));
                obj["Forward"]      = S(() => MessageHelpers.Vec(entity.Forward));
                obj["Rotation"]     = S(() => MessageHelpers.Vec(entity.Rotation));
                obj["IsLocal"]      = S(() => entity.IsLocal);
                obj["IsProxy"]      = S(() => entity.IsProxy);
                obj["IsPoi"]        = S(() => entity.IsPoi);
                obj["Faction"]      = S(() => HandlerHelper.FactionDataJson(entity.Faction));
                obj["BelongsTo"]    = S(() => entity.BelongsTo);
                obj["DockedTo"]     = S(() => entity.DockedTo);
                obj["HasStructure"] = S(() => entity.Structure != null);
                return Task.FromResult(obj.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Entity/List -- (no payload)
        // Tabular enumeration of every entity on the current playfield, richer
        // than Playfield/GetEntities (adds FactionId, FactionGroup, BelongsTo,
        // DockedTo, IsPoi).
        // =========================================================================
        public Task<string> List(MessageEnvelope env)
        {
            try
            {
                var pf = _ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));

                var rows = new JArray();
                foreach (var kv in pf.Entities)
                {
                    var e = kv.Value;
                    if (e == null) continue;
                    int factionId = 0;
                    string factionGroup = null;
                    try { factionId = e.Faction.Id; factionGroup = e.Faction.Group.ToString(); } catch { }
                    var pos = e.Position;
                    rows.Add(new JArray(
                        e.Id,
                        e.Name,
                        e.Type.ToString(),
                        factionId,
                        factionGroup,
                        e.BelongsTo,
                        e.DockedTo,
                        pos.x, pos.y, pos.z,
                        e.IsPoi));
                }
                var json = new JObject(new JProperty("Entities", MessageHelpers.Tabular(ListColumns, rows)));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Entity/SetPosition -- { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public Task<string> SetPosition(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<SetEntityPositionRequest>();
                if (req == null || req.Pos == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Pos is required"));
                var entity = GetEntity(req.EntityId);
                entity.Position = new Vector3(req.Pos.X, req.Pos.Y, req.Pos.Z);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Entity/SetRotation -- { "EntityId": int, "Rot": {X,Y,Z,W} }
        // =========================================================================
        public Task<string> SetRotation(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<SetEntityRotationRequest>();
                if (req == null || req.Rot == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Rot is required"));
                var entity = GetEntity(req.EntityId);
                entity.Rotation = new Quaternion(req.Rot.X, req.Rot.Y, req.Rot.Z, req.Rot.W);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Entity/DamageEntity -- { "EntityId": int, "DamageAmount": int, "DamageType": int }
        // =========================================================================
        public Task<string> DamageEntity(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<DamageEntityRequest>();
                var entity = GetEntity(req.EntityId);
                entity.DamageEntity(req.DamageAmount, req.DamageType);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Entity/Move -- { "EntityId": int, "Direction": {X,Y,Z} }
        // =========================================================================
        public Task<string> Move(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<MoveEntityRequest>();
                if (req == null || req.Direction == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Direction is required"));
                var entity = GetEntity(req.EntityId);
                entity.Move(new Vector3(req.Direction.X, req.Direction.Y, req.Direction.Z));
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Entity/MoveForward -- { "EntityId": int, "Speed": float }
        // =========================================================================
        public Task<string> MoveForward(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<MoveForwardRequest>();
                var entity = GetEntity(req.EntityId);
                entity.MoveForward(req.Speed);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Entity/MoveStop -- { "EntityId": int }
        // =========================================================================
        public Task<string> MoveStop(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<EntityIdRequest>();
                var entity = GetEntity(req.EntityId);
                entity.MoveStop();
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
