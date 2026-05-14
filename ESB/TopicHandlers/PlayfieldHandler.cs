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
    public class PlayfieldHandler
    {
        private readonly ContextData _ctx;

        public PlayfieldHandler(ContextData ctx) { _ctx = ctx; }

        public void Register()
        {
            _ctx.Bus.OnRequest("Playfield", "GetProperties",          GetProperties);
            _ctx.Bus.OnRequest("Playfield", "Name",                   GetName);
            _ctx.Bus.OnRequest("Playfield", "PlayfieldType",          GetPlayfieldType);
            _ctx.Bus.OnRequest("Playfield", "PlanetType",             GetPlanetType);
            _ctx.Bus.OnRequest("Playfield", "PlanetClass",            GetPlanetClass);
            _ctx.Bus.OnRequest("Playfield", "SolarSystemName",        GetSolarSystemName);
            _ctx.Bus.OnRequest("Playfield", "SolarSystemCoordinates", GetSolarSystemCoordinates);
            _ctx.Bus.OnRequest("Playfield", "IsPvP",                  GetIsPvP);
            _ctx.Bus.OnRequest("Playfield", "GetEntities",            GetEntities);
            _ctx.Bus.OnRequest("Playfield", "GetPlayers",             GetPlayers);
            _ctx.Bus.OnRequest("Playfield", "GetTerrainHeight",       GetTerrainHeight);
            _ctx.Bus.OnRequest("Playfield", "SpawnEntity",            SpawnEntity);
            _ctx.Bus.OnRequest("Playfield", "SpawnPrefab",            SpawnPrefab);
            _ctx.Bus.OnRequest("Playfield", "RemoveEntity",           RemoveEntity);
            _ctx.Bus.OnRequest("Playfield", "SpawnTestPlayer",        SpawnTestPlayer);
            _ctx.Bus.OnRequest("Playfield", "RemoveTestPlayer",       RemoveTestPlayer);
            _ctx.Bus.OnRequest("Playfield", "LockStructureDevice",    LockStructureDevice);
            _ctx.Bus.OnRequest("Playfield", "IsStructureDeviceLocked",IsStructureDeviceLocked);
            _ctx.Bus.OnRequest("Playfield", "GetStructureDevices",    GetStructureDevices);
            _ctx.Bus.OnRequest("Playfield", "AddVoxelArea",           AddVoxelArea);
            _ctx.Bus.OnRequest("Playfield", "MoveVoxelArea",          MoveVoxelArea);
            _ctx.Bus.OnRequest("Playfield", "RemoveVoxelArea",        RemoveVoxelArea);
        }

        private IPlayfield CurrentPlayfield =>
            _ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield;

        // =========================================================================
        // Playfield/GetProperties -- (no payload)
        // All scalar IPlayfield properties plus inline Players and Entities lists.
        // =========================================================================
        private Task<string> GetProperties(MessageEnvelope env)
        {
            var pf = CurrentPlayfield;
            if (pf == null)
                return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));

            var json = new JObject();
            json["Name"]                   = pf.Name;
            json["PlayfieldType"]          = pf.PlayfieldType;
            json["PlanetType"]             = pf.PlanetType;
            json["PlanetClass"]            = pf.PlanetClass;
            json["SolarSystemName"]        = pf.SolarSystemName;
            json["SolarSystemCoordinates"] = MessageHelpers.Vec(pf.SolarSystemCoordinates);
            json["IsPvP"]                  = pf.IsPvP;

            try
            {
                var rows = new JArray();
                foreach (var kv in pf.Players)
                {
                    var p = kv.Value;
                    if (p == null) continue;
                    rows.Add(new JArray(p.Id, p.Name, p.SteamId));
                }
                json["Players"] = MessageHelpers.Tabular(PlayerColumns, rows);
            }
            catch { }

            try
            {
                var rows = new JArray();
                foreach (var kv in pf.Entities)
                {
                    var e = kv.Value;
                    if (e == null) continue;
                    rows.Add(new JArray(e.Id, e.Name, e.Type.ToString(), MessageHelpers.Vec(e.Position), e.IsProxy));
                }
                json["Entities"] = MessageHelpers.Tabular(EntityColumns, rows);
            }
            catch { }

            return Task.FromResult(json.ToString(Formatting.None));
        }

        private static readonly string[] EntityColumns = { "EntityId", "Name", "EntityType", "Position", "IsProxy" };
        private static readonly string[] PlayerColumns = { "EntityId", "Name", "SteamId" };
        private static readonly string[] DeviceColumns = { "Type", "Pos" };

        // =========================================================================
        // Scalar property handlers
        // =========================================================================
        private Task<string> GetName(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null) return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                return Task.FromResult(new JObject(new JProperty("Name", pf.Name)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        private Task<string> GetPlayfieldType(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null) return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                return Task.FromResult(new JObject(new JProperty("PlayfieldType", pf.PlayfieldType)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        private Task<string> GetPlanetType(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null) return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                return Task.FromResult(new JObject(new JProperty("PlanetType", pf.PlanetType)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        private Task<string> GetPlanetClass(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null) return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                return Task.FromResult(new JObject(new JProperty("PlanetClass", pf.PlanetClass)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        private Task<string> GetSolarSystemName(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null) return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                return Task.FromResult(new JObject(new JProperty("SolarSystemName", pf.SolarSystemName)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        private Task<string> GetSolarSystemCoordinates(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null) return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                return Task.FromResult(new JObject(new JProperty("SolarSystemCoordinates", MessageHelpers.Vec(pf.SolarSystemCoordinates))).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        private Task<string> GetIsPvP(MessageEnvelope env)
        {
            try
            {
                var pf = CurrentPlayfield;
                if (pf == null) return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                return Task.FromResult(new JObject(new JProperty("IsPvP", pf.IsPvP)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

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

                var rows = new JArray();
                foreach (var kv in pf.Entities)
                {
                    var e = kv.Value;
                    if (e == null) continue;
                    rows.Add(new JArray(e.Id, e.Name, e.Type.ToString(), MessageHelpers.Vec(e.Position), e.IsProxy));
                }
                var json = new JObject(new JProperty("Entities", MessageHelpers.Tabular(EntityColumns, rows)));
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

                var rows = new JArray();
                foreach (var kv in pf.Players)
                {
                    var p = kv.Value;
                    if (p == null) continue;
                    rows.Add(new JArray(p.Id, p.Name, p.SteamId));
                }
                var json = new JObject(new JProperty("Players", MessageHelpers.Tabular(PlayerColumns, rows)));
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
        // Playfield/SpawnPrefab -- { "PrefabName": string, "Pos": {X,Y,Z} }
        // Returns: { "EntityId": int }
        // =========================================================================
        private Task<string> SpawnPrefab(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<SpawnPrefabRequest>();
                if (string.IsNullOrEmpty(req?.PrefabName))
                    return Task.FromResult(MessageHelpers.ErrorJson("PrefabName is required"));
                if (req.Pos == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Pos is required"));
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                var pos = new Vector3(req.Pos.X, req.Pos.Y, req.Pos.Z);
                int entityId = pf.SpawnPrefab(req.PrefabName, pos);
                return Task.FromResult(new JObject(new JProperty("EntityId", entityId)).ToString(Formatting.None));
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

        // =========================================================================
        // Playfield/SpawnTestPlayer -- { "Pos": {X,Y,Z} }
        // Returns: { "EntityId": int }
        // =========================================================================
        private Task<string> SpawnTestPlayer(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<SpawnTestPlayerRequest>();
                if (req?.Pos == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Pos is required"));
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                var pos = new Vector3(req.Pos.X, req.Pos.Y, req.Pos.Z);
                int entityId = pf.SpawnTestPlayer(pos);
                return Task.FromResult(new JObject(new JProperty("EntityId", entityId)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/RemoveTestPlayer -- { "EntityId": int }
        // Returns: { "ok": bool }
        // =========================================================================
        private Task<string> RemoveTestPlayer(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                bool ok = pf.RemoveTestPlayer(entityId);
                return Task.FromResult(new JObject(new JProperty("ok", ok)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/LockStructureDevice -- { "StructureId": int, "PosInStruct": {X,Y,Z}, "DoLock": bool }
        // PosInStruct uses Vec3Payload (float); values are truncated to int for VectorInt3.
        // Use whole-number coordinates; consider a VecInt3Payload if precision matters.
        // Async callback pattern; callback: void(int structureId, VectorInt3 pos, bool success)
        // Returns: { "StructureId": int, "PosInStruct": {X,Y,Z}, "Success": bool }
        // =========================================================================
        private async Task<string> LockStructureDevice(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<LockStructureDeviceRequest>();
                if (req?.PosInStruct == null)
                    return MessageHelpers.ErrorJson("PosInStruct is required");
                var pf = CurrentPlayfield;
                if (pf == null)
                    return MessageHelpers.ErrorJson("No active playfield");
                var pos = new VectorInt3((int)req.PosInStruct.X, (int)req.PosInStruct.Y, (int)req.PosInStruct.Z);
                var tcs = new TaskCompletionSource<string>();
                bool queued = await _ctx.MainThreadRunner.RunOnMainThread<bool>(() =>
                    pf.LockStructureDevice(req.StructureId, pos, req.DoLock,
                        (structureId, posResult, success) =>
                            tcs.SetResult(new JObject(
                                new JProperty("StructureId",  structureId),
                                new JProperty("PosInStruct",  MessageHelpers.Vec(posResult)),
                                new JProperty("Success",      success)).ToString(Formatting.None))));
                if (!queued)
                    return MessageHelpers.ErrorJson("LockStructureDevice request failed");
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                return MessageHelpers.ExceptionJson(ex);
            }
        }

        // =========================================================================
        // Playfield/IsStructureDeviceLocked -- { "StructureId": int, "PosInStruct": {X,Y,Z} }
        // Returns: { "IsLocked": bool }
        // =========================================================================
        private Task<string> IsStructureDeviceLocked(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<IsStructureDeviceLockedRequest>();
                if (req?.PosInStruct == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("PosInStruct is required"));
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                var pos = new VectorInt3((int)req.PosInStruct.X, (int)req.PosInStruct.Y, (int)req.PosInStruct.Z);
                bool isLocked = pf.IsStructureDeviceLocked(req.StructureId, pos);
                return Task.FromResult(new JObject(new JProperty("IsLocked", isLocked)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/GetStructureDevices -- { "StructureId": int, "DeviceType": string? }
        // DeviceType is optional; if omitted, enumerates every DeviceTypeName.
        // Returns: { "StructureId": int, "DeviceCount": int, "Devices": { Columns:["Type","Pos"], Rows:[...] } }
        // =========================================================================
        private Task<string> GetStructureDevices(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<GetStructureDevicesRequest>();
                if (req == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("StructureId is required"));
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                IEntity entity;
                if (!pf.Entities.TryGetValue(req.StructureId, out entity) || entity == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Entity not found: " + req.StructureId));
                var st = entity.Structure;
                if (st == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Entity " + req.StructureId + " has no Structure"));

                DeviceTypeName? filter = null;
                if (!string.IsNullOrEmpty(req.DeviceType))
                {
                    DeviceTypeName parsed;
                    if (!Enum.TryParse(req.DeviceType, true, out parsed))
                        return Task.FromResult(MessageHelpers.ErrorJson("Unknown DeviceType: " + req.DeviceType));
                    filter = parsed;
                }

                var rows = new JArray();
                foreach (DeviceTypeName dt in Enum.GetValues(typeof(DeviceTypeName)))
                {
                    if ((int)dt == 0) continue; // skip All_NOT_USED_YET
                    if (filter.HasValue && dt != filter.Value) continue;
                    var list = st.GetDevices(dt);
                    if (list == null) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var p = list.GetAt(i);
                        rows.Add(new JArray(dt.ToString(), MessageHelpers.Vec(p)));
                    }
                }
                var json = new JObject(
                    new JProperty("StructureId", req.StructureId),
                    new JProperty("DeviceCount", st.DeviceCount),
                    new JProperty("Devices",     MessageHelpers.Tabular(DeviceColumns, rows)));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/AddVoxelArea -- { "Pos": {X,Y,Z}, "SizeInMeter": int }
        // Returns: { "AreaId": int }
        // =========================================================================
        private Task<string> AddVoxelArea(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<AddVoxelAreaRequest>();
                if (req?.Pos == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Pos is required"));
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                var pos = new Vector3(req.Pos.X, req.Pos.Y, req.Pos.Z);
                int areaId = pf.AddVoxelArea(pos, req.SizeInMeter);
                return Task.FromResult(new JObject(new JProperty("AreaId", areaId)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/MoveVoxelArea -- { "AreaId": int, "Pos": {X,Y,Z} }
        // Returns: { "ok": bool }
        // =========================================================================
        private Task<string> MoveVoxelArea(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<MoveVoxelAreaRequest>();
                if (req?.Pos == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Pos is required"));
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                var pos = new Vector3(req.Pos.X, req.Pos.Y, req.Pos.Z);
                bool ok = pf.MoveVoxelArea(req.AreaId, pos);
                return Task.FromResult(new JObject(new JProperty("ok", ok)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Playfield/RemoveVoxelArea -- { "AreaId": int }
        // Returns: { "ok": bool }
        // =========================================================================
        private Task<string> RemoveVoxelArea(MessageEnvelope env)
        {
            try
            {
                int areaId = (int)env.PayloadJson["AreaId"];
                var pf = CurrentPlayfield;
                if (pf == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                bool ok = pf.RemoveVoxelArea(areaId);
                return Task.FromResult(new JObject(new JProperty("ok", ok)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }
    }
}
