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

        // TODO: ClientPlayfield calls are only available in Client mod .. see MoveEntity below for fix, will require _ctx.LoadedPlayfields ref (or can we derive playfield and entity dictionaries via other interface?)
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
        // TODO: ClientPlayfield calls are only available in Client mod .. see MoveEntity below for fix, will require _ctx.LoadedPlayfields ref
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

        // TODO: ClientPlayfield calls are only available in Client mod .. see MoveEntity below for fix, will require _ctx.LoadedEntities lookup
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
        // TODO: ClientPlayfield calls are only available in Client mod .. see MoveEntity below for fix, scan loaded entities for .Structure.Id (ouch!)?
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
        // MoveEntity { "EntityId"="<Id:Int>", "Pos"="<X:Real>,<Y:Real>,<Z:Real>" }
        // moves Entity on the current playfield to Pos
        public async Task MoveEntity(string topic, string payload)
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
                    entityInterface.Position = pos;
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