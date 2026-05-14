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
    public class StructureHandler
    {
        private readonly ContextData _ctx;

        public StructureHandler(ContextData ctx) { _ctx = ctx; }

        public void Register()
        {
            _ctx.Bus.OnRequest("Structure", "Info",                    Info);
            _ctx.Bus.OnRequest("Structure", "Tanks",                   Tanks);
            _ctx.Bus.OnRequest("Structure", "GetAllCustomDeviceNames", GetAllCustomDeviceNames);
            _ctx.Bus.OnRequest("Structure", "GetDevicePositions",      GetDevicePositions);
            _ctx.Bus.OnRequest("Structure", "GetDockedVessels",        GetDockedVessels);
            _ctx.Bus.OnRequest("Structure", "GetPassengers",           GetPassengers);
            _ctx.Bus.OnRequest("Structure", "GetBlockSignals",         GetBlockSignals);
            _ctx.Bus.OnRequest("Structure", "GetControlPanelSignals",  GetControlPanelSignals);
            _ctx.Bus.OnRequest("Structure", "GetSignalState",          GetSignalState);
            _ctx.Bus.OnRequest("Structure", "GetSignalReceivers",      GetSignalReceivers);
            _ctx.Bus.OnRequest("Structure", "GetSendSignalName",       GetSendSignalName);
            _ctx.Bus.OnRequest("Structure", "AddTankContent",          AddTankContent);
            _ctx.Bus.OnRequest("Structure", "SetFaction",              SetFaction);
            _ctx.Bus.OnRequest("Structure", "StructToGlobalPos",       StructToGlobalPos);
            _ctx.Bus.OnRequest("Structure", "GlobalToStructPos",       GlobalToStructPos);
            _ctx.Bus.OnRequest("Structure", "ScanFloor",               ScanFloor);
            _ctx.Bus.OnRequest("Structure", "GetAllBlocks",            GetAllBlocks);
            // LCD
            _ctx.Bus.OnRequest("Structure", "GetLcd",                  GetLcd);
            _ctx.Bus.OnRequest("Structure", "SetLcdText",              SetLcdText);
            _ctx.Bus.OnRequest("Structure", "SetLcdColors",            SetLcdColors);
            _ctx.Bus.OnRequest("Structure", "SetLcdFontSize",          SetLcdFontSize);
            // Container
            _ctx.Bus.OnRequest("Structure", "GetContainer",            GetContainer);
            _ctx.Bus.OnRequest("Structure", "SetContainer",            SetContainer);
            _ctx.Bus.OnRequest("Structure", "AddItems",                AddItems);
            _ctx.Bus.OnRequest("Structure", "RemoveItems",             RemoveItems);
            // Block
            _ctx.Bus.OnRequest("Structure", "GetBlock",                GetBlock);
            _ctx.Bus.OnRequest("Structure", "SetBlock",                SetBlock);
            _ctx.Bus.OnRequest("Structure", "SetBlockSwitchState",     SetBlockSwitchState);
            // Light
            _ctx.Bus.OnRequest("Structure", "GetLight",                GetLight);
            _ctx.Bus.OnRequest("Structure", "SetLightColor",           SetLightColor);
            _ctx.Bus.OnRequest("Structure", "SetLightIntensity",       SetLightIntensity);
            _ctx.Bus.OnRequest("Structure", "SetLightRange",           SetLightRange);
            _ctx.Bus.OnRequest("Structure", "SetLightBlink",           SetLightBlink);
            // Teleporter
            _ctx.Bus.OnRequest("Structure", "GetTeleporter",           GetTeleporter);
            _ctx.Bus.OnRequest("Structure", "SetTeleporter",           SetTeleporter);
        }

        // Shared structure lookup -- returns null when entity not on this playfield.
        private IStructure GetStructureForEntity(int entityId)
        {
            var pf = _ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield;
            if (pf == null) return null;
            IEntity entity;
            if (!pf.Entities.TryGetValue(entityId, out entity) || entity == null)
                return null;
            if (entity.Structure == null)
                throw new InvalidOperationException(string.Format("Entity {0} has no Structure (not a structure entity)", entityId));
            return entity.Structure;
        }

        private ILcd        ResolveLcd(IStructure s, int id, VectorInt3 pos)        => GetNamedDevice(s, id, pos, (st, p) => st.GetDevice<ILcd>(p),        "ILcd");
        private ILight      ResolveLight(IStructure s, int id, VectorInt3 pos)      => GetNamedDevice(s, id, pos, (st, p) => st.GetDevice<ILight>(p),      "ILight");
        private IContainer  ResolveContainer(IStructure s, int id, VectorInt3 pos)  => GetNamedDevice(s, id, pos, (st, p) => st.GetDevice<IContainer>(p),  "IContainer");
        private ITeleporter ResolveTeleporter(IStructure s, int id, VectorInt3 pos) => GetNamedDevice(s, id, pos, (st, p) => st.GetDevice<ITeleporter>(p), "ITeleporter");

        private T GetNamedDevice<T>(IStructure structure, int entityId, VectorInt3 pos, Func<IStructure, VectorInt3, T> lookup, string typeName)
            where T : class
        {
            var device = lookup(structure, pos);
            if (device == null)
                throw new InvalidOperationException(string.Format("No {0} device at {1} in entity {2}", typeName, MessageHelpers.Vec(pos), entityId));
            return device;
        }

        private static JObject TankJson(IStructureTank tank) =>
            new JObject(
                new JProperty("Content",  tank != null ? tank.Content  : 0f),
                new JProperty("Capacity", tank != null ? tank.Capacity : 0f));

        private static JObject ColorJson(Color c) =>
            new JObject(new JProperty("R", c.r), new JProperty("G", c.g), new JProperty("B", c.b), new JProperty("A", c.a));

        private static Color ParseColor(JToken j) =>
            new Color((float)j["R"], (float)j["G"], (float)j["B"], j["A"] != null ? (float)j["A"] : 1f);

        private static VectorInt3 DevicePos(JObject args) =>
            new VectorInt3((int)args["X"], (int)args["Y"], (int)args["Z"]);

        // =========================================================================
        // Structure/Info -- { "EntityId": int }
        // =========================================================================
        public Task<string> Info(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                JToken S(Func<object> getter)
                {
                    try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
                    catch { return JValue.CreateNull(); }
                }

                var json = new JObject();
                json.Add("EntityId",             entityId);
                json.Add("Id",                   S(() => structure.Id));
                json.Add("IsReady",              S(() => structure.IsReady));
                json.Add("CoreType",             S(() => structure.CoreType.ToString()));
                json.Add("SizeClass",            S(() => structure.SizeClass));
                json.Add("LastVisitedTicks",     S(() => structure.LastVisitedTicks));
                json.Add("PlayerCreatedSteamId", S(() => structure.PlayerCreatedSteamId));
                json.Add("MinPos",               S(() => MessageHelpers.Vec(structure.MinPos)));
                json.Add("MaxPos",               S(() => MessageHelpers.Vec(structure.MaxPos)));

                bool isReady = false;
                try { isReady = structure.IsReady; } catch { }
                if (isReady)
                {
                    json.Add("IsPowered",            S(() => structure.IsPowered));
                    json.Add("IsOfflineProtectable", S(() => structure.IsOfflineProtectable));
                    json.Add("DamageLevel",          S(() => structure.DamageLevel));
                    json.Add("BlockCount",           S(() => structure.BlockCount));
                    json.Add("DeviceCount",          S(() => structure.DeviceCount));
                    json.Add("LightCount",           S(() => structure.LightCount));
                    json.Add("TriangleCount",        S(() => structure.TriangleCount));
                    json.Add("Fuel",                 S(() => structure.Fuel));
                    json.Add("PowerOutCapacity",     S(() => structure.PowerOutCapacity));
                    json.Add("PowerConsumption",     S(() => structure.PowerConsumption));
                    json.Add("IsShieldActive",       S(() => structure.IsShieldActive));
                    json.Add("ShieldLevel",          S(() => structure.ShieldLevel));
                    json.Add("TotalMass",            S(() => structure.TotalMass));
                    json.Add("HasLandClaimDevice",   S(() => structure.HasLandClaimDevice));
                    json.Add("PilotEntityId",        S(() => structure.Pilot != null ? (object)structure.Pilot.Id : null));
                }

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Tanks -- { "EntityId": int }
        // =========================================================================
        public Task<string> Tanks(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var json = new JObject(
                    new JProperty("EntityId",     entityId),
                    new JProperty("FuelTank",     TankJson(structure.FuelTank)),
                    new JProperty("OxygenTank",   TankJson(structure.OxygenTank)),
                    new JProperty("PentaxidTank", TankJson(structure.PentaxidTank)));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetAllCustomDeviceNames -- { "EntityId": int }
        // =========================================================================
        public Task<string> GetAllCustomDeviceNames(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var names = structure.GetAllCustomDeviceNames();
                var json = new JObject(
                    new JProperty("EntityId",    entityId),
                    new JProperty("DeviceNames", new JArray(names ?? new string[0])));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetDevicePositions -- { "EntityId": int, "DeviceName": string }
        // =========================================================================
        public Task<string> GetDevicePositions(MessageEnvelope env)
        {
            try
            {
                var args       = env.PayloadJson;
                int entityId   = (int)args["EntityId"];
                string devName = (string)args["DeviceName"];

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var list = structure.GetDevicePositions(devName);
                var positions = new JArray();
                if (list != null)
                    foreach (var pos in list)
                        positions.Add(MessageHelpers.Vec(pos));

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", devName),
                    new JProperty("Positions",  positions));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetDockedVessels -- { "EntityId": int }
        // =========================================================================
        public Task<string> GetDockedVessels(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var vessels = new JArray();
                var list = structure.GetDockedVessels();
                if (list != null)
                    foreach (var v in list)
                        vessels.Add(new JObject(
                            new JProperty("Id",      v.Entity != null ? (object)v.Entity.Id   : null),
                            new JProperty("Name",    v.Entity != null ? (object)v.Entity.Name : null),
                            new JProperty("IsReady", v.IsReady)));

                var json = new JObject(
                    new JProperty("EntityId",      entityId),
                    new JProperty("DockedVessels", vessels));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetPassengers -- { "EntityId": int }
        // =========================================================================
        public Task<string> GetPassengers(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var passengers = new JArray();
                var list = structure.GetPassengers();
                if (list != null)
                    foreach (var p in list)
                        passengers.Add(new JObject(
                            new JProperty("Id",      p.Id),
                            new JProperty("Name",    p.Name),
                            new JProperty("SteamId", p.SteamId)));

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("Passengers", passengers));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetBlockSignals -- { "EntityId": int, "Filter": string? }
        // =========================================================================
        public Task<string> GetBlockSignals(MessageEnvelope env)
        {
            try
            {
                var args      = env.PayloadJson;
                int entityId  = (int)args["EntityId"];
                string filter = (string)args["Filter"];

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var signals = new JArray();
                var list = structure.GetBlockSignals(filter);
                if (list != null)
                    foreach (var s in list)
                        signals.Add(new JObject(
                            new JProperty("Name",     s.Name),
                            new JProperty("BlockPos", s.BlockPos.HasValue
                                ? (JToken)MessageHelpers.Vec(s.BlockPos.Value)
                                : JValue.CreateNull()),
                            new JProperty("Index",    s.Index)));

                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("Signals",  signals));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetControlPanelSignals -- { "EntityId": int }
        // =========================================================================
        public Task<string> GetControlPanelSignals(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var signals = new JArray();
                var list = structure.GetControlPanelSignals();
                if (list != null)
                    foreach (var s in list)
                        signals.Add(new JObject(
                            new JProperty("Name",     s.Name),
                            new JProperty("BlockPos", s.BlockPos.HasValue
                                ? (JToken)MessageHelpers.Vec(s.BlockPos.Value)
                                : JValue.CreateNull()),
                            new JProperty("Index",    s.Index)));

                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("Signals",  signals));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetSignalState -- { "EntityId": int, "SignalName": string }
        // =========================================================================
        public Task<string> GetSignalState(MessageEnvelope env)
        {
            try
            {
                var args       = env.PayloadJson;
                int entityId   = (int)args["EntityId"];
                string sigName = (string)args["SignalName"];

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                bool state = structure.GetSignalState(sigName);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("SignalName", sigName),
                    new JProperty("State",      state));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetSignalReceivers -- { "EntityId": int, "SignalName": string }
        // =========================================================================
        public Task<string> GetSignalReceivers(MessageEnvelope env)
        {
            try
            {
                var args       = env.PayloadJson;
                int entityId   = (int)args["EntityId"];
                string sigName = (string)args["SignalName"];

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var receivers = new JArray();
                var list = structure.GetSignalReceivers(sigName);
                if (list != null)
                    foreach (var r in list)
                        receivers.Add(new JObject(
                            new JProperty("Func",        r.Func.ToString()),
                            new JProperty("Behavior",    r.Behavior.ToString()),
                            new JProperty("BlockPos",    MessageHelpers.Vec(r.BlockPos)),
                            new JProperty("IsInverting", r.IsInverting)));

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("SignalName", sigName),
                    new JProperty("Receivers",  receivers));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetSendSignalName -- { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public Task<string> GetSendSignalName(MessageEnvelope env)
        {
            try
            {
                var args       = env.PayloadJson;
                int entityId   = (int)args["EntityId"];
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                string sigName = structure.GetSendSignalName(pos);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("SignalName", sigName));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/AddTankContent -- { "EntityId": int, "TankType": "Fuel"|"Oxygen"|"Pentaxid", "Amount": float }
        // =========================================================================
        public Task<string> AddTankContent(MessageEnvelope env)
        {
            try
            {
                var args        = env.PayloadJson;
                int entityId    = (int)args["EntityId"];
                string tankType = (string)args["TankType"];
                float amount    = (float)args["Amount"];

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                IStructureTank tank;
                if      (tankType == "Fuel")     tank = structure.FuelTank;
                else if (tankType == "Oxygen")   tank = structure.OxygenTank;
                else if (tankType == "Pentaxid") tank = structure.PentaxidTank;
                else                             tank = null;

                if (tank == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Unknown TankType '" + tankType + "'. Valid: Fuel, Oxygen, Pentaxid"));

                tank.AddContent(amount);
                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("TankType", tankType),
                    new JProperty("Amount",   amount),
                    new JProperty("Content",  tank.Content),
                    new JProperty("Capacity", tank.Capacity));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/SetFaction -- { "EntityId": int, "FactionGroup": string, "FactionEntityId": int }
        // =========================================================================
        public Task<string> SetFaction(MessageEnvelope env)
        {
            try
            {
                var args          = env.PayloadJson;
                int entityId      = (int)args["EntityId"];
                int factionEntity = (int)args["FactionEntityId"];
                FactionGroup group;
                if (!Enum.TryParse((string)args["FactionGroup"], true, out group))
                    return Task.FromResult(MessageHelpers.ErrorJson("Unknown FactionGroup: " + (string)args["FactionGroup"]));

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                structure.SetFaction(group, factionEntity);
                var json = new JObject(
                    new JProperty("EntityId",        entityId),
                    new JProperty("FactionGroup",    group.ToString()),
                    new JProperty("FactionEntityId", factionEntity));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/StructToGlobalPos -- { "EntityId": int, "StructPos": {X,Y,Z} }
        // =========================================================================
        public Task<string> StructToGlobalPos(MessageEnvelope env)
        {
            try
            {
                var args             = env.PayloadJson;
                int entityId         = (int)args["EntityId"];
                VectorInt3 structPos = MessageHelpers.ParseVecInt3(args["StructPos"]);

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                Vector3 globalPos = structure.StructToGlobalPos(structPos);
                var json = new JObject(
                    new JProperty("EntityId",  entityId),
                    new JProperty("StructPos", MessageHelpers.Vec(structPos)),
                    new JProperty("GlobalPos", MessageHelpers.Vec(globalPos)));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GlobalToStructPos -- { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public Task<string> GlobalToStructPos(MessageEnvelope env)
        {
            try
            {
                var args          = env.PayloadJson;
                int entityId      = (int)args["EntityId"];
                Vector3 globalPos = MessageHelpers.ParseVec3(args["Pos"]);

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                VectorInt3 structPos = structure.GlobalToStructPos(globalPos);
                var json = new JObject(
                    new JProperty("EntityId",  entityId),
                    new JProperty("GlobalPos", MessageHelpers.Vec(globalPos)),
                    new JProperty("StructPos", MessageHelpers.Vec(structPos)));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/ScanFloor -- { "EntityId": int, "Y": int }
        // =========================================================================
        public Task<string> ScanFloor(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                int y        = (int)args["Y"];

                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);

                var minPos = structure.MinPos;
                var maxPos = structure.MaxPos;

                var blocks = new JArray();
                for (int x = minPos.x; x <= maxPos.x; x++)
                    for (int z = minPos.z; z <= maxPos.z; z++)
                    {
                        var block = structure.GetBlock(x, y, z);
                        if (block == null) continue;
                        int type, shape, rotation;
                        bool active;
                        block.Get(out type, out shape, out rotation, out active);
                        if (type == 0) continue;
                        blocks.Add(new JObject(
                            new JProperty("X",        x),
                            new JProperty("Z",        z),
                            new JProperty("Type",     type),
                            new JProperty("Shape",    shape),
                            new JProperty("Rotation", rotation)));
                    }

                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("Y",        y),
                    new JProperty("MinPos",   MessageHelpers.Vec(minPos)),
                    new JProperty("MaxPos",   MessageHelpers.Vec(maxPos)),
                    new JProperty("Blocks",   blocks));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetAllBlocks -- { "EntityId": int }
        // response: { EntityId, Blocks: { Columns: ["X","Y","Z","Type","Shape","Rotation","Active"], Rows: [[...], ...] } }
        //
        // GetBlock coordinate note: IStructure.MinPos/MaxPos X and Z map directly to the
        // coordinates GetBlock expects. Y does not -- MinPos/MaxPos Y is in structure-centered
        // space while GetBlock requires block-space Y. GlobalToStructPos(entity.Position)
        // converts the entity's world position to block space, giving the Y basis needed to
        // apply the MinPos/MaxPos Y offsets correctly. X and Z do not need this correction
        // because structures are centered at (0,0) horizontally in their block space.
        // =========================================================================
        public Task<string> GetAllBlocks(MessageEnvelope env)
        {
            try
            {
                int entityId = (int)env.PayloadJson["EntityId"];

                var pf = _ctx.GameManager.CurrentPlayfield;
                if (pf == null) return Task.FromResult<string>(null);
                IEntity entity;
                if (!pf.Entities.TryGetValue(entityId, out entity) || entity == null)
                    return Task.FromResult<string>(null);
                var structure = entity.Structure;
                if (structure == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Entity " + entityId + " has no Structure"));

                var min   = structure.MinPos;
                var max   = structure.MaxPos;
                int yBase = structure.GlobalToStructPos(entity.Position).y;

                var rows = new JArray();
                for (int x = min.x; x <= max.x; x++)
                    for (int y = yBase + min.y; y <= yBase + max.y; y++)
                        for (int z = min.z; z <= max.z; z++)
                        {
                            var block = structure.GetBlock(x, y, z);
                            if (block == null) continue;
                            int type, shape, rotation;
                            bool active;
                            block.Get(out type, out shape, out rotation, out active);
                            if (type == 0) continue;
                            rows.Add(new JArray(x, y, z, type, shape, rotation, active));
                        }

                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("Blocks", MessageHelpers.Tabular(
                        new[] { "X", "Y", "Z", "Type", "Shape", "Rotation", "Active" }, rows)));

                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/GetLcd -- { "EntityId": int, "X": int, "Y": int, "Z": int }
        // =========================================================================
        public Task<string> GetLcd(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var lcd = ResolveLcd(structure, entityId, pos);
                var json = new JObject(
                    new JProperty("EntityId",        entityId),
                    new JProperty("Pos",             MessageHelpers.Vec(pos)),
                    new JProperty("Text",            lcd.GetText()),
                    new JProperty("BackgroundColor", ColorJson(lcd.GetBackgroundColor())),
                    new JProperty("TextColor",       ColorJson(lcd.GetTextColor())),
                    new JProperty("FontSize",        lcd.GetFontSize()));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetLcdText -- { "EntityId": int, "X": int, "Y": int, "Z": int, "Text": string }
        // =========================================================================
        public Task<string> SetLcdText(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                string text  = (string)args["Text"] ?? "";
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                ResolveLcd(structure, entityId, pos).SetText(text);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetLcdColors -- { "EntityId": int, "X": int, "Y": int, "Z": int,
        //                             "BackgroundColor"?: {R,G,B,A}, "TextColor"?: {R,G,B,A} }
        // =========================================================================
        public Task<string> SetLcdColors(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var lcd = ResolveLcd(structure, entityId, pos);
                if (args["BackgroundColor"] != null) lcd.SetBackgroundColor(ParseColor(args["BackgroundColor"]));
                if (args["TextColor"]       != null) lcd.SetTextColor(ParseColor(args["TextColor"]));
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetLcdFontSize -- { "EntityId": int, "X": int, "Y": int, "Z": int, "FontSize": int }
        // =========================================================================
        public Task<string> SetLcdFontSize(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                int fontSize = (int)args["FontSize"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                ResolveLcd(structure, entityId, pos).SetFontSize(fontSize);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/GetContainer -- { "EntityId": int, "X": int, "Y": int, "Z": int }
        // =========================================================================
        public Task<string> GetContainer(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var container = ResolveContainer(structure, entityId, pos);
                var json = new JObject(
                    new JProperty("EntityId",       entityId),
                    new JProperty("Pos",            MessageHelpers.Vec(pos)),
                    new JProperty("VolumeCapacity", container.VolumeCapacity),
                    new JProperty("DecayFactor",    container.DecayFactor),
                    new JProperty("Items",          HandlerHelper.ItemStacksJson(container.GetContent())));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetContainer -- { "EntityId": int, "X": int, "Y": int, "Z": int,
        //                             "Items": [{Id, Count, SlotIdx, Ammo, Decay}, ...] }
        // =========================================================================
        public Task<string> SetContainer(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var container = ResolveContainer(structure, entityId, pos);
                var items = new System.Collections.Generic.List<ItemStack>();
                var jItems = args["Items"] as JArray;
                if (jItems != null)
                    foreach (var j in jItems)
                        items.Add(new ItemStack { id = (int)j["Id"], count = (int)j["Count"], slotIdx = (byte)(int)(j["SlotIdx"] ?? 0), ammo = (byte)(int)(j["Ammo"] ?? 0), decay = (byte)(int)(j["Decay"] ?? 0) });
                container.SetContent(items);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/AddItems -- { "EntityId": int, "X": int, "Y": int, "Z": int, "ItemId": int, "Count": int }
        // Returns: { "Leftover": int }
        // =========================================================================
        public Task<string> AddItems(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                int itemId   = (int)args["ItemId"];
                int count    = (int)args["Count"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                int leftover = ResolveContainer(structure, entityId, pos).AddItems(itemId, count);
                return Task.FromResult(new JObject(new JProperty("Leftover", leftover)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/RemoveItems -- { "EntityId": int, "X": int, "Y": int, "Z": int, "ItemId": int, "Count": int }
        // Returns: { "Removed": int }
        // =========================================================================
        public Task<string> RemoveItems(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                int itemId   = (int)args["ItemId"];
                int count    = (int)args["Count"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                int removed = ResolveContainer(structure, entityId, pos).RemoveItems(itemId, count);
                return Task.FromResult(new JObject(new JProperty("Removed", removed)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/GetBlock -- { "EntityId": int, "X": int, "Y": int, "Z": int }
        // =========================================================================
        public Task<string> GetBlock(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                int x = (int)args["X"], y = (int)args["Y"], z = (int)args["Z"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var block = structure.GetBlock(x, y, z);
                if (block == null)
                    return Task.FromResult(MessageHelpers.ErrorJson(string.Format("No block at ({0},{1},{2}) in entity {3}", x, y, z, entityId)));
                int type, shape, rotation;
                bool active;
                block.Get(out type, out shape, out rotation, out active);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("X",          x),
                    new JProperty("Y",          y),
                    new JProperty("Z",          z),
                    new JProperty("Type",       type),
                    new JProperty("Shape",      shape),
                    new JProperty("Rotation",   rotation),
                    new JProperty("Active",     active),
                    new JProperty("CustomName", block.CustomName),
                    new JProperty("LockCode",   block.LockCode.HasValue ? (JToken)block.LockCode.Value : JValue.CreateNull()),
                    new JProperty("Damage",     block.GetDamage()),
                    new JProperty("HitPoints",  block.GetHitPoints()));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetBlock -- { "EntityId": int, "X": int, "Y": int, "Z": int,
        //                         "Type"?: int, "Shape"?: int, "Rotation"?: int, "Active"?: bool }
        // =========================================================================
        public Task<string> SetBlock(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                int x = (int)args["X"], y = (int)args["Y"], z = (int)args["Z"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var block = structure.GetBlock(x, y, z);
                if (block == null)
                    return Task.FromResult(MessageHelpers.ErrorJson(string.Format("No block at ({0},{1},{2}) in entity {3}", x, y, z, entityId)));
                int?  type     = args["Type"]     != null && args["Type"].Type     != JTokenType.Null ? (int?)  (int) args["Type"]     : null;
                int?  shape    = args["Shape"]    != null && args["Shape"].Type    != JTokenType.Null ? (int?)  (int) args["Shape"]    : null;
                int?  rotation = args["Rotation"] != null && args["Rotation"].Type != JTokenType.Null ? (int?)  (int) args["Rotation"] : null;
                bool? active   = args["Active"]   != null && args["Active"].Type   != JTokenType.Null ? (bool?) (bool)args["Active"]   : null;
                block.Set(type, shape, rotation, active);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetBlockSwitchState -- { "EntityId": int, "X": int, "Y": int, "Z": int, "State": bool, "Index": int }
        // =========================================================================
        public Task<string> SetBlockSwitchState(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                int x = (int)args["X"], y = (int)args["Y"], z = (int)args["Z"];
                bool state   = (bool)args["State"];
                int  index   = (int)args["Index"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var block = structure.GetBlock(x, y, z);
                if (block == null)
                    return Task.FromResult(MessageHelpers.ErrorJson(string.Format("No block at ({0},{1},{2}) in entity {3}", x, y, z, entityId)));
                bool? newState = block.SetSwitchState(state, index);
                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("NewState", newState.HasValue ? (JToken)newState.Value : JValue.CreateNull()));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/GetLight -- { "EntityId": int, "X": int, "Y": int, "Z": int }
        // =========================================================================
        public Task<string> GetLight(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var light = ResolveLight(structure, entityId, pos);
                float blinkInterval, blinkLength, blinkOffset;
                light.GetBlinkData(out blinkInterval, out blinkLength, out blinkOffset);
                var json = new JObject(
                    new JProperty("EntityId",     entityId),
                    new JProperty("Pos",          MessageHelpers.Vec(pos)),
                    new JProperty("Color",        ColorJson(light.GetColor())),
                    new JProperty("Intensity",    light.GetIntensity()),
                    new JProperty("Range",        light.GetRange()),
                    new JProperty("LightType",    light.GetLightType().ToString()),
                    new JProperty("SpotAngle",    light.GetSpotAngle()),
                    new JProperty("BlinkInterval",blinkInterval),
                    new JProperty("BlinkLength",  blinkLength),
                    new JProperty("BlinkOffset",  blinkOffset));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetLightColor -- { "EntityId": int, "X": int, "Y": int, "Z": int, "Color": {R,G,B,A} }
        // =========================================================================
        public Task<string> SetLightColor(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                ResolveLight(structure, entityId, pos).SetColor(ParseColor(args["Color"]));
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetLightIntensity -- { "EntityId": int, "X": int, "Y": int, "Z": int, "Intensity": float }
        // =========================================================================
        public Task<string> SetLightIntensity(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                float intensity = (float)args["Intensity"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                ResolveLight(structure, entityId, pos).SetIntensity(intensity);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetLightRange -- { "EntityId": int, "X": int, "Y": int, "Z": int, "Range": float }
        // =========================================================================
        public Task<string> SetLightRange(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                float range  = (float)args["Range"];
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                ResolveLight(structure, entityId, pos).SetRange(range);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetLightBlink -- { "EntityId": int, "X": int, "Y": int, "Z": int,
        //                              "Interval": float, "Length": float, "Offset": float }
        // =========================================================================
        public Task<string> SetLightBlink(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                float interval = (float)args["Interval"];
                float length   = (float)args["Length"];
                float offset   = (float)args["Offset"];
                var structure  = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                ResolveLight(structure, entityId, pos).SetBlinkData(interval, length, offset);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/GetTeleporter -- { "EntityId": int, "X": int, "Y": int, "Z": int }
        // =========================================================================
        public Task<string> GetTeleporter(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var tp = ResolveTeleporter(structure, entityId, pos);
                var td = tp.TargetData;
                var json = new JObject(
                    new JProperty("EntityId",               entityId),
                    new JProperty("Pos",                    MessageHelpers.Vec(pos)),
                    new JProperty("TargetEntityNameOrGroup",td.TargetEntityNameOrGroup),
                    new JProperty("TargetPlayfield",        td.TargetPlayfield),
                    new JProperty("TargetSolarSystemName",  td.TargetSolarSystemName),
                    new JProperty("Origin",                 td.Origin));
                return Task.FromResult(json.ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }

        // =========================================================================
        // Structure/SetTeleporter -- { "EntityId": int, "X": int, "Y": int, "Z": int,
        //   "TargetEntityNameOrGroup"?: string, "TargetPlayfield"?: string,
        //   "TargetSolarSystemName"?: string, "Origin"?: byte }
        // =========================================================================
        public Task<string> SetTeleporter(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                int entityId = (int)args["EntityId"];
                var pos      = DevicePos(args);
                var structure = GetStructureForEntity(entityId);
                if (structure == null) return Task.FromResult<string>(null);
                var tp = ResolveTeleporter(structure, entityId, pos);
                var td = tp.TargetData;
                if (args["TargetEntityNameOrGroup"] != null) td.TargetEntityNameOrGroup = (string)args["TargetEntityNameOrGroup"];
                if (args["TargetPlayfield"]         != null) td.TargetPlayfield         = (string)args["TargetPlayfield"];
                if (args["TargetSolarSystemName"]   != null) td.TargetSolarSystemName   = (string)args["TargetSolarSystemName"];
                if (args["Origin"]                  != null) td.Origin                  = (byte)(int)args["Origin"];
                tp.TargetData = td;
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex) { return Task.FromResult(MessageHelpers.ExceptionJson(ex)); }
        }
    }
}
