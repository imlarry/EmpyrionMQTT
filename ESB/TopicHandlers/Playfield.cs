using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class Playfield : IPlayfield
    {

        private readonly ContextData _ctx;

        public Playfield(ContextData ctx)
        {
            _ctx = ctx;
        }
        
        public void Register()
        {
            _ctx.Messenger.RegisterHandler("Playfield.SpawnEntity",             SpawnEntity);
            _ctx.Messenger.RegisterHandler("Playfield.SpawnPrefab",             SpawnPrefab);
            _ctx.Messenger.RegisterHandler("Playfield.RemoveEntity",            RemoveEntity);
            _ctx.Messenger.RegisterHandler("Playfield.IsStructureDeviceLocked", IsStructureDeviceLocked);
            _ctx.Messenger.RegisterHandler("Playfield.MoveEntity",              MoveEntity);
            _ctx.Messenger.RegisterHandler("Playfield.Info",                    Info);
        }

        public async Task SpawnEntity(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                var entityType = args.GetValue("EntityType").ToString();
                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
                Quaternion rot = new Quaternion(0.0f, 0.0f, 0.0f, 1);
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var entityId = _ctx.ModApi.ClientPlayfield.SpawnEntity(entityType, pos, rot);
                    JObject json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("EntityType", entityType));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
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
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var entityId = _ctx.ModApi.ClientPlayfield.SpawnPrefab(prefabName, pos);
                    JObject json = new JObject(
                            new JProperty("EntityId", entityId),
                            new JProperty("Pos", MessageHelpers.Vec(pos))
                            );
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
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
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.ClientPlayfield.RemoveEntity(entityId);
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, new JObject(new JProperty("EntityId", entityId)).ToString(Newtonsoft.Json.Formatting.None));
                });
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
                bool isLocked = false;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    isLocked = _ctx.ModApi.ClientPlayfield.IsStructureDeviceLocked(structureId, posInStructure);
                    await Task.CompletedTask;
                });
                JObject json = new JObject(
                        new JProperty("StructureId", structureId),
                        new JProperty("PosInStructure", MessageHelpers.Vec(posInStructure)),
                        new JProperty("IsStructureDeviceLocked", isLocked)
                        );
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
                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    JToken S(Func<object> getter)
                    {
                        try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
                        catch { return JValue.CreateNull(); }
                    }

                    var pf = _ctx.ModApi.ClientPlayfield;
                    json = new JObject();
                    json.Add("Name",                  S(() => pf.Name));
                    json.Add("PlayfieldType",         S(() => pf.PlayfieldType));
                    json.Add("PlanetType",            S(() => pf.PlanetType));
                    json.Add("PlanetClass",           S(() => pf.PlanetClass));
                    json.Add("SolarSystemName",       S(() => pf.SolarSystemName));
                    json.Add("SolarSystemCoordinates",S(() => new JObject(
                                                          new JProperty("X", pf.SolarSystemCoordinates.x),
                                                          new JProperty("Y", pf.SolarSystemCoordinates.y),
                                                          new JProperty("Z", pf.SolarSystemCoordinates.z))));
                    json.Add("IsPvP",                 S(() => pf.IsPvP));
                    await Task.CompletedTask;
                });
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
        // IEntity interface .. only works in single player client mode
        public async Task MoveEntity(string topic, string payload)  // this is actually IEntity.Pos[set]
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = args.GetValue("EntityId").Value<int>();
                var entityInterface = _ctx.GetEntityByKey(entityId);
                if (entityInterface == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson($"Entity {entityId} not found in LoadedEntity cache"));
                    return;
                }

                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    entityInterface.Position = pos;
                    JObject json = new JObject(
                            new JProperty("EntityId", entityInterface.Id),
                            new JProperty("Pos", MessageHelpers.Vec(entityInterface.Position))
                            );
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