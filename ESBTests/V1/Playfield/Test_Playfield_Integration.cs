using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

#pragma warning disable CS8600   // null-state analysis: casts are guarded by Assert.NotNull or conditional checks above

namespace ESBTests.V1.IPlayfield;

/// <summary>
/// Integration tests for the V1 Playfield topic handler.
/// Requires a MULTIPLAYER game running with a DedicatedServer and the ESB mod loaded.
///
/// V1 (ModBase/DediAPI) is only available on DedicatedServer in multiplayer.
/// These tests will time out if run against a SinglePlayer session.
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Playfield_Integration
{
    // -------------------------------------------------------------------------
    // V1.Playfield.List -- must contain at least the known test playfield
    // -------------------------------------------------------------------------
    [Fact]
    public async Task List_ReturnsPlayfieldNames()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Playfield.List",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Playfield.List/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JArray;
        Assert.NotNull(data);
        Assert.True(data.Count > 0, "Playfield list is empty");
        Assert.Contains(data, t => t.Value<string>() == KnownState.Playfield);
    }

    // -------------------------------------------------------------------------
    // V1.Playfield.Stats -- stats for the known loaded playfield
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Stats_KnownPlayfield_ReturnsStats()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Playfield.Stats",
            $"{{\"Playfield\":\"{KnownState.Playfield}\"}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Playfield.Stats/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.Equal(KnownState.Playfield, data["playfield"]!.Value<string>());
        Assert.True(data["fps"]!.Value<float>()  >= 0f);
        Assert.True(data["mem"]!.Value<int>()    >= 0);
        Assert.True(data["uptime"]!.Value<int>() >= 0);
    }

    // -------------------------------------------------------------------------
    // V1.Playfield.EntityList -- entity list for the known playfield must be
    // non-empty and return valid entity objects.
    // Note: V1 EntityList returns NPC/POI entities only -- players do NOT
    // appear in this list. Each entity has id (int), type (int), pos (object).
    // -------------------------------------------------------------------------
    [Fact]
    public async Task EntityList_KnownPlayfield_ReturnsEntities()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Playfield.EntityList",
            $"{{\"Playfield\":\"{KnownState.Playfield}\"}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Playfield.EntityList/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.Equal(KnownState.Playfield, (string)data["playfield"]);

        var entities = data["entities"] as JArray;
        Assert.NotNull(entities);
        Assert.True(entities.Count > 0, "Entity list is empty");
        Assert.All(entities, e =>
        {
            Assert.NotNull(e["id"]);
            Assert.NotNull(e["type"]);
            Assert.NotNull(e["pos"]);
        });
    }

    // -------------------------------------------------------------------------
    // V1.Playfield.Load -- sending Load for an already-loaded playfield returns
    // a PlayfieldAlreadyLoaded error from the game; confirms the handler dispatches
    // and the error is surfaced cleanly rather than hanging.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Load_AlreadyLoadedPlayfield_ReturnsOkOrAlreadyLoaded()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Playfield.Load",
            $"{{\"Playfield\":\"{KnownState.Playfield}\"}}",
            appId: KnownState.V1AppId);

        bool isOk = topic.StartsWith($"{KnownState.V1AppId}/R/V1.Playfield.Load/");
        bool isAlreadyLoaded = topic.StartsWith($"{KnownState.V1AppId}/X/V1.Playfield.Load/")
            && (string)payload["Error"] == "PlayfieldAlreadyLoaded";

        Assert.True(isOk || isAlreadyLoaded,
            $"Unexpected response: {topic} -- {payload["Error"]?.Value<string>()}");
    }
}
