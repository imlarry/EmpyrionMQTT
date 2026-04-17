using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IStructure;

/// <summary>
/// Integration tests for the V1 Structure topic handler.
/// Requires a MULTIPLAYER game running with a DedicatedServer and the ESB mod loaded.
///
/// V1 (ModBase/DediAPI) is only available on DedicatedServer in multiplayer.
/// These tests will time out if run against a SinglePlayer session.
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Structure_Integration
{
    // -------------------------------------------------------------------------
    // V1.Structure.ListGlobal -- all structures, no filter
    // Response must include at least one entry and globalStructures must be a dict
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ListGlobal_NoFilter_ReturnsGlobalStructureDict()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Structure.ListGlobal",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Structure.ListGlobal/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        var dict = data["GlobalStructures"] as JObject;
        Assert.NotNull(dict);
        // At least the test playfield must appear
        Assert.True(dict.Count > 0, "GlobalStructures is empty -- no structures on any playfield");
    }

    // -------------------------------------------------------------------------
    // V1.Structure.ListGlobal -- the known base entity must appear in the list
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ListGlobal_ContainsKnownBase()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Structure.ListGlobal",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Structure.ListGlobal/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var dict = payload["Data"]!["GlobalStructures"] as JObject;
        Assert.NotNull(dict);

        // Flatten all structures across all playfields and look for BaseEntityId
        var allIds = dict.Properties()
            .SelectMany(p => (p.Value as JArray) ?? new JArray())
            .Select(e => e["Id"]?.Value<int>() ?? 0);

        Assert.Contains(KnownState.BaseEntityId, allIds);
    }

    // -------------------------------------------------------------------------
    // V1.Structure.BlockStats -- block-type counts for the known base
    // -------------------------------------------------------------------------
    [Fact]
    public async Task BlockStats_KnownBase_ReturnsBlockCounts()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Structure.BlockStats",
            $"{{\"EntityId\":{KnownState.BaseEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Structure.BlockStats/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.Equal(KnownState.BaseEntityId, data["Id"]!.Value<int>());
        // BlockStatistics must be a non-empty dictionary (any structure has at least one block type)
        var stats = data["BlockStatistics"] as JObject;
        Assert.NotNull(stats);
        Assert.True(stats.Count > 0, "BlockStatistics is empty -- structure has no blocks?");
    }

    // -------------------------------------------------------------------------
    // V1.Structure.Touch -- update the lastVisitedTicks timestamp for the known base
    // No visible in-game effect; safe to run without save restore.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Touch_KnownBase_ReturnsOk()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Structure.Touch",
            $"{{\"EntityId\":{KnownState.BaseEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Structure.Touch/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        Assert.True(payload["Ok"]!.Value<bool>());
    }
}
