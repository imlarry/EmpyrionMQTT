using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IPlayer;

/// <summary>
/// Integration tests for the V1 Player topic handler.
/// Requires a MULTIPLAYER game running with a DedicatedServer and the ESB mod loaded.
///
/// V1 (ModBase/DediAPI) is only available on DedicatedServer in multiplayer.
/// V1's Initialize() is never called in SinglePlayer — these tests will not
/// exercise any code path in SP and will time out waiting for a response.
///
/// KnownState.PlayerEntityId must be set to the active player's entity ID.
/// Obtain it in-game via the "di" console command or V2.Application.GetPlayerEntityIds.
/// KnownState.V1AppId must be "DedicatedServer" — requests are routed there, not to Client.
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Player_Integration
{
    // -------------------------------------------------------------------------
    // V1.Player.GetInventory — valid player entity, expects inventory data
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetInventory_ValidPlayer_ReturnsInventoryData()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Player.GetInventory",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInventory/"),
            $"Expected R/ but got: {topic} — {payload["Error"]?.Value<string>()}");

        // A valid player response returns Data as a JObject (inventory), not the "<no data>" string.
        Assert.Equal(JTokenType.Object, payload["Data"]!.Type);
    }

    // -------------------------------------------------------------------------
    // V1.Player.GetInventory — structure entity ID, expects PlayerIdNotFound error
    // The V1 API throws rather than returning {"Data":"<no data>"} for non-player entities.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetInventory_StructureEntity_ReturnsPlayerIdNotFound()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Player.GetInventory",
            $"{{\"EntityId\":{KnownState.BaseEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/X/V1.Player.GetInventory/"),
            $"Expected X/ but got: {topic}");

        Assert.Equal("PlayerIdNotFound", payload["Error"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // V1.Player.GetInfo — valid player entity, expects full PlayerInfo
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetInfo_ValidPlayer_ReturnsPlayerInfo()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Player.GetInfo",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInfo/"),
            $"Expected R/ but got: {topic} — {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        // PlayerInfo must carry the entity id and player name
        Assert.Equal(KnownState.PlayerEntityId, data["entityId"]!.Value<int>());
        Assert.False(string.IsNullOrEmpty(data["playerName"]?.Value<string>()));
    }

    // -------------------------------------------------------------------------
    // V1.Player.GetInfo — structure entity ID, expects error
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetInfo_StructureEntity_ReturnsError()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Handler waits up to 3 s for the game to respond before sending X.
        // Give the test 8 s so it always outlasts the handler's internal timeout.
        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Player.GetInfo",
            $"{{\"EntityId\":{KnownState.BaseEntityId}}}",
            timeoutMs: 8000,
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/X/V1.Player.GetInfo/"),
            $"Expected X/ but got: {topic}");
    }

    // -------------------------------------------------------------------------
    // V1.Player.List — connected player list must contain the known player
    // -------------------------------------------------------------------------
    [Fact]
    public async Task List_ReturnsConnectedPlayerEntityIds()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Player.List",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.List/"),
            $"Expected R/ but got: {topic} — {payload["Error"]?.Value<string>()}");

        var ids = payload["Data"] as JArray;
        Assert.NotNull(ids);
        Assert.Contains(ids, t => t.Value<int>() == KnownState.PlayerEntityId);
    }

    // -------------------------------------------------------------------------
    // V1.Player.GetCredits — valid player, expects IdCredits with non-negative credits
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetCredits_ValidPlayer_ReturnsCredits()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Player.GetCredits",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetCredits/"),
            $"Expected R/ but got: {topic} — {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.Equal(KnownState.PlayerEntityId, data["id"]!.Value<int>());
        Assert.True(data["credits"]!.Value<double>() >= 0d);
    }

    // -------------------------------------------------------------------------
    // V1.Player.AddCredits — adding 0 credits is a safe no-op;
    // confirms the handler round-trips and returns the current balance.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task AddCredits_Zero_ReturnsCurrentBalance()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Player.AddCredits",
            $"{{\"EntityId\":{KnownState.PlayerEntityId},\"Credits\":0.0}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.AddCredits/"),
            $"Expected R/ but got: {topic} — {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.Equal(KnownState.PlayerEntityId, data["id"]!.Value<int>());
    }

    // -------------------------------------------------------------------------
    // V1.Player.SetCredits — round-trip: read current credits then set same amount.
    // Net effect on the game is zero.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SetCredits_SameAmount_ReturnsConfirmedBalance()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Read current credits
        var (_, getPayload) = await mqtt.RequestAsync(
            "V1.Player.GetCredits",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        double currentCredits = getPayload["Data"]!["credits"]!.Value<double>();

        // Set to same amount
        var (topic, setPayload) = await mqtt.RequestAsync(
            "V1.Player.SetCredits",
            $"{{\"EntityId\":{KnownState.PlayerEntityId},\"Credits\":{currentCredits}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.SetCredits/"),
            $"Expected R/ but got: {topic} — {setPayload["Error"]?.Value<string>()}");

        var data = setPayload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.Equal(currentCredits, data["credits"]!.Value<double>(), precision: 2);
    }
}
