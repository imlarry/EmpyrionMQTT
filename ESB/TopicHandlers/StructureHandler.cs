using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public partial class StructureHandler
    {
        private readonly ContextData _ctx;

        public StructureHandler(ContextData ctx) { _ctx = ctx; }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("Structure/Info",                   Info);
            _ctx.Messenger.RegisterHandler("Structure/Tanks",                  Tanks);
            _ctx.Messenger.RegisterHandler("Structure/GetAllCustomDeviceNames",GetAllCustomDeviceNames);
            _ctx.Messenger.RegisterHandler("Structure/GetDevicePositions",     GetDevicePositions);
            _ctx.Messenger.RegisterHandler("Structure/GetDockedVessels",       GetDockedVessels);
            _ctx.Messenger.RegisterHandler("Structure/GetPassengers",          GetPassengers);
            _ctx.Messenger.RegisterHandler("Structure/GetBlockSignals",        GetBlockSignals);
            _ctx.Messenger.RegisterHandler("Structure/GetControlPanelSignals", GetControlPanelSignals);
            _ctx.Messenger.RegisterHandler("Structure/GetSignalState",         GetSignalState);
            _ctx.Messenger.RegisterHandler("Structure/GetSignalReceivers",     GetSignalReceivers);
            _ctx.Messenger.RegisterHandler("Structure/GetSendSignalName",      GetSendSignalName);
            _ctx.Messenger.RegisterHandler("Structure/AddTankContent",         AddTankContent);
            _ctx.Messenger.RegisterHandler("Structure/SetFaction",             SetFaction);
            _ctx.Messenger.RegisterHandler("Structure/StructToGlobalPos",      StructToGlobalPos);
            _ctx.Messenger.RegisterHandler("Structure/GlobalToStructPos",      GlobalToStructPos);
            // Device scope operations are deferred to a future DeviceHandler pass.
        }

        // -------------------------------------------------------------------------
        // Shared structure lookup via CurrentPlayfield -- works on Client and Pfs.
        // Returns null and sends error reply when the entity cannot be resolved.
        // -------------------------------------------------------------------------
        private async Task<IStructure> GetStructureForEntity(MessageContext ctx, int entityId)
        {
            var pf = _ctx.GameManager.CurrentPlayfield;
            if (pf == null)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                    MessageHelpers.ErrorJson("CurrentPlayfield is null -- no playfield loaded on this process"));
                return null;
            }
            IEntity entity;
            if (!pf.Entities.TryGetValue(entityId, out entity) || entity == null)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                    MessageHelpers.ErrorJson($"Entity {entityId} not found in CurrentPlayfield.Entities"));
                return null;
            }
            var structure = entity.Structure;
            if (structure == null)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                    MessageHelpers.ErrorJson($"Entity {entityId} has no Structure (not a structure entity)"));
                return null;
            }
            return structure;
        }

        private Task<ILcd>       GetLcd(MessageContext ctx, IStructure s, int id, VectorInt3 pos)       => GetNamedDevice(ctx, s, id, pos, (st, p) => st.GetDevice<ILcd>(p),       "ILcd");
        private Task<ILight>     GetLight(MessageContext ctx, IStructure s, int id, VectorInt3 pos)     => GetNamedDevice(ctx, s, id, pos, (st, p) => st.GetDevice<ILight>(p),     "ILight");
        private Task<IContainer> GetContainer(MessageContext ctx, IStructure s, int id, VectorInt3 pos) => GetNamedDevice(ctx, s, id, pos, (st, p) => st.GetDevice<IContainer>(p), "IContainer");
        private Task<ITeleporter> GetTeleporter(MessageContext ctx, IStructure s, int id, VectorInt3 pos) => GetNamedDevice(ctx, s, id, pos, (st, p) => st.GetDevice<ITeleporter>(p), "ITeleporter");

        private async Task<T> GetNamedDevice<T>(MessageContext ctx, IStructure structure, int entityId, VectorInt3 pos, Func<IStructure, VectorInt3, T> lookup, string typeName)
            where T : class
        {
            var device = lookup(structure, pos);
            if (device == null)
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                    MessageHelpers.ErrorJson($"No {typeName} device at {MessageHelpers.Vec(pos)} in entity {entityId}"));
            return device;
        }

        // =========================================================================
        // Structure/Req/get/Info
        // payload: { "EntityId": int }
        // =========================================================================
        public async Task Info(MessageContext ctx)
        {
            try
            {
                int entityId = JObject.Parse(ctx.Payload)["EntityId"].Value<int>();
                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

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

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/get/Tanks
        // payload: { "EntityId": int }
        // =========================================================================
        public async Task Tanks(MessageContext ctx)
        {
            try
            {
                int entityId = JObject.Parse(ctx.Payload)["EntityId"].Value<int>();
                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var json = new JObject(
                    new JProperty("EntityId",     entityId),
                    new JProperty("FuelTank",     TankJson(structure.FuelTank)),
                    new JProperty("OxygenTank",   TankJson(structure.OxygenTank)),
                    new JProperty("PentaxidTank", TankJson(structure.PentaxidTank)));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetAllCustomDeviceNames
        // payload: { "EntityId": int }
        // =========================================================================
        public async Task GetAllCustomDeviceNames(MessageContext ctx)
        {
            try
            {
                int entityId = JObject.Parse(ctx.Payload)["EntityId"].Value<int>();
                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var names = structure.GetAllCustomDeviceNames();
                var json = new JObject(
                    new JProperty("EntityId",    entityId),
                    new JProperty("DeviceNames", new JArray(names ?? new string[0])));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetDevicePositions
        // payload: { "EntityId": int, "DeviceName": string }
        // =========================================================================
        public async Task GetDevicePositions(MessageContext ctx)
        {
            try
            {
                var args       = JObject.Parse(ctx.Payload);
                int entityId   = args["EntityId"].Value<int>();
                string devName = args["DeviceName"].Value<string>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var list = structure.GetDevicePositions(devName);
                var positions = new JArray();
                if (list != null)
                    foreach (var pos in list)
                        positions.Add(MessageHelpers.Vec(pos));

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", devName),
                    new JProperty("Positions",  positions));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetDockedVessels
        // payload: { "EntityId": int }
        // =========================================================================
        public async Task GetDockedVessels(MessageContext ctx)
        {
            try
            {
                int entityId = JObject.Parse(ctx.Payload)["EntityId"].Value<int>();
                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

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

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetPassengers
        // payload: { "EntityId": int }
        // =========================================================================
        public async Task GetPassengers(MessageContext ctx)
        {
            try
            {
                int entityId = JObject.Parse(ctx.Payload)["EntityId"].Value<int>();
                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

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

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetBlockSignals
        // payload: { "EntityId": int, "Filter": string? }
        // =========================================================================
        public async Task GetBlockSignals(MessageContext ctx)
        {
            try
            {
                var args      = JObject.Parse(ctx.Payload);
                int entityId  = args["EntityId"].Value<int>();
                string filter = args["Filter"]?.Value<string>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

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

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetControlPanelSignals
        // payload: { "EntityId": int }
        // =========================================================================
        public async Task GetControlPanelSignals(MessageContext ctx)
        {
            try
            {
                int entityId = JObject.Parse(ctx.Payload)["EntityId"].Value<int>();
                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

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

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetSignalState
        // payload: { "EntityId": int, "SignalName": string }
        // =========================================================================
        public async Task GetSignalState(MessageContext ctx)
        {
            try
            {
                var args       = JObject.Parse(ctx.Payload);
                int entityId   = args["EntityId"].Value<int>();
                string sigName = args["SignalName"].Value<string>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                bool state = structure.GetSignalState(sigName);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("SignalName", sigName),
                    new JProperty("State",      state));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetSignalReceivers
        // payload: { "EntityId": int, "SignalName": string }
        // =========================================================================
        public async Task GetSignalReceivers(MessageContext ctx)
        {
            try
            {
                var args       = JObject.Parse(ctx.Payload);
                int entityId   = args["EntityId"].Value<int>();
                string sigName = args["SignalName"].Value<string>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

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

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GetSendSignalName
        // payload: { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public async Task GetSendSignalName(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                string sigName = structure.GetSendSignalName(pos);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("SignalName", sigName));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/AddTankContent
        // payload: { "EntityId": int, "TankType": "Fuel"|"Oxygen"|"Pentaxid", "Amount": float }
        // =========================================================================
        public async Task AddTankContent(MessageContext ctx)
        {
            try
            {
                var args        = JObject.Parse(ctx.Payload);
                int entityId    = args["EntityId"].Value<int>();
                string tankType = args["TankType"].Value<string>();
                float amount    = args["Amount"].Value<float>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                IStructureTank tank;
                if      (tankType == "Fuel")     tank = structure.FuelTank;
                else if (tankType == "Oxygen")   tank = structure.OxygenTank;
                else if (tankType == "Pentaxid") tank = structure.PentaxidTank;
                else                             tank = null;

                if (tank == null)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                        MessageHelpers.ErrorJson($"Unknown TankType '{tankType}'. Valid: Fuel, Oxygen, Pentaxid"));
                    return;
                }

                tank.AddContent(amount);
                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("TankType", tankType),
                    new JProperty("Amount",   amount),
                    new JProperty("Content",  tank.Content),
                    new JProperty("Capacity", tank.Capacity));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/SetFaction
        // payload: { "EntityId": int, "FactionGroup": string, "FactionEntityId": int }
        // =========================================================================
        public async Task SetFaction(MessageContext ctx)
        {
            try
            {
                var args          = JObject.Parse(ctx.Payload);
                int entityId      = args["EntityId"].Value<int>();
                int factionEntity = args["FactionEntityId"].Value<int>();
                FactionGroup group;
                if (!Enum.TryParse(args["FactionGroup"].Value<string>(), true, out group))
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                        MessageHelpers.ErrorJson($"Unknown FactionGroup: {args["FactionGroup"]}"));
                    return;
                }

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                structure.SetFaction(group, factionEntity);
                var json = new JObject(
                    new JProperty("EntityId",        entityId),
                    new JProperty("FactionGroup",    group.ToString()),
                    new JProperty("FactionEntityId", factionEntity));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/StructToGlobalPos
        // payload: { "EntityId": int, "StructPos": {X,Y,Z} }
        // =========================================================================
        public async Task StructToGlobalPos(MessageContext ctx)
        {
            try
            {
                var args         = JObject.Parse(ctx.Payload);
                int entityId     = args["EntityId"].Value<int>();
                VectorInt3 structPos = MessageHelpers.ParseVecInt3(args["StructPos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                Vector3 globalPos = structure.StructToGlobalPos(structPos);
                var json = new JObject(
                    new JProperty("EntityId",  entityId),
                    new JProperty("StructPos", MessageHelpers.Vec(structPos)),
                    new JProperty("GlobalPos", MessageHelpers.Vec(globalPos)));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Structure/Req/call/GlobalToStructPos
        // payload: { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public async Task GlobalToStructPos(MessageContext ctx)
        {
            try
            {
                var args      = JObject.Parse(ctx.Payload);
                int entityId  = args["EntityId"].Value<int>();
                Vector3 globalPos = MessageHelpers.ParseVec3(args["Pos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                VectorInt3 structPos = structure.GlobalToStructPos(globalPos);
                var json = new JObject(
                    new JProperty("EntityId",  entityId),
                    new JProperty("GlobalPos", MessageHelpers.Vec(globalPos)),
                    new JProperty("StructPos", MessageHelpers.Vec(structPos)));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

    }
}
