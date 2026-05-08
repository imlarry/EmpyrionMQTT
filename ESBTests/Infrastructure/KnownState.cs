using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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

    // ESB Test Entity - the test base entity
    public const int    BaseEntityId    = 5320;
    public const string BaseName        = "ESB Test Entity";

    // Block positions (struct-space)
    public const string LeverSwitchBlock = "{\"X\":2,\"Y\":130,\"Z\":1}";

    // Signal / device names
    public const string SignalName   = "Fridge";
    public const string DeviceName1  = "Constructor";
    public const string DeviceName2  = "Fridge";

    // Global coordinates of the base (approximate, for GlobalToStructPos tests)
    public const string BaseGlobalPos = "{\"X\":-156.5,\"Y\":51.0,\"Z\":50.5}";

    // Player spawn point above the base (for V2 Player.Teleport tests — uppercase X,Y,Z)
    public const string PlayerSpawnPos = "{\"X\":-155.3,\"Y\":53.3,\"Z\":29.3}";

    // V1 ChangePlayfield return spawn.
    // Y is advisory; raise it if the player clips into terrain on arrival.
    public const string PlayerSpawnPosV1 = "{\"X\":-155.3,\"Y\":53.3,\"Z\":29.3}";
    public const string PlayerSpawnRotV1 = "{\"X\":0,\"Y\":0,\"Z\":0}";

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
    /// Polls V2.Structure.Info until IsReady is true, then returns.
    /// Throws if the structure is not ready within maxWaitMs.
    /// Call before any test that uses GetDevicePositions or other IsReady-gated operations.
    /// </summary>
    public static async Task WaitForStructureReadyAsync(
        SBTestClient mqtt, int entityId, int maxWaitMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            var payload = await mqtt.RequestAsync(
                "Structure", "Info",
                $"{{\"EntityId\":{entityId}}}");
            if ((bool?)payload["IsReady"] == true)
                return;
            await Task.Delay(500);
        }
        throw new Exception($"Structure {entityId} not ready after {maxWaitMs}ms");
    }

    /// <summary>
    /// Constants for Tier 3 destructive tests. All values must be confirmed in the
    /// live save before running Integration_Destructive tests.
    /// </summary>
    public static class Baseline
    {
        // A second playfield that exists in the save, used by the ChangePlayfield test.
        // The player is teleported there and immediately returned to Akua/PlayerSpawnPos.
        public const string TestPlayfield = "Ningues";

        // Spawn coordinates on TestPlayfield.
        // Y is advisory; the game places the player at terrain height on arrival.
        // Raise Y if the player clips into terrain or spawns underground.
        public const string TestPlayfieldSpawnPos = "{\"X\":5,\"Y\":55,\"Z\":5}";
        public const string TestPlayfieldSpawnRot = "{\"X\":0,\"Y\":0,\"Z\":0}";

        // V2 Playfield spawn test constants.
        // SpawnEntityTypeV2: entity type string passed to IPlayfield.SpawnEntity
        //   (e.g. "HV", "SV", "CV", "BA" -- check IEntityType names in the Eleon SDK).
        // V2SpawnPos: global world coordinates on KnownState.Playfield, clear of existing structures.
        //   X,Y,Z uppercase -- V2 uses ParseVec3 which accepts both cases.
        public const string SpawnEntityTypeV2 = "HV";
        public const string V2SpawnPos        = "{\"X\":-180,\"Y\":60,\"Z\":50}";
    }
}
