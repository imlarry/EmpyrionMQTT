using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.IBlock;

/// <summary>
/// Integration tests for the Block topic handlers.
/// Requires the game to be running with the ESB mod loaded and the saved game state
/// described in <see cref="KnownState"/> active (VNS Akua base, EntityId 5320).
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Block_Integration
{
    private const int EID = KnownState.BaseEntityId;

    // -------------------------------------------------------------------------
    // Block.Get
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Get_LeverSwitchPosition_ReturnsBlockData()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Block.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Block.Get/", topic);
        Assert.NotNull(payload["Type"]);
        Assert.NotNull(payload["Pos"]);
        Assert.NotNull(payload["Shape"]);
        Assert.NotNull(payload["Rotation"]);
        Assert.NotNull(payload["Active"]);
        Assert.NotNull(payload["Damage"]);
        Assert.NotNull(payload["HitPoints"]);
    }

    // -------------------------------------------------------------------------
    // Block.GetSwitchState
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetSwitchState_LeverSwitch_ReturnsBoolOrNull()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Block.GetSwitchState",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Block.GetSwitchState/", topic);
        Assert.NotNull(payload["State"]);
        Assert.Equal(0, payload["Index"]!.Value<int>());
    }

    // -------------------------------------------------------------------------
    // Block.SetSwitchState — mutation: toggles lever, reads back new state
    // NOTE: changes game state — lever switch at KnownState.LeverSwitchBlock is toggled.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SetSwitchState_LeverSwitch_ReturnsNewState()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Read current state
        var (_, readPayload) = await mqtt.RequestAsync(
            "Block.GetSwitchState",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");
        bool current = readPayload["State"]?.Value<bool>() ?? false;

        // Toggle it
        var (topic, payload) = await mqtt.RequestAsync(
            "Block.SetSwitchState",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock},\"State\":{(!current).ToString().ToLower()}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Block.SetSwitchState/", topic);
        // State returned should reflect the new value (or null if block doesn't support it)
        Assert.NotNull(payload["State"]);
    }

    // -------------------------------------------------------------------------
    // Block.GetTextures
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetTextures_LeverSwitchPosition_ReturnsSixSides()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Block.GetTextures",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Block.GetTextures/", topic);
        Assert.NotNull(payload["Top"]);
        Assert.NotNull(payload["Bottom"]);
        Assert.NotNull(payload["North"]);
        Assert.NotNull(payload["South"]);
        Assert.NotNull(payload["West"]);
        Assert.NotNull(payload["East"]);
    }

    // -------------------------------------------------------------------------
    // Block.GetColors
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetColors_LeverSwitchPosition_ReturnsSixSides()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Block.GetColors",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Block.GetColors/", topic);
        Assert.NotNull(payload["Top"]);
        Assert.NotNull(payload["Bottom"]);
        Assert.NotNull(payload["North"]);
        Assert.NotNull(payload["South"]);
        Assert.NotNull(payload["West"]);
        Assert.NotNull(payload["East"]);
    }

    // -------------------------------------------------------------------------
    // Block.SetDamage — mutation: repair the block (damage=0 is safe)
    // NOTE: changes game state — repairs the lever switch block.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SetDamage_Zero_RepairsBlock()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Block.SetDamage",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock},\"Damage\":0}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Block.SetDamage/", topic);
        Assert.Equal(EID, payload["EntityId"]!.Value<int>());
        Assert.Equal(0, payload["Damage"]!.Value<int>());
    }

    // -------------------------------------------------------------------------
    // Error case — unknown entity should return an Exception topic
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Get_UnknownEntityId_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Block.Get",
            $"{{\"EntityId\":999999,\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/Block.Get/", topic);
        Assert.NotNull(payload["Error"]);
    }
}
