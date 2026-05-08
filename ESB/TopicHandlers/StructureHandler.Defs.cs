using Eleon.Modding;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace ESB.TopicHandlers
{
    public partial class StructureHandler
    {
        static readonly Dictionary<string, HandlerHelper.OpDef> _opDefs =
            new Dictionary<string, HandlerHelper.OpDef>
        {
            ["Info"] = new HandlerHelper.OpDef(
                summary: "Returns detailed info for a structure entity on the current playfield.",
                input: new[] { new HandlerHelper.FieldDef("EntityId", "int", required: true) },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",             "int"),
                    new HandlerHelper.FieldDef("Id",                   "int"),
                    new HandlerHelper.FieldDef("IsReady",              "bool"),
                    new HandlerHelper.FieldDef("CoreType",             "string"),
                    new HandlerHelper.FieldDef("SizeClass",            "int"),
                    new HandlerHelper.FieldDef("LastVisitedTicks",     "long"),
                    new HandlerHelper.FieldDef("PlayerCreatedSteamId", "string"),
                    new HandlerHelper.FieldDef("MinPos",               "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("MaxPos",               "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("IsPowered",            "bool",   note: "present when IsReady"),
                    new HandlerHelper.FieldDef("IsOfflineProtectable", "bool",   note: "present when IsReady"),
                    new HandlerHelper.FieldDef("DamageLevel",          "float",  note: "present when IsReady"),
                    new HandlerHelper.FieldDef("BlockCount",           "int",    note: "present when IsReady"),
                    new HandlerHelper.FieldDef("DeviceCount",          "int",    note: "present when IsReady"),
                    new HandlerHelper.FieldDef("LightCount",           "int",    note: "present when IsReady"),
                    new HandlerHelper.FieldDef("TriangleCount",        "int",    note: "present when IsReady"),
                    new HandlerHelper.FieldDef("Fuel",                 "float",  note: "present when IsReady"),
                    new HandlerHelper.FieldDef("PowerOutCapacity",     "float",  note: "present when IsReady"),
                    new HandlerHelper.FieldDef("PowerConsumption",     "float",  note: "present when IsReady"),
                    new HandlerHelper.FieldDef("IsShieldActive",       "bool",   note: "present when IsReady"),
                    new HandlerHelper.FieldDef("ShieldLevel",          "float",  note: "present when IsReady"),
                    new HandlerHelper.FieldDef("TotalMass",            "float",  note: "present when IsReady"),
                    new HandlerHelper.FieldDef("HasLandClaimDevice",   "bool",   note: "present when IsReady"),
                    new HandlerHelper.FieldDef("PilotEntityId",        "int?",   note: "present when IsReady; null if no pilot"),
                }),

            ["Tanks"] = new HandlerHelper.OpDef(
                summary: "Returns fuel, oxygen, and pentaxid tank state for a structure.",
                input: new[] { new HandlerHelper.FieldDef("EntityId", "int", required: true) },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",     "int"),
                    new HandlerHelper.FieldDef("FuelTank",     "object {Capacity, Content, UsesIntegerAmounts}"),
                    new HandlerHelper.FieldDef("OxygenTank",   "object {Capacity, Content, UsesIntegerAmounts}"),
                    new HandlerHelper.FieldDef("PentaxidTank", "object {Capacity, Content, UsesIntegerAmounts}"),
                }),

            ["GetAllCustomDeviceNames"] = new HandlerHelper.OpDef(
                summary: "Returns all custom device names defined on a structure.",
                input: new[] { new HandlerHelper.FieldDef("EntityId", "int", required: true) },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",    "int"),
                    new HandlerHelper.FieldDef("DeviceNames", "string[]"),
                }),

            ["GetDevicePositions"] = new HandlerHelper.OpDef(
                summary: "Returns all block positions for a named device on a structure.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",   "int",    required: true),
                    new HandlerHelper.FieldDef("DeviceName", "string", required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",   "int"),
                    new HandlerHelper.FieldDef("DeviceName", "string"),
                    new HandlerHelper.FieldDef("Positions",  "array of Vec3 {X,Y,Z}"),
                }),

            ["GetDockedVessels"] = new HandlerHelper.OpDef(
                summary: "Returns all vessels currently docked to a structure.",
                input: new[] { new HandlerHelper.FieldDef("EntityId", "int", required: true) },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",      "int"),
                    new HandlerHelper.FieldDef("[]",            "array"),
                    new HandlerHelper.FieldDef("Id",            "int"),
                    new HandlerHelper.FieldDef("Name",          "string"),
                    new HandlerHelper.FieldDef("IsReady",       "bool"),
                }),

            ["GetPassengers"] = new HandlerHelper.OpDef(
                summary: "Returns all passengers currently inside a structure.",
                input: new[] { new HandlerHelper.FieldDef("EntityId", "int", required: true) },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",   "int"),
                    new HandlerHelper.FieldDef("[]",         "array"),
                    new HandlerHelper.FieldDef("Id",         "int"),
                    new HandlerHelper.FieldDef("Name",       "string"),
                    new HandlerHelper.FieldDef("SteamId",    "string"),
                }),

            ["GetBlockSignals"] = new HandlerHelper.OpDef(
                summary: "Returns block-level signals for a structure, optionally filtered by name prefix.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int",    required: true),
                    new HandlerHelper.FieldDef("Filter",   "string", note: "Optional name prefix filter"),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int"),
                    new HandlerHelper.FieldDef("[]",       "array"),
                    new HandlerHelper.FieldDef("Name",     "string"),
                    new HandlerHelper.FieldDef("BlockPos", "Vec3 {X,Y,Z} or null"),
                    new HandlerHelper.FieldDef("Index",    "int"),
                }),

            ["GetControlPanelSignals"] = new HandlerHelper.OpDef(
                summary: "Returns control-panel signals for a structure.",
                input: new[] { new HandlerHelper.FieldDef("EntityId", "int", required: true) },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int"),
                    new HandlerHelper.FieldDef("[]",       "array"),
                    new HandlerHelper.FieldDef("Name",     "string"),
                    new HandlerHelper.FieldDef("BlockPos", "Vec3 {X,Y,Z} or null"),
                    new HandlerHelper.FieldDef("Index",    "int"),
                }),

            ["GetSignalState"] = new HandlerHelper.OpDef(
                summary: "Returns the current on/off state of a named signal on a structure.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",   "int",    required: true),
                    new HandlerHelper.FieldDef("SignalName", "string", required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",   "int"),
                    new HandlerHelper.FieldDef("SignalName", "string"),
                    new HandlerHelper.FieldDef("State",      "bool"),
                }),

            ["GetSignalReceivers"] = new HandlerHelper.OpDef(
                summary: "Returns all signal receiver blocks wired to a named signal on a structure.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",   "int",    required: true),
                    new HandlerHelper.FieldDef("SignalName", "string", required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",   "int"),
                    new HandlerHelper.FieldDef("SignalName", "string"),
                    new HandlerHelper.FieldDef("[]",         "array"),
                    new HandlerHelper.FieldDef("Func",       "string"),
                    new HandlerHelper.FieldDef("Behavior",   "string"),
                    new HandlerHelper.FieldDef("BlockPos",   "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("IsInverting","bool"),
                }),

            ["GetSendSignalName"] = new HandlerHelper.OpDef(
                summary: "Returns the signal name emitted by the block at a given position.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int",          required: true),
                    new HandlerHelper.FieldDef("Pos",      "Vec3 {X,Y,Z}", required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",   "int"),
                    new HandlerHelper.FieldDef("Pos",        "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("SignalName", "string"),
                }),

            ["AddTankContent"] = new HandlerHelper.OpDef(
                summary: "Adds fuel, oxygen, or pentaxid to a tank on a structure.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int",    required: true),
                    new HandlerHelper.FieldDef("TankType", "string", required: true, note: "Fuel | Oxygen | Pentaxid"),
                    new HandlerHelper.FieldDef("Amount",   "float",  required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int"),
                    new HandlerHelper.FieldDef("TankType", "string"),
                    new HandlerHelper.FieldDef("Amount",   "float"),
                    new HandlerHelper.FieldDef("Content",  "float"),
                    new HandlerHelper.FieldDef("Capacity", "float"),
                }),

            ["SetFaction"] = new HandlerHelper.OpDef(
                summary: "Changes the faction ownership of a structure.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",        "int",    required: true),
                    new HandlerHelper.FieldDef("FactionGroup",    "string", required: true, note: "Faction | Alliance | ..."),
                    new HandlerHelper.FieldDef("FactionEntityId", "int",    required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",        "int"),
                    new HandlerHelper.FieldDef("FactionGroup",    "string"),
                    new HandlerHelper.FieldDef("FactionEntityId", "int"),
                }),

            ["StructToGlobalPos"] = new HandlerHelper.OpDef(
                summary: "Converts a structure-local block position to a global world position.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",  "int",          required: true),
                    new HandlerHelper.FieldDef("StructPos", "Vec3 {X,Y,Z}", required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",  "int"),
                    new HandlerHelper.FieldDef("StructPos", "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("GlobalPos", "Vec3 {X,Y,Z}"),
                }),

            ["GlobalToStructPos"] = new HandlerHelper.OpDef(
                summary: "Converts a global world position to a structure-local block position.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int",          required: true),
                    new HandlerHelper.FieldDef("Pos",      "Vec3 {X,Y,Z}", required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId",  "int"),
                    new HandlerHelper.FieldDef("GlobalPos", "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("StructPos", "Vec3 {X,Y,Z}"),
                }),

            ["ScanFloor"] = new HandlerHelper.OpDef(
                summary: "Returns all non-air blocks at a given Y layer of a structure.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int", required: true),
                    new HandlerHelper.FieldDef("Y",        "int", required: true),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int"),
                    new HandlerHelper.FieldDef("Y",        "int"),
                    new HandlerHelper.FieldDef("MinPos",   "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("MaxPos",   "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("[]",       "array"),
                    new HandlerHelper.FieldDef("X",        "int"),
                    new HandlerHelper.FieldDef("Z",        "int"),
                    new HandlerHelper.FieldDef("Type",     "int"),
                    new HandlerHelper.FieldDef("Shape",    "int"),
                    new HandlerHelper.FieldDef("Rotation", "int"),
                }),

            ["GetAllBlocks"] = new HandlerHelper.OpDef(
                summary: "Returns every non-air block in a structure as a compact row-array.",
                input: new[] { new HandlerHelper.FieldDef("EntityId", "int", required: true) },
                output: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int"),
                    new HandlerHelper.FieldDef("Blocks",   "array; first row is header [\"X\",\"Y\",\"Z\",\"Type\",\"Shape\",\"Rotation\",\"Active\"]"),
                }),

            ["Describe"] = new HandlerHelper.OpDef(
                summary: "Returns the catalog of all Structure scope operations with names and summaries."),
        };

        // -------------------------------------------------------------------------
        // Shared serialization helpers
        // -------------------------------------------------------------------------

        static JObject TankJson(IStructureTank tank)
        {
            if (tank == null) return null;
            return new JObject(
                new JProperty("Capacity",           tank.Capacity),
                new JProperty("Content",            tank.Content),
                new JProperty("UsesIntegerAmounts", tank.UsesIntegerAmounts));
        }

    }
}
