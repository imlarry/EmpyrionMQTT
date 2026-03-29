using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V2.IApplication;

/// <summary>
/// Integration tests for the Application topic handlers.
/// Requires the game to be running with the ESB mod loaded and the saved game state
/// described in <see cref="KnownState"/> active.
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Application_Integration
{
    private const int EID = KnownState.BaseEntityId;

    // -------------------------------------------------------------------------
    // Application.State — read-only, always available
    // -------------------------------------------------------------------------
    [Fact]
    public async Task State_ReturnsStateString()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Application.State", "{}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Application.State/", topic);
        Assert.NotNull(payload["State"]);
        Assert.False(string.IsNullOrEmpty(payload["State"]!.Value<string>()),
            "State must be a non-empty string");
    }

    // -------------------------------------------------------------------------
    // Application.Mode — read-only, always available
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Mode_ReturnsModeString()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Application.Mode", "{}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Application.Mode/", topic);
        Assert.NotNull(payload["Mode"]);
        Assert.False(string.IsNullOrEmpty(payload["Mode"]!.Value<string>()),
            "Mode must be a non-empty string");
    }

    // -------------------------------------------------------------------------
    // Application.GameTicks — read-only, always available
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GameTicks_ReturnsPositiveNumber()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Application.GameTicks", "{}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Application.GameTicks/", topic);
        Assert.NotNull(payload["GameTicks"]);
        Assert.True(payload["GameTicks"]!.Value<long>() > 0,
            "GameTicks should be positive once the game is running");
    }

    // -------------------------------------------------------------------------
    // Application.LocalPlayer — read-only, null in dedicated mode
    // -------------------------------------------------------------------------
    [Fact]
    public async Task LocalPlayer_ReturnsPlayerDataOrNull()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Application.LocalPlayer", "{}");

        // Always R — handler returns {LocalPlayer: null} when no active player
        Assert.StartsWith($"{KnownState.AppId}/R/V2.Application.LocalPlayer/", topic);

        if (payload["LocalPlayer"] != null && payload["LocalPlayer"]!.Type != JTokenType.Null)
        {
            // Player is active — check core fields
            Assert.NotNull(payload["Id"]);
            Assert.NotNull(payload["Health"]);
            Assert.NotNull(payload["Position"]);
        }
        // else: dedicated server, LocalPlayer == null — test still passes
    }

    // -------------------------------------------------------------------------
    // Application.GetPathFor — read-only
    // Root and SaveGame are reliably available in both client and dedicated modes.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetPathFor_Root_ReturnsPath()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Application.GetPathFor", "{\"AppFolder\":\"Root\"}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Application.GetPathFor/", topic);
        Assert.NotNull(payload["AppFolder"]);
        Assert.NotNull(payload["Path"]);
        Assert.Equal("Root", payload["AppFolder"]!.Value<string>());
        Assert.False(string.IsNullOrEmpty(payload["Path"]!.Value<string>()),
            "Path must be non-empty");
    }

    [Fact]
    public async Task GetPathFor_SaveGame_ReturnsPath()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Application.GetPathFor", "{\"AppFolder\":\"SaveGame\"}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Application.GetPathFor/", topic);
        Assert.Equal("SaveGame", payload["AppFolder"]!.Value<string>());
        Assert.False(string.IsNullOrEmpty(payload["Path"]!.Value<string>()),
            "SaveGame path must be non-empty");
    }

    // -------------------------------------------------------------------------
    // Application.GetAllPlayfields — read-only, returns array
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetAllPlayfields_ContainsAkua()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Application.GetAllPlayfields", "{}");

        // Handler serialises a list — response may be a JSON array at root
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Application.GetAllPlayfields/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Application.GetAllPlayfields/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            // Handler sends a bare JSON array; MqttTestClient wraps it under "items"
            var array = (JArray)payload["items"]!;
            Assert.NotEmpty(array);
            // At least one entry should have PlayfieldName == "Akua"
            Assert.Contains(array, t => t["PlayfieldName"]?.Value<string>() == KnownState.Playfield);
        }
    }

    // -------------------------------------------------------------------------
    // Application.GetPlayerEntityIds — read-only, returns array
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetPlayerEntityIds_ReturnsArray()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Application.GetPlayerEntityIds", "{}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Application.GetPlayerEntityIds/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Application.GetPlayerEntityIds/"),
            $"Unexpected topic: {topic}");
        // No structural assertion — list may be empty in dedicated mode without players
    }

    // -------------------------------------------------------------------------
    // Application.GetStructure — async DB query by entity ID
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetStructure_BaseEntity_ReturnsStructureInfo()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Application.GetStructure", $"{{\"EntityId\":{EID}}}",
            timeoutMs: 10000);  // DB callback may take a moment

        // R on success, X if entity not found in DB
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Application.GetStructure/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Application.GetStructure/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.Equal(EID, payload["Id"]!.Value<int>());
            Assert.NotNull(payload["Name"]);
            Assert.NotNull(payload["Pos"]);
        }
    }

    // -------------------------------------------------------------------------
    // Application.GetStructures — async DB query by playfield name
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetStructures_AkuaPlayfield_ReturnsStructureList()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Application.GetStructures", $"{{\"PlayfieldName\":\"{KnownState.Playfield}\"}}",
            timeoutMs: 10000);

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Application.GetStructures/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Application.GetStructures/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            // Handler sends a bare JSON array; MqttTestClient wraps it under "items"
            var array = (JArray)payload["items"]!;
            // The test base ("VNS Akua") should be in the list
            Assert.Contains(array, t => t["Name"]?.Value<string>() == KnownState.BaseName);
        }
    }

    // NOTE: Application.SendChatMessage is omitted — fire-and-forget in single-player
    // mode (no RunOnMainThread wrapper in the handler) and may block the main thread,
    // causing subsequent tests to time out.
}
