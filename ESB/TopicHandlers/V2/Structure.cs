using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
{
    public class Structure
    {
        private readonly ContextData _ctx;

        public Structure(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Structure.Info",                   Info);
            _ctx.Messenger.RegisterHandler("V2.Structure.Tanks",                  Tanks);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetAllCustomDeviceNames", GetAllCustomDeviceNames);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetDevicePositions",     GetDevicePositions);
            _ctx.Messenger.RegisterHandler("V2.Structure.SetFaction",             SetFaction);
            _ctx.Messenger.RegisterHandler("V2.Structure.AddTankContent",         AddTankContent);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetDockedVessels",       GetDockedVessels);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetPassengers",          GetPassengers);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetBlockSignals",        GetBlockSignals);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetControlPanelSignals", GetControlPanelSignals);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetSignalState",         GetSignalState);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetSignalReceivers",     GetSignalReceivers);
            _ctx.Messenger.RegisterHandler("V2.Structure.GetSendSignalName",      GetSendSignalName);
            _ctx.Messenger.RegisterHandler("V2.Structure.StructToGlobalPos",      StructToGlobalPos);
            _ctx.Messenger.RegisterHandler("V2.Structure.GlobalToStructPos",      GlobalToStructPos);
        }

        // Shared lookup: entity from cache → structure, or send an exception and return null.
        private async Task<Eleon.Modding.IStructure> GetStructure(string topic, int entityId)
        {
            var entity = _ctx.GetEntityByKey(entityId);
            if (entity == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} not found in LoadedEntity cache"));
                return null;
            }
            if (entity.Structure == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} has no Structure (not a structure entity)"));
                return null;
            }
            return entity.Structure;
        }

        // -----------------------------------------------------------------------
        // V2.Structure.Info — all scalar IStructure properties
        // -----------------------------------------------------------------------
        public async Task Info(string topic, string payload)
        {
            try
            {
                int entityId = JObject.Parse(payload)["EntityId"].Value<int>();
                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    JToken S(Func<object> getter)
                    {
                        try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
                        catch { return JValue.CreateNull(); }
                    }

                    json = new JObject();
                    json.Add("EntityId",              entityId);
                    json.Add("Id",                    S(() => structure.Id));
                    json.Add("IsReady",               S(() => structure.IsReady));
                    json.Add("MinPos",                S(() => MessageHelpers.Vec(structure.MinPos)));
                    json.Add("MaxPos",                S(() => MessageHelpers.Vec(structure.MaxPos)));
                    json.Add("PlayerCreatedSteamId",  S(() => structure.PlayerCreatedSteamId));
                    json.Add("CoreType",              S(() => structure.CoreType.ToString()));
                    json.Add("SizeClass",             S(() => structure.SizeClass));
                    json.Add("LastVisitedTicks",      S(() => structure.LastVisitedTicks));

                    if (structure.IsReady)
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
                        json.Add("PilotEntityId",        S(() => structure.Pilot?.Id));
                    }
                    await Task.CompletedTask;
                });

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.Tanks — FuelTank, OxygenTank, PentaxidTank
        // -----------------------------------------------------------------------
        public async Task Tanks(string topic, string payload)
        {
            try
            {
                int entityId = JObject.Parse(payload)["EntityId"].Value<int>();
                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    JObject TankJson(IStructureTank tank)
                    {
                        if (tank == null) return null;
                        return new JObject(
                            new JProperty("Capacity",           tank.Capacity),
                            new JProperty("Content",            tank.Content),
                            new JProperty("UsesIntegerAmounts", tank.UsesIntegerAmounts));
                    }

                    json = new JObject(
                        new JProperty("EntityId",     entityId),
                        new JProperty("FuelTank",     TankJson(structure.FuelTank)),
                        new JProperty("OxygenTank",   TankJson(structure.OxygenTank)),
                        new JProperty("PentaxidTank", TankJson(structure.PentaxidTank)));
                    await Task.CompletedTask;
                });

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetAllCustomDeviceNames — returns string[] of named devices
        // -----------------------------------------------------------------------
        public async Task GetAllCustomDeviceNames(string topic, string payload)
        {
            try
            {
                int entityId = JObject.Parse(payload)["EntityId"].Value<int>();
                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                string[] names = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    names = structure.GetAllCustomDeviceNames();
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("DeviceNames", new JArray(names ?? new string[0])));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetDevicePositions — block positions for a named device
        // Payload: { "EntityId": n, "DeviceName": "MyContainer" }
        // -----------------------------------------------------------------------
        public async Task GetDevicePositions(string topic, string payload)
        {
            try
            {
                var args       = JObject.Parse(payload);
                int entityId   = args["EntityId"].Value<int>();
                string devName = args["DeviceName"].Value<string>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JArray positions = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var list = structure.GetDevicePositions(devName);
                    positions = new JArray();
                    if (list != null)
                        foreach (var pos in list)
                            positions.Add(MessageHelpers.Vec(pos));
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("DeviceName", devName),
                    new JProperty("Positions",  positions));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.SetFaction
        // Payload: { "EntityId": n, "FactionGroup": "Faction", "FactionEntityId": m }
        // -----------------------------------------------------------------------
        public async Task SetFaction(string topic, string payload)
        {
            try
            {
                var args          = JObject.Parse(payload);
                int entityId      = args["EntityId"].Value<int>();
                int factionEntity = args["FactionEntityId"].Value<int>();
                if (!Enum.TryParse(args["FactionGroup"].Value<string>(), true, out FactionGroup group))
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"Unknown FactionGroup: {args["FactionGroup"]}"));
                    return;
                }

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    structure.SetFaction(group, factionEntity);
                    var json = new JObject(
                        new JProperty("EntityId",      entityId),
                        new JProperty("FactionGroup",  group.ToString()),
                        new JProperty("FactionEntityId", factionEntity));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetDockedVessels — entity IDs of all docked vessels
        // -----------------------------------------------------------------------
        public async Task GetDockedVessels(string topic, string payload)
        {
            try
            {
                int entityId = JObject.Parse(payload)["EntityId"].Value<int>();
                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JArray vessels = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    vessels = new JArray();
                    var list = structure.GetDockedVessels();
                    if (list != null)
                        foreach (var v in list)
                            vessels.Add(new JObject(
                                new JProperty("Id",      v.Entity?.Id),
                                new JProperty("Name",    v.Entity?.Name),
                                new JProperty("IsReady", v.IsReady)));
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("DockedVessels", vessels));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetPassengers — players currently aboard
        // -----------------------------------------------------------------------
        public async Task GetPassengers(string topic, string payload)
        {
            try
            {
                int entityId = JObject.Parse(payload)["EntityId"].Value<int>();
                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JArray passengers = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    passengers = new JArray();
                    var list = structure.GetPassengers();
                    if (list != null)
                        foreach (var p in list)
                            passengers.Add(new JObject(
                                new JProperty("Id",      p.Id),
                                new JProperty("Name",    p.Name),
                                new JProperty("SteamId", p.SteamId)));
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("Passengers", passengers));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetBlockSignals — all block-sourced sender signals
        // Payload: { "EntityId": n, "Filter": "optional" }
        // -----------------------------------------------------------------------
        public async Task GetBlockSignals(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                string filter = args["Filter"]?.Value<string>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JArray signals = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    signals = new JArray();
                    var list = structure.GetBlockSignals(filter);
                    if (list != null)
                        foreach (var s in list)
                            signals.Add(new JObject(
                                new JProperty("Name",     s.Name),
                                new JProperty("BlockPos", s.BlockPos.HasValue ? (JToken)MessageHelpers.Vec(s.BlockPos.Value) : JValue.CreateNull()),
                                new JProperty("Index",    s.Index)));
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("Signals",  signals));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetControlPanelSignals — control panel defined signals
        // -----------------------------------------------------------------------
        public async Task GetControlPanelSignals(string topic, string payload)
        {
            try
            {
                int entityId = JObject.Parse(payload)["EntityId"].Value<int>();
                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JArray signals = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    signals = new JArray();
                    var list = structure.GetControlPanelSignals();
                    if (list != null)
                        foreach (var s in list)
                            signals.Add(new JObject(
                                new JProperty("Name",     s.Name),
                                new JProperty("BlockPos", s.BlockPos.HasValue ? (JToken)MessageHelpers.Vec(s.BlockPos.Value) : JValue.CreateNull()),
                                new JProperty("Index",    s.Index)));
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId", entityId),
                    new JProperty("Signals",  signals));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetSignalState — current on/off state of a named signal
        // Payload: { "EntityId": n, "SignalName": "MySignal" }
        // -----------------------------------------------------------------------
        public async Task GetSignalState(string topic, string payload)
        {
            try
            {
                var args        = JObject.Parse(payload);
                int entityId    = args["EntityId"].Value<int>();
                string sigName  = args["SignalName"].Value<string>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                bool state = false;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    state = structure.GetSignalState(sigName);
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("SignalName", sigName),
                    new JProperty("State",      state));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetSignalReceivers — blocks that listen to a named signal
        // Payload: { "EntityId": n, "SignalName": "MySignal" }
        // -----------------------------------------------------------------------
        public async Task GetSignalReceivers(string topic, string payload)
        {
            try
            {
                var args       = JObject.Parse(payload);
                int entityId   = args["EntityId"].Value<int>();
                string sigName = args["SignalName"].Value<string>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JArray receivers = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    receivers = new JArray();
                    var list = structure.GetSignalReceivers(sigName);
                    if (list != null)
                        foreach (var r in list)
                            receivers.Add(new JObject(
                                new JProperty("Func",        r.Func.ToString()),
                                new JProperty("Behavior",    r.Behavior.ToString()),
                                new JProperty("BlockPos",    MessageHelpers.Vec(r.BlockPos)),
                                new JProperty("IsInverting", r.IsInverting)));
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("SignalName", sigName),
                    new JProperty("Receivers",  receivers));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GetSendSignalName — signal name at a block position, or null
        // Payload: { "EntityId": n, "Pos": {"X":0,"Y":0,"Z":0} }
        // -----------------------------------------------------------------------
        public async Task GetSendSignalName(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                string sigName = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    sigName = structure.GetSendSignalName(pos);
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId",   entityId),
                    new JProperty("Pos",        MessageHelpers.Vec(pos)),
                    new JProperty("SignalName", sigName));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.StructToGlobalPos — local block position → world position
        // Payload: { "EntityId": n, "StructPos": {"X":0,"Y":0,"Z":0} }
        // -----------------------------------------------------------------------
        public async Task StructToGlobalPos(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                int entityId  = args["EntityId"].Value<int>();
                VectorInt3 structPos = MessageHelpers.ParseVecInt3(args["StructPos"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                Vector3 globalPos = default;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    globalPos = structure.StructToGlobalPos(structPos);
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId",  entityId),
                    new JProperty("StructPos", MessageHelpers.Vec(structPos)),
                    new JProperty("GlobalPos", MessageHelpers.Vec(globalPos)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.GlobalToStructPos — world position → local block position
        // Payload: { "EntityId": n, "Pos": {"X":0.0,"Y":0.0,"Z":0.0} }
        // -----------------------------------------------------------------------
        public async Task GlobalToStructPos(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                Vector3 globalPos = MessageHelpers.ParseVec3(args["Pos"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                VectorInt3 structPos = default;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    structPos = structure.GlobalToStructPos(globalPos);
                    await Task.CompletedTask;
                });

                var json = new JObject(
                    new JProperty("EntityId",  entityId),
                    new JProperty("GlobalPos", MessageHelpers.Vec(globalPos)),
                    new JProperty("StructPos", MessageHelpers.Vec(structPos)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Structure.AddTankContent — add material to a fuel, oxygen, or pentaxid tank
        // Payload: { "EntityId": n, "TankType": "Fuel"|"Oxygen"|"Pentaxid", "Amount": 100.0 }
        // -----------------------------------------------------------------------
        public async Task AddTankContent(string topic, string payload)
        {
            try
            {
                var args       = JObject.Parse(payload);
                int entityId   = args["EntityId"].Value<int>();
                string tankType = args["TankType"].Value<string>();
                float amount   = args["Amount"].Value<float>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    IStructureTank tank;
                    if (tankType == "Fuel")           tank = structure.FuelTank;
                    else if (tankType == "Oxygen")    tank = structure.OxygenTank;
                    else if (tankType == "Pentaxid")  tank = structure.PentaxidTank;
                    else                              tank = null;

                    if (tank == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"Unknown or unavailable TankType: {tankType}. Valid values: Fuel, Oxygen, Pentaxid"));
                        return;
                    }

                    tank.AddContent(amount);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("TankType", tankType),
                        new JProperty("Amount",   amount),
                        new JProperty("Content",  tank.Content),
                        new JProperty("Capacity", tank.Capacity));
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
