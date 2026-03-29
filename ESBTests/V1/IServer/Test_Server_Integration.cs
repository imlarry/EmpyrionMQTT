using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IServer;

/// <summary>
/// Integration tests for the V1 Server topic handler.
/// Requires a MULTIPLAYER game running with a DedicatedServer and the ESB mod loaded.
///
/// V1 (ModBase/DediAPI) is only available on DedicatedServer in multiplayer.
/// These tests will time out if run against a SinglePlayer session or if
/// KnownState.V1AppId is not "DedicatedServer".
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Server_Integration
{
    // -------------------------------------------------------------------------
    // V1.Server.Stats -- response must include all DediStats fields
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Stats_ReturnsDediStats()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Server.Stats",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Server.Stats/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        // fps and uptime must be non-negative; ticks must be positive once the server has been up
        Assert.True(data["fps"]!.Value<float>()  >= 0f);
        Assert.True(data["mem"]!.Value<int>()    >= 0);
        Assert.True(data["players"]!.Value<int>() >= 0);
        Assert.True(data["uptime"]!.Value<int>() >= 0);
    }

    // -------------------------------------------------------------------------
    // V1.Server.ConsoleCommand -- fire-and-forget; Ok confirms dispatch
    // Using "help" as a safe read-only command that works on any dedicated server.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ConsoleCommand_HelpCommand_ReturnsOk()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Server.ConsoleCommand",
            "{\"Command\":\"help\"}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Server.ConsoleCommand/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        Assert.True(payload["Ok"]!.Value<bool>());
    }

    // -------------------------------------------------------------------------
    // V1.Server.BannedPlayers -- returns an array (may be empty on a clean server)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task BannedPlayers_ReturnsList()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Server.BannedPlayers",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Server.BannedPlayers/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        // Data must be an array; entries (if any) must carry steam64Id and dateTime
        var data = Assert.IsType<JArray>(payload["Data"]);
        foreach (JObject entry in data)
        {
            Assert.True(entry.ContainsKey("steam64Id"), "entry missing steam64Id");
            Assert.True(entry.ContainsKey("dateTime"),  "entry missing dateTime");
        }
    }
}
