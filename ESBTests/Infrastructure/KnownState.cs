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
    public const string Playfield   = "Akua";

    // VNS Akua — the test base entity
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

    // LCD panel on the base — place an LCD panel with this custom name.
    // Position is discovered at runtime via Structure.GetDevicePositions.
    public const string LcdName = "InfoLcd";

    // Light block on the base — place a light with this custom name.
    // Position is discovered at runtime via Structure.GetDevicePositions.
    public const string LightName = "Light";

    // Teleporter device on the base — place a teleporter pad with this custom name.
    // Position is discovered at runtime via Structure.GetDevicePositions.
    public const string TeleporterName = "Teleport";

}
