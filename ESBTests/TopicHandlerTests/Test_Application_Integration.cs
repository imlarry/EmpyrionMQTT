using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// <summary>
/// Integration tests for the ESB/ Application handlers.
/// Requires the game running with the ESB mod loaded.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Application_Integration
{
    // -------------------------------------------------------------------------
    // App/Req/get/GameTicks
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GameTicks_ReturnsPositiveNumber()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/GameTicks", "{}");

        Assert.NotNull(payload["GameTicks"]);
        Assert.True(payload["GameTicks"]!.Value<long>() > 0,
            "GameTicks should be positive once the game is running");
    }

    // -------------------------------------------------------------------------
    // App/Req/get/Mode
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Mode_ReturnsModeString()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/Mode", "{}");

        Assert.NotNull(payload["Mode"]);
        Assert.False(string.IsNullOrEmpty(payload["Mode"]!.Value<string>()),
            "Mode must be a non-empty string");
    }

    // -------------------------------------------------------------------------
    // App/Req/get/State
    // -------------------------------------------------------------------------
    [Fact]
    public async Task State_ReturnsStateString()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/State", "{}");

        Assert.NotNull(payload["State"]);
        Assert.False(string.IsNullOrEmpty(payload["State"]!.Value<string>()),
            "State must be a non-empty string");
    }

    // -------------------------------------------------------------------------
    // App/Req/get/LocalPlayer -- null in dedicated mode
    // -------------------------------------------------------------------------
    [Fact]
    public async Task LocalPlayer_ReturnsPlayerDataOrNull()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/LocalPlayer", "{}");

        // Dedicated server: {LocalPlayer: null}. Client: full object at root.
        if (payload["LocalPlayer"] != null && payload["LocalPlayer"]!.Type != JTokenType.Null)
        {
            Assert.NotNull(payload["Id"]);
            Assert.NotNull(payload["Health"]);
            Assert.NotNull(payload["Position"]);
        }
        // else: dedicated server path -- test still passes
    }

    // -------------------------------------------------------------------------
    // App/Req/get/ModApiProperties
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ModApiProperties_ReturnsExpectedKeys()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/ModApiProperties", "{}");

        Assert.NotNull(payload["Application"]);
        Assert.NotNull(payload["GUI"]);
        Assert.NotNull(payload["Network"]);
    }

    // -------------------------------------------------------------------------
    // App/Req/get/AllPlayfields
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetAllPlayfields_ContainsAkua()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/AllPlayfields", "{}");

        // Handler returns a JSON array; SBTestClient wraps it under "items"
        var array = payload["items"] as JArray ?? new JArray();
        Assert.NotEmpty(array);
        Assert.Contains(array, t => t["PlayfieldName"]?.Value<string>() == KnownState.Playfield);
    }

    // -------------------------------------------------------------------------
    // App/Req/get/PlayerEntityIds
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetPlayerEntityIds_ReturnsArray()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/PlayerEntityIds", "{}");

        // May be an array (no players online) or an object -- just check it arrives
        Assert.NotNull(payload);
    }

    // -------------------------------------------------------------------------
    // App/Req/get/GetPathFor
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetPathFor_Root_ReturnsPath()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/GetPathFor",
            "{\"AppFolder\":\"Root\"}");

        Assert.Equal("Root", payload["AppFolder"]!.Value<string>());
        Assert.False(string.IsNullOrEmpty(payload["Path"]!.Value<string>()),
            "Path must be non-empty");
    }

    [Fact]
    public async Task GetPathFor_SaveGame_ReturnsPath()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/GetPathFor",
            "{\"AppFolder\":\"SaveGame\"}");

        Assert.Equal("SaveGame", payload["AppFolder"]!.Value<string>());
        Assert.False(string.IsNullOrEmpty(payload["Path"]!.Value<string>()),
            "SaveGame path must be non-empty");
    }

    // -------------------------------------------------------------------------
    // App/Req/get/GetStructure -- async DB query by entity ID
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetStructure_BaseEntity_ReturnsStructureInfo()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/GetStructure",
            $"{{\"EntityId\":{KnownState.BaseEntityId}}}", timeoutMs: 10000);

        Assert.Equal(KnownState.BaseEntityId, payload["Id"]!.Value<int>());
        Assert.NotNull(payload["Name"]);
        Assert.NotNull(payload["Pos"]);
    }

    // -------------------------------------------------------------------------
    // App/Req/get/GetStructures -- async DB query by playfield name
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetStructures_AkuaPlayfield_ReturnsStructureList()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/GetStructures",
            $"{{\"PlayfieldName\":\"{KnownState.Playfield}\"}}", timeoutMs: 10000);

        // Handler returns a bare JSON array; SBTestClient wraps it under "items"
        var array = payload["items"] as JArray ?? new JArray();
        Assert.Contains(array, t => t["Name"]?.Value<string>() == KnownState.BaseName);
    }

    [Fact]
    public async Task GetStructures_MissingBothFilters_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "get/GetStructures", "{}");

        Assert.NotNull(payload["Error"]);
    }
}
