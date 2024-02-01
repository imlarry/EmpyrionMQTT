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
        
        public async Task Subscribe()
        {
            await _ctx.Messenger.SubscribeAsync("Playfield.SpawnEntity", SpawnEntity);
            await _ctx.Messenger.SubscribeAsync("Playfield.SpawnPrefab", SpawnPrefab);
            await _ctx.Messenger.SubscribeAsync("Playfield.RemoveEntity", RemoveEntity);
            await _ctx.Messenger.SubscribeAsync("Playfield.IsStructureDeviceLocked", IsStructureDeviceLocked);
            await _ctx.Messenger.SubscribeAsync("Playfield.MoveEntity", MoveEntity);
        }

        public async Task SpawnEntity(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                var entityType = args.GetValue("EntityType").ToString();
                string posStr = args.GetValue("Pos").ToString();
                string rotStr = args.GetValue("Rot").ToString();
                string[] values = posStr.Split(',');
                Vector3 pos = new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]));
                Quaternion rot = new Quaternion(0.0f, 0.0f, 0.0f, 1);
                var entityId = _ctx.ModApi.ClientPlayfield.SpawnEntity(entityType, pos, rot);
                JObject json = new JObject(new JProperty("EntityType", entityType));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task SpawnPrefab(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                string prefabName = args.GetValue("PrefabName").ToString();
                string posStr = args.GetValue("Pos").ToString();
                string[] values = posStr.Split(',');
                Vector3 pos = new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]));
                var entityId = _ctx.ModApi.ClientPlayfield.SpawnPrefab(prefabName, pos);
                JObject json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos", pos.ToString())
                        );
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task RemoveEntity(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = args.GetValue("EntityId").Value<int>();
                _ctx.ModApi.ClientPlayfield.RemoveEntity(entityId);
                // TODO: add response json send
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task IsStructureDeviceLocked(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int structureId = args.GetValue("StructureId").Value<int>();
                string posStr = args.GetValue("PosInStructure").ToString();
                string[] values = posStr.Split(',');
                VectorInt3 posInStructure = new VectorInt3(int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]));
                bool isLocked = _ctx.ModApi.ClientPlayfield.IsStructureDeviceLocked(structureId, posInStructure);
                JObject json = new JObject(
                        new JProperty("StructureId", structureId.ToString()),
                        new JProperty("PosInStructure", posInStructure.ToString()),
                        new JProperty("IsStructureDeviceLocked", isLocked.ToString())
                        );
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        //int AddVoxelArea(Vector3 pos, int sizeInMeter)
        //bool MoveVoxelArea(int id, Vector3 pos)
        //bool RemoveVoxelArea(int id)
        //int SpawnTestPlayer(Vector3 pos)
        //bool RemoveTestPlayer (int entityId)
        //float GetTerrainHeightAt (float x, float z)
        //string Name[get]
        //string PlayfieldType[get]
        //string PlanetType[get]
        //string PlanetClass[get]
        //string SolarSystemName[get]
        //VectorInt3 SolarSystemCoordinates[get]
        //bool IsPvP[get]
        //Dictionary< int, IPlayer > Players[get]
        //Dictionary<int, IEntity> Entities[get]

        // *********************************************************************************************************************
        // IEntity interface .. only works in single player client mode
        public async Task MoveEntity(string topic, string payload)  // this is actually IEntity.Pos[set]
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = args.GetValue("EntityId").Value<int>();
                var entityInterface = _ctx.GetEntityByKey(entityId);
                if (entityInterface != null)
                {
                    string posStr = args.GetValue("Pos").ToString();
                    string[] values = posStr.Split(',');
                    Vector3 pos = new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]));
                    entityInterface.Position = pos; // the actual move
                    JObject json = new JObject(
                            new JProperty("EntityId", entityInterface.Id),
                            new JProperty("Pos", entityInterface.Position.ToString())
                            );
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                }
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

    }
}