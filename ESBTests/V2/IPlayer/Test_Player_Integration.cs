using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V2.IPlayer;

/// <summary>
/// Integration tests for the V2 Player topic handlers.
/// Requires the game running in single-player or hosted mode with the ESB mod loaded.
/// LocalPlayer must be active — handlers return X when LocalPlayer is null (dedicated server).
///
/// Handler pattern:
///   "V2.Player"              — property getter (all or filtered via "Properties" key)
///   "V2.Player.Teleport"     — method call
///   "V2.Player.DamageEntity" — method call
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Player_Integration
{
    // =========================================================================
    // V2.Player — property getter
    // =========================================================================

    /// <summary>No payload → all properties returned. Spot-check a cross-section.</summary>
    [Fact]
    public async Task Properties_NoPayload_ReturnsAllFields()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Player", "{}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Player/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Player/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.NotNull(payload["Health"]);
            Assert.NotNull(payload["SteamId"]);
            Assert.NotNull(payload["Position"]);
            Assert.NotNull(payload["Bag"]);
            Assert.NotNull(payload["Toolbar"]);
            Assert.NotNull(payload["FactionData"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    /// <summary>Requesting specific properties returns only those keys.</summary>
    [Fact]
    public async Task Properties_SelectFields_ReturnsOnlyRequested()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player",
            "{\"Properties\":[\"SteamId\",\"Health\"]}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Player/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Player/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.NotNull(payload["SteamId"]);
            Assert.NotNull(payload["Health"]);
            Assert.Null(payload["Bag"]);
            Assert.Null(payload["Oxygen"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    /// <summary>Bag is a JArray; each non-empty element has the ItemStack fields.</summary>
    [Fact]
    public async Task Properties_Bag_ContainsItemStacks()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player",
            "{\"Properties\":[\"Bag\"]}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Player/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Player/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            var bag = payload["Bag"] as JArray;
            Assert.NotNull(bag);
            foreach (JObject item in bag)
            {
                Assert.NotNull(item["Id"]);
                Assert.NotNull(item["Count"]);
                Assert.NotNull(item["SlotIdx"]);
            }
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    /// <summary>Toolbar is a JArray; each non-empty element has the ItemStack fields.</summary>
    [Fact]
    public async Task Properties_Toolbar_ContainsItemStacks()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player",
            "{\"Properties\":[\"Toolbar\"]}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Player/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Player/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            var toolbar = payload["Toolbar"] as JArray;
            Assert.NotNull(toolbar);
            foreach (JObject item in toolbar)
            {
                Assert.NotNull(item["Id"]);
                Assert.NotNull(item["Count"]);
                Assert.NotNull(item["SlotIdx"]);
            }
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    /// <summary>An invalid property name returns X with a ValidProperties list.</summary>
    [Fact]
    public async Task Properties_InvalidProperty_ReturnsXWithValidList()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player",
            "{\"Properties\":[\"NotAProperty\"]}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Player/", topic);
        Assert.Equal("InvalidProperty", payload["Error"]?.Value<string>());
        var validProps = payload["ValidProperties"] as JArray;
        Assert.NotNull(validProps);
        Assert.True(validProps.Count > 0, "ValidProperties should list all property names");
    }

    /// <summary>EntityId in payload is reserved for the future V1 path — returns X for now.</summary>
    [Fact]
    public async Task Properties_EntityId_ReturnsXRequiresDedicatedServer()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player",
            "{\"EntityId\":1}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Player/", topic);
        Assert.Contains("DedicatedServer", payload["Error"]?.Value<string>() ?? "");
    }

    /// <summary>Position contains X, Y, Z float values.</summary>
    [Fact]
    public async Task Properties_Position_ContainsXYZ()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player",
            "{\"Properties\":[\"Position\"]}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Player/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Player/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            var pos = payload["Position"] as JObject;
            Assert.NotNull(pos);
            Assert.NotNull(pos["X"]);
            Assert.NotNull(pos["Y"]);
            Assert.NotNull(pos["Z"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    /// <summary>FactionData contains Group and Id.</summary>
    [Fact]
    public async Task Properties_FactionData_ContainsGroupAndId()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player",
            "{\"Properties\":[\"FactionData\"]}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Player/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Player/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            var fd = payload["FactionData"] as JObject;
            Assert.NotNull(fd);
            Assert.NotNull(fd["Group"]);
            Assert.NotNull(fd["Id"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    // =========================================================================
    // V2.Player.Teleport
    // =========================================================================

    /// <summary>
    /// Pos-only teleport returns a bool result. Teleports to the known spawn point.
    /// Note: same-playfield Teleport(pos) ignores Y — the game snaps to terrain height.
    /// The Y value in PlayerSpawnPos is irrelevant; X/Z determine the landing spot.
    /// </summary>
    [Fact]
    public async Task Teleport_PosOnly_ReturnsBool()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player.Teleport",
            $"{{\"Pos\":{KnownState.PlayerSpawnPos}}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Player.Teleport/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Player.Teleport/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
            Assert.NotNull(payload["Teleport"]);
        else
            Assert.NotNull(payload["Error"]);
    }

    /// <summary>Missing Pos returns X with a descriptive error.</summary>
    [Fact]
    public async Task Teleport_MissingPos_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Player.Teleport", "{}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Player.Teleport/", topic);
        Assert.Contains("Pos argument is required", payload["Error"]?.Value<string>() ?? "");
    }

    /// <summary>Providing Playfield without Rot returns X with a descriptive error.</summary>
    [Fact]
    public async Task Teleport_WithPlayfield_MissingRot_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player.Teleport",
            $"{{\"Pos\":{KnownState.PlayerSpawnPos},\"Playfield\":\"{KnownState.Playfield}\"}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Player.Teleport/", topic);
        Assert.Contains("Rot argument is required", payload["Error"]?.Value<string>() ?? "");
    }

    // =========================================================================
    // V2.Player.DamageEntity
    // =========================================================================

    /// <summary>
    /// DamageAmount:0 is a safe no-op. Verifies the handler responds without crashing.
    /// Accept R (success) or X (e.g. LocalPlayer null on dedicated server).
    /// </summary>
    [Fact]
    public async Task DamageEntity_ZeroDamage_ReturnsResultOrException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Player.DamageEntity",
            "{\"DamageAmount\":0,\"DamageType\":0}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Player.DamageEntity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Player.DamageEntity/"),
            $"Unexpected topic: {topic}");
    }
}
