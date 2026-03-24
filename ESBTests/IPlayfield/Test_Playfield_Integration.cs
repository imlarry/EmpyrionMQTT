using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.IPlayfield;

/// <summary>
/// Integration tests for the Playfield topic handlers.
/// Requires the game to be running with the ESB mod loaded and the saved game state
/// described in <see cref="KnownState"/> active (player standing on Akua).
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Playfield_Integration
{
    private const int EID = KnownState.BaseEntityId;

    // -------------------------------------------------------------------------
    // Playfield.Info — read-only, no arguments needed
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Info_ReturnsPlayfieldProperties()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Playfield.Info", "{}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Playfield.Info/", topic);
        Assert.NotNull(payload["Name"]);
        Assert.NotNull(payload["PlayfieldType"]);
        Assert.NotNull(payload["IsPvP"]);
        Assert.NotNull(payload["SolarSystemCoordinates"]);
        // Must be on Akua for the known-state save
        Assert.Equal(KnownState.Playfield, payload["Name"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // Playfield.IsStructureDeviceLocked — read-only
    // Uses the Fridge device at the lever-switch block position on the base
    // -------------------------------------------------------------------------
    [Fact]
    public async Task IsStructureDeviceLocked_FridgeDevice_ReturnsBool()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Playfield.IsStructureDeviceLocked",
            $"{{\"StructureId\":{EID},\"PosInStructure\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Playfield.IsStructureDeviceLocked/", topic);
        Assert.Equal(EID, payload["StructureId"]!.Value<int>());
        Assert.NotNull(payload["IsStructureDeviceLocked"]);
        // Must be a bool
        Assert.IsType<bool>(payload["IsStructureDeviceLocked"]!.ToObject<object>());
    }

    // -------------------------------------------------------------------------
    // Playfield.MoveEntity — mutation (IEntity.Pos set)
    // Entity must be in the LoadedEntity cache; accepts X if not cached.
    // Moves to the same position it is already at (no visible effect).
    // -------------------------------------------------------------------------
    [Fact]
    public async Task MoveEntity_BaseEntity_ReturnsPositionOrException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Playfield.MoveEntity",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.BaseGlobalPos}}}");

        // R (cached, moved) or X (not in LoadedEntity cache — valid in non-SP modes)
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Playfield.MoveEntity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Playfield.MoveEntity/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.Equal(EID, payload["EntityId"]!.Value<int>());
            Assert.NotNull(payload["Pos"]);
        }
    }
}
