using System.Collections.Generic;

namespace ESB.TopicHandlers
{
    public partial class ApplicationHandler
    {
        // _opDefs is the static metadata table for all App scope operations.
        // Each entry supports .Describe (and future .Example, .Validate) meta-operations.
        static readonly Dictionary<string, HandlerHelper.OpDef> _opDefs =
            new Dictionary<string, HandlerHelper.OpDef>
        {
            ["GameTicks"] = new HandlerHelper.OpDef(
                summary: "Returns the current game tick count.",
                output: new[] { new HandlerHelper.FieldDef("GameTicks", "long") }),

            ["Mode"] = new HandlerHelper.OpDef(
                summary: "Returns the current application mode as a string."),

            ["State"] = new HandlerHelper.OpDef(
                summary: "Returns the current application state as a string."),

            ["LocalPlayer"] = new HandlerHelper.OpDef(
                summary: "Returns position, vitals, and identity data for the local player. Returns null if no active local player.",
                output: new[]
                {
                    new HandlerHelper.FieldDef("Id",          "int"),
                    new HandlerHelper.FieldDef("Name",        "string"),
                    new HandlerHelper.FieldDef("Position",    "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("Rotation",    "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("Forward",     "Vec3 {X,Y,Z}"),
                    new HandlerHelper.FieldDef("Health",      "float"),
                    new HandlerHelper.FieldDef("HealthMax",   "float"),
                    new HandlerHelper.FieldDef("Food",        "float"),
                    new HandlerHelper.FieldDef("FoodMax",     "float"),
                    new HandlerHelper.FieldDef("Stamina",     "float"),
                    new HandlerHelper.FieldDef("StaminaMax",  "float"),
                    new HandlerHelper.FieldDef("Oxygen",      "float"),
                    new HandlerHelper.FieldDef("OxygenMax",   "float"),
                    new HandlerHelper.FieldDef("Credits",     "double"),
                    new HandlerHelper.FieldDef("SteamId",     "string"),
                    new HandlerHelper.FieldDef("FactionRole", "string"),
                    new HandlerHelper.FieldDef("IsPilot",     "bool"),
                }),

            ["ModApiProperties"] = new HandlerHelper.OpDef(
                summary: "Reports which ModApi subsystems are available in the current game mode.",
                output: new[]
                {
                    new HandlerHelper.FieldDef("ClientPlayfield", "string", note: "\"set\" or \"null\""),
                    new HandlerHelper.FieldDef("Network",         "string", note: "\"set\" or \"null\""),
                    new HandlerHelper.FieldDef("GUI",             "string", note: "\"set\" or \"null\""),
                    new HandlerHelper.FieldDef("PDA",             "string", note: "\"set\" or \"null\""),
                    new HandlerHelper.FieldDef("Scripting",       "string", note: "\"set\" or \"null\""),
                    new HandlerHelper.FieldDef("SoundPlayer",     "string", note: "\"set\" or \"null\""),
                    new HandlerHelper.FieldDef("Application",     "string", note: "\"set\" or \"null\""),
                }),

            ["AllPlayfields"] = new HandlerHelper.OpDef(
                summary: "Returns the list of all playfields known to the server.",
                output: new[]
                {
                    new HandlerHelper.FieldDef("[]",            "array"),
                    new HandlerHelper.FieldDef("PlayfieldName", "string"),
                    new HandlerHelper.FieldDef("PlayfieldType", "string"),
                    new HandlerHelper.FieldDef("IsInstance",    "bool"),
                }),

            ["PfServerInfos"] = new HandlerHelper.OpDef(
                summary: "Returns playfield server connection details."),

            ["PlayerEntityIds"] = new HandlerHelper.OpDef(
                summary: "Returns an array of entity IDs for all currently connected players.",
                output: new[] { new HandlerHelper.FieldDef("[]", "int array") }),

            ["BlockAndItemMapping"] = new HandlerHelper.OpDef(
                summary: "Returns a mapping of block and item numeric IDs to their display names."),

            ["GetPathFor"] = new HandlerHelper.OpDef(
                summary: "Returns the absolute filesystem path for a named AppFolder.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("AppFolder", "string (AppFolder enum)", required: true,
                        note: "Root | Content | SaveGame | Mod | ActiveScenario | Cache | Dedicated"),
                },
                output: new[]
                {
                    new HandlerHelper.FieldDef("AppFolder", "string", note: "Echo of requested enum name"),
                    new HandlerHelper.FieldDef("Path",      "string", note: "Absolute filesystem path"),
                }),

