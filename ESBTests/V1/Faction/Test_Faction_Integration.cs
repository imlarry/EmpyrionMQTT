using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IFaction;

/// <summary>
/// Integration tests for the V1 Faction topic handler.
/// Requires a MULTIPLAYER game running with a DedicatedServer and the ESB mod loaded.
///
/// V1 (ModBase/DediAPI) is only available on DedicatedServer in multiplayer.
/// These tests will time out if run against a SinglePlayer session.
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Faction_Integration
{
    // -------------------------------------------------------------------------
    // V1.Faction.List -- must return at least one faction entry with required fields
    // -------------------------------------------------------------------------
    [Fact]
    public async Task List_ReturnsFactionList()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Faction.List",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Faction.List/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JArray;
        Assert.NotNull(data);
        Assert.True(data.Count > 0, "Faction list is empty -- server must have at least one faction");

        // Every entry must carry the required fields
        foreach (JObject entry in data)
        {
            Assert.True(entry.ContainsKey("FactionId"), "entry missing FactionId");
            Assert.True(entry.ContainsKey("Name"),      "entry missing Name");
            Assert.True(entry.ContainsKey("Abbrev"),    "entry missing Abbrev");
        }
    }

    // -------------------------------------------------------------------------
    // V1.Faction.AlliancesAll -- returns Data with alliances set (may be empty on
    // a fresh server with no player-created alliance groups)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task AlliancesAll_ReturnsAlliancesData()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Faction.AlliancesAll",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Faction.AlliancesAll/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.True(data.ContainsKey("Alliances"), "Data missing Alliances field");
    }

    // -------------------------------------------------------------------------
    // V1.Faction.AlliancesByFaction -- requires two non-zero faction IDs.
    // The game returns MissingParameter if either ID is zero, so we derive
    // two valid faction IDs from AlliancesAll (the set of IDs involved in
    // at least one alliance).  If the server has no alliances yet (fresh save)
    // the test is inconclusive rather than failing.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task AlliancesByFaction_AlliedPair_ReturnsData()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Get all faction IDs that participate in at least one alliance
        var (allTopic, allPayload) = await mqtt.RequestAsync(
            "V1.Faction.AlliancesAll",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            allTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Faction.AlliancesAll/"),
            $"AlliancesAll failed: {allTopic} -- {allPayload["Error"]?.Value<string>()}");

        var alliances = allPayload["Data"]?["Alliances"] as JArray;
        if (alliances == null || alliances.Count < 2)
        {
            // No allied pairs on this server -- cannot exercise AlliancesByFaction
            return;
        }

        int f1 = (int)alliances[0];
        int f2 = (int)alliances[1];

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Faction.AlliancesByFaction",
            $"{{\"Faction1Id\":{f1},\"Faction2Id\":{f2}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Faction.AlliancesByFaction/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.True(data.ContainsKey("Faction1Id"), "Data missing Faction1Id");
        Assert.True(data.ContainsKey("Faction2Id"), "Data missing Faction2Id");
        Assert.True(data.ContainsKey("IsAllied"),   "Data missing IsAllied");
    }
}
