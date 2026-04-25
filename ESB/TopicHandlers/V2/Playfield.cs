using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

// TEST IF await _ctx.MainThreadRunner.RunOnMainThread(async () => {...} required for SpawnEntity, SpawnPrefab, RemoveEntity, MoveEntity (IEntity.Position setter)
namespace ESB.TopicHandlers.V2
{
    public class Playfield
    {

        private readonly ContextData _ctx;

        public Playfield(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Playfield.SpawnEntity",             SpawnEntity);
            _ctx.Messenger.RegisterHandler("V2.Playfield.SpawnPrefab",             SpawnPrefab);
            _ctx.Messenger.RegisterHandler("V2.Playfield.RemoveEntity",            RemoveEntity);
            _ctx.Messenger.RegisterHandler("V2.Playfield.IsStructureDeviceLocked", IsStructureDeviceLocked);
            _ctx.Messenger.RegisterHandler("V2.Playfield.MoveEntity",              MoveEntity);
            _ctx.Messenger.RegisterHandler("V2.Playfield.Info",                    Info);
        }

        public async Task SpawnEntity(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                string entityType = args.GetValue("EntityType").ToString();
                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
                Quaternion rot = new Quaternion(0.0f, 0.0f, 0.0f, 1);
                int entityId = _ctx.GameManager.CurrentPlayfield.SpawnEntity(entityType, pos, rot);
                JObject json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("EntityType", entityType));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SpawnPrefab(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                string prefabName = args.GetValue("PrefabName").ToString();
                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
                int entityId = _ctx.GameManager.CurrentPlayfield.SpawnPrefab(prefabName, pos);
                JObject json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("Pos", MessageHelpers.Vec(pos)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task RemoveEntity(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = args.GetValue("EntityId").Value<int>();
                _ctx.GameManager.CurrentPlayfield.RemoveEntity(entityId);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, new JObject(new JProperty("EntityId", entityId)).ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task IsStructureDeviceLocked(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int structureId = args.GetValue("StructureId").Value<int>();
                VectorInt3 posInStructure = MessageHelpers.ParseVecInt3(args["PosInStructure"]);
                bool isLocked = _ctx.GameManager.CurrentPlayfield.IsStructureDeviceLocked(structureId, posInStructure);
                JObject json = new JObject(
                    new JProperty("StructureId", structureId),
                    new JProperty("PosInStructure", MessageHelpers.Vec(posInStructure)),
                    new JProperty("IsStructureDeviceLocked", isLocked));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task Info(string topic, string payload)
        {
            try
            {
                var pf = _ctx.GameManager.CurrentPlayfield;
                JToken S(Func<object> getter)
                {
                    try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
                    catch { return JValue.CreateNull(); }
                }
                var coord = pf.SolarSystemCoordinates;
                JObject json = new JObject(
                    new JProperty("Name",                   S(() => pf.Name)),
                    new JProperty("PlayfieldType",          S(() => pf.PlayfieldType)),
                    new JProperty("PlanetType",             S(() => pf.PlanetType)),
                    new JProperty("PlanetClass",            S(() => pf.PlanetClass)),
                    new JProperty("SolarSystemName",        S(() => pf.SolarSystemName)),
                    new JProperty("SolarSystemCoordinates", new JObject(
                        new JProperty("X", coord.x),
                        new JProperty("Y", coord.y),
                        new JProperty("Z", coord.z))),
                    new JProperty("IsPvP",                  S(() => pf.IsPvP)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Not yet implemented: AddVoxelArea, MoveVoxelArea, RemoveVoxelArea, SpawnTestPlayer, RemoveTestPlayer,
        //   GetTerrainHeightAt, Players[get], Entities[get], LockStructureDevice (requires async callback)

        // *********************************************************************************************************************
        // IEntity interface
        public async Task MoveEntity(string topic, string payload)  // this is actually IEntity.Pos[set]
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = args.GetValue("EntityId").Value<int>();
                var pf = _ctx.GameManager.CurrentPlayfield;
                IEntity entityInterface;
                if (pf == null || !pf.Entities.TryGetValue(entityId, out entityInterface) || entityInterface == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson($"Entity {entityId} not found in CurrentPlayfield.Entities"));
                    return;
                }

                entityInterface.Position = MessageHelpers.ParseVec3(args["Pos"]);
                JObject json = new JObject(
                    new JProperty("EntityId", entityInterface.Id),
                    new JProperty("Pos", MessageHelpers.Vec(entityInterface.Position)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

    }
}
