using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class PlayfieldHandler
    {
        private readonly ContextData _ctx;

        public PlayfieldHandler(ContextData ctx) { _ctx = ctx; }

        public void Register()
        {
            _ctx.Bus.OnRequest("Playfield", "GetEntities",       GetEntities);
            _ctx.Bus.OnRequest("Playfield", "GetPlayers",        GetPlayers);
            _ctx.Bus.OnRequest("Playfield", "GetTerrainHeight",  GetTerrainHeight);
            _ctx.Bus.OnRequest("Playfield", "SpawnEntity",       SpawnEntity);
            _ctx.Bus.OnRequest("Playfield", "RemoveEntity",      RemoveEntity);
        }

        private IPlayfield CurrentPlayfield =>
            _ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield;

        // =========================================================================
        // Playfield/GetEntities -- (no payload)
        // Returns array of entity descriptors on the current playfield.
        // =========================================================================
        public Task<string> GetEntities(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));

                var arr = new JArray();
                foreach (var kv in pf.Entities)
                {
                    var e = kv.Value;
                    if (e == null) continue;
                    arr.Add(new JObject(
                        new JProperty("EntityId",  e.Id),
                        new JProperty("Name",      e.Name),
                        new JProperty("EntityType",e.Type.ToString()),
                        new JProperty("Position",  MessageHelpers.Vec(e.Position)),
                        new JProperty("IsProxy",   e.IsProxy)));
                }
                var json = new JObject(new JProperty("Entities", arr));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/GetPlayers -- (no payload)
        // Returns array of player descriptors on the current playfield.
        // =========================================================================
        public Task<string> GetPlayers(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));

                var arr = new JArray();
                foreach (var kv in pf.Players)
                {
                    var p = kv.Value;
                    if (p == null) continue;
                    arr.Add(new JObject(
                        new JProperty("EntityId", p.Id),
                        new JProperty("Name",     p.Name),
                        new JProperty("SteamId",  p.SteamId)));
                }
                var json = new JObject(new JProperty("Players", arr));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/GetTerrainHeight -- { "X": float, "Z": float }
        // =========================================================================
        public Task<string> GetTerrainHeight(MessageEnvelope env)
        {
            try
            {
                var args = env.PayloadJson;
                float x  = (float)args["X"];
                float z  = (float)args["Z"];
                var pf   = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                float height = pf.GetTerrainHeightAt(x, z);
                var json = new JObject(
                    new JProperty("X",      x),
                    new JProperty("Z",      z),
                    new JProperty("Height", height));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/SpawnEntity -- { "EntityType": string, "Pos": {X,Y,Z}, "Rot": {X,Y,Z,W} }
        // Returns: { "EntityId": int }
        // =========================================================================
        public Task<string> SpawnEntity(MessageEnvelope env)
        {
            try
            {
                var args       = env.PayloadJson;
                string entType = (string)args["EntityType"];
                if (string.IsNullOrEmpty(entType))
                    return Task.FromResult(MessageHelpers.ErrorJson("EntityType is required"));
                var posJ = args["Pos"];
                var rotJ = args["Rot"];
                if (posJ == null) return Task.FromResult(MessageHelpers.ErrorJson("Pos is required"));
                if (rotJ == null) return Task.FromResult(MessageHelpers.ErrorJson("Rot is required"));
                var pos = MessageHelpers.ParseVec3(posJ);
                var rot = new Quaternion((float)rotJ["X"], (float)rotJ["Y"], (float)rotJ["Z"], (float)rotJ["W"]);
                var pf  = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                int entityId = pf.SpawnEntity(entType, pos, rot);
                var json = new JObject(new JProperty("EntityId", entityId));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/RemoveEntity -- { "EntityId": int }
        // =========================================================================
        public Task<string> RemoveEntity(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                pf.RemoveEntity(entityId);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }
    }
}
