using System.Collections.Generic;

namespace ESB.TopicHandlers
{
    public partial class PlayerHandler
    {
        static readonly Dictionary<string, HandlerHelper.OpDef> _opDefs =
            new Dictionary<string, HandlerHelper.OpDef>
        {
            ["GetProperties"] = new HandlerHelper.OpDef(
                summary: "Returns a JSON object containing local player properties. Optionally filtered to a subset via the Properties input field.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("Properties", "string[]", note: "Optional list of property names to include. Omit for all."),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("Id",                       "int"),
                    new HandlerHelper.FieldDef("Name",                     "string"),
                    new HandlerHelper.FieldDef("SteamId",                  "string"),
                    new HandlerHelper.FieldDef("SteamOwnerId",             "string"),
                    new HandlerHelper.FieldDef("StartPlayfield",           "string"),
                    new HandlerHelper.FieldDef("Origin",                   "int"),
                    new HandlerHelper.FieldDef("Permission",               "int"),
                    new HandlerHelper.FieldDef("Health",                   "float"),
                    new HandlerHelper.FieldDef("HealthMax",                "float"),
                    new HandlerHelper.FieldDef("Oxygen",                   "float"),
                    new HandlerHelper.FieldDef("OxygenMax",                "float"),
                    new HandlerHelper.FieldDef("Stamina",                  "float"),
                    new HandlerHelper.FieldDef("StaminaMax",               "float"),
                    new HandlerHelper.FieldDef("Food",                     "float"),
                    new HandlerHelper.FieldDef("FoodMax",                  "float"),
                    new HandlerHelper.FieldDef("Radiation",                "float"),
                    new HandlerHelper.FieldDef("RadiationMax",             "float"),
                    new HandlerHelper.FieldDef("BodyTemp",                 "float"),
                    new HandlerHelper.FieldDef("BodyTempMax",              "float"),
                    new HandlerHelper.FieldDef("Credits",                  "double"),
                    new HandlerHelper.FieldDef("ExperiencePoints",         "int"),
                    new HandlerHelper.FieldDef("UpgradePoints",            "int"),
                    new HandlerHelper.FieldDef("Kills",                    "int"),
                    new HandlerHelper.FieldDef("Died",                     "int"),
                    new HandlerHelper.FieldDef("Ping",                     "int"),
                    new HandlerHelper.FieldDef("HomeBaseId",               "int"),
                    new HandlerHelper.FieldDef("IsPilot",                  "bool"),
                    new HandlerHelper.FieldDef("IsLocal",                  "bool"),
                    new HandlerHelper.FieldDef("IsProxy",                  "bool"),
                    new HandlerHelper.FieldDef("IsPoi",                    "bool"),
                    new HandlerHelper.FieldDef("BelongsTo",                "int"),
                    new HandlerHelper.FieldDef("DockedTo",                 "int"),
                    new HandlerHelper.FieldDef("Type",                     "string (EntityType enum)"),
                    new HandlerHelper.FieldDef("FactionData",              "object {Group, Id}"),
                    new HandlerHelper.FieldDef("Faction",                  "object {Group, Id}"),
                    new HandlerHelper.FieldDef("FactionRole",              "string (FactionRole enum)"),
                    new HandlerHelper.FieldDef("CurrentStructureId",       "int or null"),
                    new HandlerHelper.FieldDef("CurrentStructureEntityId", "int or null"),
                    new HandlerHelper.FieldDef("DrivingEntityId",          "int or null"),
                    new HandlerHelper.FieldDef("Position",                 "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("Forward",                  "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("Rotation",                 "Vec4 {X,Y,Z,W}"),
                    new HandlerHelper.FieldDef("Toolbar",                  "ItemStack[] [{Id,Count,SlotIdx,Ammo,Decay}]"),
                    new HandlerHelper.FieldDef("Bag",                      "ItemStack[] [{Id,Count,SlotIdx,Ammo,Decay}]"),
                }),

            ["Teleport"] = new HandlerHelper.OpDef(
                summary: "Teleports the local player to a position, optionally on a different playfield.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("Pos",       "Vec3 {X,Y,Z}", required: true),
                    new HandlerHelper.FieldDef("Playfield", "string",       note: "Target playfield name. Omit for same playfield."),
                    new HandlerHelper.FieldDef("Rot",       "Vec3 {X,Y,Z}", note: "Required when Playfield is provided."),
                },
                output: new[] { new HandlerHelper.FieldDef("ok", "bool") }),

            ["DamageEntity"] = new HandlerHelper.OpDef(
                summary: "Applies damage to the local player entity.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("DamageAmount", "int"),
                    new HandlerHelper.FieldDef("DamageType",   "int"),
                },
                output: new[] { new HandlerHelper.FieldDef("ok", "bool") }),

            ["Describe"] = new HandlerHelper.OpDef(
                summary: "Returns the catalog of all Player scope operations with names and summaries."),
        };
    }
}
