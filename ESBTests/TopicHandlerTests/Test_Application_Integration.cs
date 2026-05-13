using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// <summary>
/// Integration tests for the App-scope handlers (AppHandler.cs).
/// Requires the game running with the ESB mod loaded.
/// Run with: dotnet test --filter "Category=Integration"
///
/// Pattern: each operation gets a section with success and error cases.
/// No-input operations have success only; input operations have both.
/// </summary>
[Trait("Category", "Integration")]
public class Test_Application_Integration
{
    // -------------------------------------------------------------------------
    // App/GameTicks -- no input, returns ulong
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GameTicks_ReturnsPositiveNumber()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GameTicks", "{}");

        Assert.True((ulong?)payload["GameTicks"] > 0);
    }

    // -------------------------------------------------------------------------
    // App/Mode -- no input, returns ApplicationMode enum as string
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Mode_ReturnsModeString()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "Mode", "{}");

        Assert.False(string.IsNullOrEmpty((string)payload["Mode"]));
    }

    // -------------------------------------------------------------------------
    // App/State -- no input, returns GameState enum as string
    // -------------------------------------------------------------------------

    [Fact]
    public async Task State_ReturnsStateString()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "State", "{}");

        Assert.False(string.IsNullOrEmpty((string)payload["State"]));
    }

    // -------------------------------------------------------------------------
    // App/ModApiProperties -- no input, returns "set"/"null" availability map
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ModApiProperties_ReturnsAvailabilityMap()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "ModApiProperties", "{}");

        Assert.NotNull(payload["Application"]);
        Assert.NotNull(payload["Network"]);
        Assert.NotNull(payload["GUI"]);
        Assert.NotNull(payload["PDA"]);
        Assert.NotNull(payload["Scripting"]);
        Assert.NotNull(payload["SoundPlayer"]);
        Assert.NotNull(payload["ClientPlayfield"]);
    }

    // -------------------------------------------------------------------------
    // App/GetAllPlayfields -- no input, returns PlayfieldInfoResponse array
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllPlayfields_ContainsKnownPlayfield()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetAllPlayfields", "{}");

        var array = payload["items"] as JArray ?? new JArray();
        Assert.NotEmpty(array);
        Assert.Contains(array, t => (string)t["PlayfieldName"] == KnownState.Playfield);
    }

    [Fact]
    public async Task GetAllPlayfields_ItemsHaveExpectedFields()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetAllPlayfields", "{}");

        var first = (payload["items"] as JArray)?[0];
        Assert.NotNull(first);
        Assert.NotNull(first["PlayfieldName"]);
        Assert.NotNull(first["PlayfieldType"]);
        Assert.NotNull(first["IsInstance"]);
    }

    // -------------------------------------------------------------------------
    // App/PfServerInfos -- no input; may return error if no playfield servers active
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PfServerInfos_Responds()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "PfServerInfos", "{}");

        // Valid response is either a data object or an error -- both are acceptable.
        Assert.NotNull(payload);
    }

    // -------------------------------------------------------------------------
    // App/PlayerEntityIds -- no input, returns int array (may be empty)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PlayerEntityIds_Responds()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "PlayerEntityIds", "{}");

        Assert.NotNull(payload);
    }

    // -------------------------------------------------------------------------
    // App/BlockAndItemMapping -- no input, returns Dictionary<string, int>
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BlockAndItemMapping_ReturnsNonEmptyMapping()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "BlockAndItemMapping", "{}");

        Assert.Null(payload["Error"]);
        Assert.True(payload.Count > 0, "Mapping should contain at least one entry");
    }

    // -------------------------------------------------------------------------
    // App/GetPathFor -- AppFolder enum required
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPathFor_Root_ReturnsAbsolutePath()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetPathFor", "{\"AppFolder\":\"Root\"}");

        Assert.Equal("Root", (string)payload["AppFolder"]);
        Assert.False(string.IsNullOrEmpty((string)payload["Path"]));
    }

    [Fact]
    public async Task GetPathFor_SaveGame_ReturnsAbsolutePath()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetPathFor", "{\"AppFolder\":\"SaveGame\"}");

        Assert.Equal("SaveGame", (string)payload["AppFolder"]);
        Assert.False(string.IsNullOrEmpty((string)payload["Path"]));
    }

    [Fact]
    public async Task GetPathFor_InvalidEnum_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetPathFor", "{\"AppFolder\":\"NotAValidFolder\"}");

        Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task GetPathFor_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetPathFor", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // App/GetPlayerDataFor -- PlayerEntityId required
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPlayerDataFor_KnownPlayer_ReturnsData()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetPlayerDataFor",
            $"{{\"PlayerEntityId\":{KnownState.PlayerEntityId}}}");

        Assert.Null(payload["Error"]);
        Assert.Equal(KnownState.PlayerEntityId, (int)payload["EntityId"]);
        Assert.NotNull(payload["PlayerName"]);
        Assert.NotNull(payload["SteamId"]);
        Assert.NotNull(payload["PlayfieldName"]);
        Assert.NotNull(payload["IsOnline"]);
    }

    [Fact]
    public async Task GetPlayerDataFor_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetPlayerDataFor", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // App/GetStructure -- EntityId required, result via async callback
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStructure_KnownBase_ReturnsStructureInfo()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetStructure",
            $"{{\"EntityId\":{KnownState.BaseEntityId}}}", timeoutMs: 10000);

        Assert.Null(payload["Error"]);
        Assert.Equal(KnownState.BaseEntityId, (int)payload["Id"]);
        Assert.Equal(KnownState.BaseName, (string)payload["Name"]);
        Assert.NotNull(payload["Pos"]);
        Assert.NotNull(payload["Rot"]);
    }

    [Fact]
    public async Task GetStructure_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetStructure", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // App/GetStructures -- PlayfieldName or FactionData required, async callback
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStructures_ByPlayfield_ReturnsStructureList()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetStructures",
            $"{{\"PlayfieldName\":\"{KnownState.Playfield}\"}}", timeoutMs: 10000);

        var array = payload["items"] as JArray ?? new JArray();
        Assert.NotEmpty(array);
        Assert.Contains(array, t => (string)t["Name"] == KnownState.BaseName);
    }

    [Fact]
    public async Task GetStructures_NoFilters_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetStructures", "{}");

        Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task GetStructures_InvalidEntityType_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetStructures",
            $"{{\"PlayfieldName\":\"{KnownState.Playfield}\",\"EntityType\":\"NotAnEntityType\"}}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // App/SendChatMessage -- broadcasts chat; Text defaults to empty if omitted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendChatMessage_ValidRequest_ReturnsOk()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "SendChatMessage",
            "{\"Text\":\"[ESB integration test]\"}");

        Assert.Null(payload["Error"]);
        Assert.NotNull(payload["ok"]);
    }

    [Fact]
    public async Task SendChatMessage_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "SendChatMessage", "");

        Assert.NotNull(payload["Error"]);
    }
}