            ["GetPlayerDataFor"] = new HandlerHelper.OpDef(
                summary: "Returns extended player data for a given entity ID.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("PlayerEntityId", "int", required: true),
                }),

            ["GetStructure"] = new HandlerHelper.OpDef(
                summary: "Returns structure metadata for a single entity ID (async callback).",
                input: new[]
                {
                    new HandlerHelper.FieldDef("EntityId", "int", required: true),
                }),

            ["GetStructures"] = new HandlerHelper.OpDef(
                summary: "Returns structure metadata for all structures matching the given filter (async callback).",
                input: new[]
                {
                    new HandlerHelper.FieldDef("PlayfieldName", "string", note: "Filter by playfield; required if no FactionData"),
                    new HandlerHelper.FieldDef("FactionId",     "byte",   note: "Filter by faction; requires FactionGroup"),
                    new HandlerHelper.FieldDef("FactionGroup",  "byte",   note: "Filter by faction group; requires FactionId"),
                    new HandlerHelper.FieldDef("EntityType",    "string", note: "Optional EntityType enum filter"),
                }),

            ["SendChatMessage"] = new HandlerHelper.OpDef(
                summary: "Broadcasts a chat message.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("Text",               "string", required: true),
                    new HandlerHelper.FieldDef("Channel",            "string", note: "Global | Faction | SinglePlayer; default Global"),
                    new HandlerHelper.FieldDef("SenderType",         "string", note: "ServerInfo | Player | ...; default ServerInfo"),
                    new HandlerHelper.FieldDef("SenderEntityId",     "int"),
                    new HandlerHelper.FieldDef("SenderNameOverride", "string"),
                    new HandlerHelper.FieldDef("RecipientEntityId",  "int"),
                    new HandlerHelper.FieldDef("IsTextLocaKey",      "bool"),
                    new HandlerHelper.FieldDef("Arg1",               "string"),
                    new HandlerHelper.FieldDef("Arg2",               "string"),
                },
                output: new[] { new HandlerHelper.FieldDef("ok", "bool") }),

            ["ShowDialogBox"] = new HandlerHelper.OpDef(
                summary: "Displays a dialog box to a player. The player response is published as EMP/.../Evt/App/DialogResponse.",
                input: new[]
                {
                    new HandlerHelper.FieldDef("PlayerEntityId",    "int",      note: "Defaults to local player"),
                    new HandlerHelper.FieldDef("TitleText",         "string",   note: "Default: \"Dialog\""),
                    new HandlerHelper.FieldDef("BodyText",          "string"),
                    new HandlerHelper.FieldDef("ButtonTexts",       "string[]"),
                    new HandlerHelper.FieldDef("CloseOnLinkClick",  "bool",     note: "Default: true"),
                    new HandlerHelper.FieldDef("ButtonIdxForEsc",   "int",      note: "Default: -1"),
                    new HandlerHelper.FieldDef("ButtonIdxForEnter", "int",      note: "Default: -1"),
                    new HandlerHelper.FieldDef("MaxChars",          "int",      note: "Default: 0"),
                    new HandlerHelper.FieldDef("Placeholder",       "string"),
                    new HandlerHelper.FieldDef("InitialContent",    "string"),
                    new HandlerHelper.FieldDef("CustomValue",       "int"),
                },
                output: new[] { new HandlerHelper.FieldDef("ok", "bool") }),

            ["Describe"] = new HandlerHelper.OpDef(
                summary: "Returns the catalog of all App scope operations with names and summaries."),
        };
    }
}
