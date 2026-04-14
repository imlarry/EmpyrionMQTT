namespace ESBTests.Infrastructure;

/// <summary>
/// Constants describing the saved game state used for integration tests.
/// Save the game in this state before running — entity IDs are stable across reloads.
///
/// Saved state: VNS Akua base on Akua moon.
///   - A Constructor, ConstructorBuffer, and Fridge device are placed
///   - A Lever Switch at block (2,130,1) named "Fridge" (signal same name as device)
/// </summary>
public static class KnownState
{
    public const string AppId       = "Client";
    // V1 (ModBase/DediAPI) is only initialized on DedicatedServer — route V1 requests here.
    public const string V1AppId     = "DedicatedServer";
    public const string Playfield   = "Akua";

    // VNS Akua - the test base entity
    public const int    BaseEntityId    = 5320;
    public const string BaseName        = "VNS Akua";

    // Block positions (struct-space)
    public const string LeverSwitchBlock = "{\"X\":2,\"Y\":130,\"Z\":1}";

    // Signal / device names
    public const string SignalName   = "Fridge";
    public const string DeviceName1  = "Constructor";
    public const string DeviceName2  = "Fridge";

    // Global coordinates of the base (approximate, for GlobalToStructPos tests)
    public const string BaseGlobalPos = "{\"X\":-156.5,\"Y\":51.0,\"Z\":50.5}";

    // Player spawn point above the base (for V2 Player.Teleport tests — uppercase X,Y,Z)
    public const string PlayerSpawnPos = "{\"X\":-155.3,\"Y\":53.1,\"Z\":29.3}";

    // V1 ChangePlayfield return spawn — lowercase x,y,z as required by ParsePVec.
    // Y is advisory; raise it if the player clips into terrain on arrival.
    public const string PlayerSpawnPosV1 = "{\"x\":-155.3,\"y\":53.0,\"z\":29.3}";
    public const string PlayerSpawnRotV1 = "{\"x\":0,\"y\":0,\"z\":0}";

    // Local player entity ID — set to the active player's entity ID before running V1 tests.
    // Visible in-game via console: "di" or via V2.Application.GetPlayerEntityIds.
    public const int PlayerEntityId = 1042;

    // LCD panel on the base — place an LCD panel with this custom name.
    // Position is discovered at runtime via Structure.GetDevicePositions.
    public const string LcdName = "InfoLcd";

    // Light block on the base — place a light with this custom name.
    // Position is discovered at runtime via Structure.GetDevicePositions.
    public const string LightName = "Light";

    // Teleporter device on the base — place a teleporter pad with this custom name.
    // Position is discovered at runtime via Structure.GetDevicePositions.
    public const string TeleporterName = "Teleport";

    /// <summary>
    /// Constants for Tier 3 destructive tests. All values must be confirmed in the
    /// live save before running Integration_Destructive tests.
    /// </summary>
    public static class Baseline
    {
        // A second playfield that exists in the save, used by the ChangePlayfield test.
        // The player is teleported there and immediately returned to Akua/PlayerSpawnPos.
        public const string TestPlayfield = "Skillon";

        // Spawn coordinates on TestPlayfield — lowercase x,y,z as required by ParsePVec.
        // Y is advisory; the game places the player at terrain height on arrival.
        // Raise Y if the player clips into terrain or spawns underground.
        public const string TestPlayfieldSpawnPos = "{\"x\":5,\"y\":100,\"z\":5}";
        public const string TestPlayfieldSpawnRot = "{\"x\":0,\"y\":0,\"z\":0}";

        // V2 Playfield spawn test constants.
        // SpawnEntityTypeV2: entity type string passed to IPlayfield.SpawnEntity
        //   (e.g. "HV", "SV", "CV", "BA" -- check IEntityType names in the Eleon SDK).
        // V2SpawnPos: global world coordinates on KnownState.Playfield, clear of existing structures.
        //   X,Y,Z uppercase -- V2 uses ParseVec3 which accepts both cases.
        public const string SpawnEntityTypeV2 = "HV";
        public const string V2SpawnPos        = "{\"X\":-180,\"Y\":60,\"Z\":50}";
    }
}
