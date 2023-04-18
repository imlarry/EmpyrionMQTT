
using System;
using Eleon.Modding;
using Newtonsoft.Json.Linq;
using UnityEngine;
using ESBGameMod;

namespace ModApi
{
    public class Playfield
    {
        private readonly ContextData _ctx;

        public Playfield(ContextData ctx)
        {
            _ctx = ctx;
        }

        public async void SpawnEntity(string topic, string payload)
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
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void SpawnPrefab(string topic, string payload)
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
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void RemoveEntity(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = args.GetValue("EntityId").Value<int>();
                _ctx.ModApi.ClientPlayfield.RemoveEntity(entityId);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void IsStructureDeviceLocked(string topic, string payload)
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
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void MoveEntity(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = args.GetValue("EntityId").Value<int>();
                if (_ctx.LoadedEntity.TryGetValue(entityId, out var amplifierEntity))
                {
                    string posStr = args.GetValue("Pos").ToString();
                    string[] values = posStr.Split(',');
                    Vector3 pos = new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]));
                    // confirm this is actually a flexion amplifier
                    amplifierEntity.Position = pos;
                    JObject json = new JObject(
                            new JProperty("EntityId", entityId),
                            new JProperty("Pos", pos.ToString())
                            );
                    await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
                }
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

    }
}