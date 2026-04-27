using Eleon.Modding;
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
            // Structure scope -- structure-level operations
            _ctx.Messenger.RegisterHandler("Structure/Req/get/Info",                    Info);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/Tanks",                   Tanks);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetAllCustomDeviceNames", GetAllCustomDeviceNames);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetDevicePositions",      GetDevicePositions);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetDockedVessels",        GetDockedVessels);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetPassengers",           GetPassengers);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetBlockSignals",         GetBlockSignals);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetControlPanelSignals",  GetControlPanelSignals);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetSignalState",          GetSignalState);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetSignalReceivers",      GetSignalReceivers);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GetSendSignalName",       GetSendSignalName);
            _ctx.Messenger.RegisterHandler("Structure/Req/set/AddTankContent",          AddTankContent);
            _ctx.Messenger.RegisterHandler("Structure/Req/set/SetFaction",              SetFaction);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/StructToGlobalPos",       StructToGlobalPos);
            _ctx.Messenger.RegisterHandler("Structure/Req/get/GlobalToStructPos",       GlobalToStructPos);

            // Device sub-scope -- wildcard device name; DeviceName read from ctx.ParsedTopic.DeviceName
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/get/Lcd",             LcdGet);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/Text",            LcdSetText);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/FontSize",        LcdSetFontSize);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/TextColor",       LcdSetTextColor);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/BackgroundColor", LcdSetBackgroundColor);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/get/Light",           LightGet);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/Color",           LightSetColor);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/Intensity",       LightSetIntensity);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/Range",           LightSetRange);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/LightType",       LightSetLightType);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/BlinkData",       LightSetBlinkData);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/SpotAngle",       LightSetSpotAngle);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/get/Container",       ContainerGet);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/AddItems",       ContainerAddItems);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/RemoveItems",    ContainerRemoveItems);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/SetContent",     ContainerSetContent);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/Clear",          ContainerClear);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/get/Contains",       ContainerContains);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/get/GetTotalItems",  ContainerGetTotalItems);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/get/Teleporter",      TeleporterGet);
            _ctx.Messenger.RegisterHandler("Structure/Device/*/Req/set/Teleporter",      TeleporterSet);
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

        // =========================================================================
        // LCD device handlers
        // Device name is in ctx.ParsedTopic.DeviceName; entity ID and Pos in payload.
        //
        // Structure/Device/{name}/Req/get/Lcd
        // payload: { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public async Task LcdGet(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var lcd = await GetLcd(ctx, structure, entityId, pos);
                if (lcd == null) return;

                var json = new JObject(
                    new JProperty("EntityId",        entityId),
                    new JProperty("DeviceName",      ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",             MessageHelpers.Vec(pos)),
                    new JProperty("Text",            lcd.GetText()            ?? string.Empty),
                    new JProperty("FontSize",        lcd.GetFontSize()),
                    new JProperty("TextColor",       ColorJson(lcd.GetTextColor())),
                    new JProperty("BackgroundColor", ColorJson(lcd.GetBackgroundColor())));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/Text
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": string }
        public async Task LcdSetText(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                string text  = args["Value"].Value<string>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var lcd = await GetLcd(ctx, structure, entityId, pos);
                if (lcd == null) return;

                lcd.SetText(text);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      text));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/FontSize
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": int }
        public async Task LcdSetFontSize(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int fontSize = args["Value"].Value<int>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var lcd = await GetLcd(ctx, structure, entityId, pos);
                if (lcd == null) return;

                lcd.SetFontSize(fontSize);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      fontSize));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/TextColor
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": {R,G,B,A} }
        public async Task LcdSetTextColor(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                Color color  = ParseColor(args["Value"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var lcd = await GetLcd(ctx, structure, entityId, pos);
                if (lcd == null) return;

                lcd.SetTextColor(color);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      ColorJson(color)));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/BackgroundColor
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": {R,G,B,A} }
        public async Task LcdSetBackgroundColor(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                Color color  = ParseColor(args["Value"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var lcd = await GetLcd(ctx, structure, entityId, pos);
                if (lcd == null) return;

                lcd.SetBackgroundColor(color);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      ColorJson(color)));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Light device handlers
        //
        // Structure/Device/{name}/Req/get/Light
        // payload: { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public async Task LightGet(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var light = await GetLight(ctx, structure, entityId, pos);
                if (light == null) return;

                light.GetBlinkData(out float blinkInterval, out float blinkLength, out float blinkOffset);
                var json = new JObject(
                    new JProperty("EntityId",      entityId),
                    new JProperty("DeviceName",    ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",           MessageHelpers.Vec(pos)),
                    new JProperty("Color",         ColorJson(light.GetColor())),
                    new JProperty("Intensity",     light.GetIntensity()),
                    new JProperty("Range",         light.GetRange()),
                    new JProperty("LightType",     light.GetLightType().ToString()),
                    new JProperty("SpotAngle",     light.GetSpotAngle()),
                    new JProperty("BlinkInterval", blinkInterval),
                    new JProperty("BlinkLength",   blinkLength),
                    new JProperty("BlinkOffset",   blinkOffset));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/Color
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": {R,G,B,A} }
        public async Task LightSetColor(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                Color color  = ParseColor(args["Value"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var light = await GetLight(ctx, structure, entityId, pos);
                if (light == null) return;

                light.SetColor(color);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      ColorJson(color)));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/Intensity
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": float }
        public async Task LightSetIntensity(MessageContext ctx)
        {
            try
            {
                var args      = JObject.Parse(ctx.Payload);
                int entityId  = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                float value   = args["Value"].Value<float>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var light = await GetLight(ctx, structure, entityId, pos);
                if (light == null) return;

                light.SetIntensity(value);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      value));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/Range
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": float }
        public async Task LightSetRange(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                float value  = args["Value"].Value<float>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var light = await GetLight(ctx, structure, entityId, pos);
                if (light == null) return;

                light.SetRange(value);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      value));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/LightType
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": string }
        public async Task LightSetLightType(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                LightType lightType;
                if (!Enum.TryParse(args["Value"].Value<string>(), true, out lightType))
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                        MessageHelpers.ErrorJson($"Unknown LightType: {args["Value"]}"));
                    return;
                }

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var light = await GetLight(ctx, structure, entityId, pos);
                if (light == null) return;

                light.SetLightType(lightType);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      lightType.ToString()));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/BlinkData
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "BlinkInterval": float, "BlinkLength": float, "BlinkOffset": float }
        public async Task LightSetBlinkData(MessageContext ctx)
        {
            try
            {
                var args          = JObject.Parse(ctx.Payload);
                int entityId      = args["EntityId"].Value<int>();
                VectorInt3 pos    = MessageHelpers.ParseVecInt3(args["Pos"]);
                float blinkInterval = args["BlinkInterval"].Value<float>();
                float blinkLength   = args["BlinkLength"].Value<float>();
                float blinkOffset   = args["BlinkOffset"].Value<float>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var light = await GetLight(ctx, structure, entityId, pos);
                if (light == null) return;

                light.SetBlinkData(blinkInterval, blinkLength, blinkOffset);
                var json = new JObject(
                    new JProperty("EntityId",      entityId),
                    new JProperty("DeviceName",    ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",           MessageHelpers.Vec(pos)),
                    new JProperty("BlinkInterval", blinkInterval),
                    new JProperty("BlinkLength",   blinkLength),
                    new JProperty("BlinkOffset",   blinkOffset));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/SpotAngle
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Value": float }
        public async Task LightSetSpotAngle(MessageContext ctx)
        {
            try
            {
                var args      = JObject.Parse(ctx.Payload);
                int entityId  = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                float value   = args["Value"].Value<float>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var light = await GetLight(ctx, structure, entityId, pos);
                if (light == null) return;

                light.SetSpotAngle(value);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Value",      value));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Container device handlers
        //
        // Structure/Device/{name}/Req/get/Container
        // payload: { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public async Task ContainerGet(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var container = await GetContainer(ctx, structure, entityId, pos);
                if (container == null) return;

                var content = container.GetContent();
                var contentArray = new JArray();
                if (content != null)
                    foreach (var item in content)
                        contentArray.Add(ItemStackJson(item));

                var json = new JObject(
                    new JProperty("EntityId",       entityId),
                    new JProperty("DeviceName",     ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",            MessageHelpers.Vec(pos)),
                    new JProperty("VolumeCapacity", container.VolumeCapacity),
                    new JProperty("DecayFactor",    container.DecayFactor),
                    new JProperty("Content",        contentArray));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/call/AddItems
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Type": int, "Count": int }
        public async Task ContainerAddItems(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int type     = args["Type"].Value<int>();
                int count    = args["Count"].Value<int>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var container = await GetContainer(ctx, structure, entityId, pos);
                if (container == null) return;

                int couldNotAdd = container.AddItems(type, count);
                var json = new JObject(
                    new JProperty("EntityId",    entityId),
                    new JProperty("DeviceName",  ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",         MessageHelpers.Vec(pos)),
                    new JProperty("Type",        type),
                    new JProperty("Count",       count),
                    new JProperty("CouldNotAdd", couldNotAdd));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/call/RemoveItems
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Type": int, "Count": int }
        public async Task ContainerRemoveItems(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int type     = args["Type"].Value<int>();
                int count    = args["Count"].Value<int>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var container = await GetContainer(ctx, structure, entityId, pos);
                if (container == null) return;

                int couldNotRemove = container.RemoveItems(type, count);
                var json = new JObject(
                    new JProperty("EntityId",       entityId),
                    new JProperty("DeviceName",     ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",            MessageHelpers.Vec(pos)),
                    new JProperty("Type",           type),
                    new JProperty("Count",          count),
                    new JProperty("CouldNotRemove", couldNotRemove));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/call/SetContent
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Content": [{Id,Count,...}] }
        public async Task ContainerSetContent(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var contentArray = args["Content"] as JArray;
                if (contentArray == null)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                        MessageHelpers.ErrorJson("Content must be a JSON array"));
                    return;
                }
                var items = new List<ItemStack>();
                foreach (var t in contentArray)
                    items.Add(ParseItemStack(t));

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var container = await GetContainer(ctx, structure, entityId, pos);
                if (container == null) return;

                container.SetContent(items);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Count",      items.Count));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/call/Clear
        // payload: { "EntityId": int, "Pos": {X,Y,Z} }
        public async Task ContainerClear(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var container = await GetContainer(ctx, structure, entityId, pos);
                if (container == null) return;

                container.Clear();
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/call/Contains
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Type": int }
        public async Task ContainerContains(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int type     = args["Type"].Value<int>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var container = await GetContainer(ctx, structure, entityId, pos);
                if (container == null) return;

                bool result = container.Contains(type);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Type",       type),
                    new JProperty("Contains",   result));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/call/GetTotalItems
        // payload: { "EntityId": int, "Pos": {X,Y,Z}, "Type": int }
        public async Task ContainerGetTotalItems(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int type     = args["Type"].Value<int>();

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var container = await GetContainer(ctx, structure, entityId, pos);
                if (container == null) return;

                int count = container.GetTotalItems(type);
                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("Type",       type),
                    new JProperty("Count",      count));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Teleporter device handlers
        //
        // Structure/Device/{name}/Req/get/Teleporter
        // payload: { "EntityId": int, "Pos": {X,Y,Z} }
        // =========================================================================
        public async Task TeleporterGet(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var tp = await GetTeleporter(ctx, structure, entityId, pos);
                if (tp == null) return;

                var td = tp.TargetData;
                var json = new JObject(
                    new JProperty("EntityId",                entityId),
                    new JProperty("DeviceName",              ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",                     MessageHelpers.Vec(pos)),
                    new JProperty("TargetEntityNameOrGroup", td.TargetEntityNameOrGroup),
                    new JProperty("TargetPlayfield",         td.TargetPlayfield),
                    new JProperty("TargetSolarSystemName",   td.TargetSolarSystemName),
                    new JProperty("Origin",                  td.Origin));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        // Structure/Device/{name}/Req/set/Teleporter
        // payload: { "EntityId": int, "Pos": {X,Y,Z},
        //            "TargetEntityNameOrGroup": string, "TargetPlayfield": string,
        //            "TargetSolarSystemName": string, "Origin": byte }
        public async Task TeleporterSet(MessageContext ctx)
        {
            try
            {
                var args     = JObject.Parse(ctx.Payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructureForEntity(ctx, entityId);
                if (structure == null) return;

                var tp = await GetTeleporter(ctx, structure, entityId, pos);
                if (tp == null) return;

                var td = tp.TargetData;
                if (args["TargetEntityNameOrGroup"] != null) td.TargetEntityNameOrGroup = args["TargetEntityNameOrGroup"].Value<string>();
                if (args["TargetPlayfield"]         != null) td.TargetPlayfield         = args["TargetPlayfield"].Value<string>();
                if (args["TargetSolarSystemName"]   != null) td.TargetSolarSystemName   = args["TargetSolarSystemName"].Value<string>();
                if (args["Origin"]                  != null) td.Origin                  = args["Origin"].Value<byte>();
                tp.TargetData = td;

                var json = new JObject(
                    new JProperty("EntityId",                entityId),
                    new JProperty("DeviceName",              ctx.ParsedTopic.DeviceName),
                    new JProperty("Pos",                     MessageHelpers.Vec(pos)),
                    new JProperty("TargetEntityNameOrGroup", td.TargetEntityNameOrGroup),
                    new JProperty("TargetPlayfield",         td.TargetPlayfield),
                    new JProperty("TargetSolarSystemName",   td.TargetSolarSystemName),
                    new JProperty("Origin",                  td.Origin));

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
