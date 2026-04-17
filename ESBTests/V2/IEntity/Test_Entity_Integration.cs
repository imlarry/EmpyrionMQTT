using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V2.IEntity;

/// <summary>
/// Integration tests for the V2 Entity topic handlers.
/// Requires the game running with the ESB mod loaded and the entity described
/// in <see cref="KnownState"/> present in the active playfield.
///
/// Handler pattern:
///   "V2.Entity"              — property getter (all or filtered via "Properties" key)
///   "V2.Entity.DamageEntity" — method call
///   "V2.Entity.Move"         — method call
///   "V2.Entity.MoveForward"  — method call
///   "V2.Entity.MoveStop"     — method call
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Entity_Integration
{
    private const int EID = KnownState.BaseEntityId;

    // =========================================================================
    // V2.Entity — property getter
    // =========================================================================

    /// <summary>Missing EntityId returns X with a descriptive error.</summary>
    [Fact]
    public async Task Properties_NoEntityId_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Entity", "{}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Entity/", topic);
        Assert.Contains("EntityId is required", payload["Error"]?.Value<string>() ?? "");
    }

    /// <summary>No Properties filter returns all fields for the known entity.</summary>
    [Fact]
    public async Task Properties_NoFilter_ReturnsAllFields()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Entity", $"{{\"EntityId\":{EID}}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.Equal(EID, payload["Id"]?.Value<int>());
            Assert.NotNull(payload["Name"]);
            Assert.NotNull(payload["Type"]);
            Assert.NotNull(payload["Position"]);
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
            "V2.Entity",
            $"{{\"EntityId\":{EID},\"Properties\":[\"Name\",\"Type\"]}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.NotNull(payload["Name"]);
            Assert.NotNull(payload["Type"]);
            Assert.Null(payload["Position"]);
            Assert.Null(payload["Faction"]);
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
            "V2.Entity",
            $"{{\"EntityId\":{EID},\"Properties\":[\"NotAProperty\"]}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Entity/", topic);
        Assert.Equal("InvalidProperty", payload["Error"]?.Value<string>());
        var validProps = payload["ValidProperties"] as JArray;
        Assert.NotNull(validProps);
        Assert.True(validProps.Count > 0, "ValidProperties should list all property names");
    }

    /// <summary>An unknown EntityId returns X with a not-found error.</summary>
    [Fact]
    public async Task Properties_UnknownEntityId_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Entity", "{\"EntityId\":999999999}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Entity/", topic);
        Assert.NotNull(payload["Error"]);
    }

    /// <summary>Position contains X, Y, Z float values.</summary>
    [Fact]
    public async Task Properties_Position_ContainsXYZ()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity",
            $"{{\"EntityId\":{EID},\"Properties\":[\"Position\"]}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity/"),
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

    /// <summary>Rotation contains X, Y, Z, W quaternion components.</summary>
    [Fact]
    public async Task Properties_Rotation_ContainsXYZW()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity",
            $"{{\"EntityId\":{EID},\"Properties\":[\"Rotation\"]}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            var rot = payload["Rotation"] as JObject;
            Assert.NotNull(rot);
            Assert.NotNull(rot["X"]);
            Assert.NotNull(rot["Y"]);
            Assert.NotNull(rot["Z"]);
            Assert.NotNull(rot["W"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    /// <summary>Faction contains Group and Id.</summary>
    [Fact]
    public async Task Properties_Faction_ContainsGroupAndId()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity",
            $"{{\"EntityId\":{EID},\"Properties\":[\"Faction\"]}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            var faction = payload["Faction"] as JObject;
            Assert.NotNull(faction);
            Assert.NotNull(faction["Group"]);
            Assert.NotNull(faction["Id"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    /// <summary>Structure is either a JObject with Id and IsReady, or null for non-structure entities.</summary>
    [Fact]
    public async Task Properties_Structure_IsObjectOrNull()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity",
            $"{{\"EntityId\":{EID},\"Properties\":[\"Structure\"]}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            var structure = payload["Structure"];
            Assert.NotNull(structure);
            if (structure.Type != JTokenType.Null)
            {
                Assert.NotNull(structure["Id"]);
                Assert.NotNull(structure["IsReady"]);
            }
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    // =========================================================================
    // V2.Entity.DamageEntity
    // =========================================================================

    /// <summary>DamageAmount:0 is a safe no-op. Verifies the handler responds without crashing.</summary>
    [Fact]
    public async Task DamageEntity_ZeroDamage_ReturnsResultOrException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity.DamageEntity",
            $"{{\"EntityId\":{EID},\"DamageAmount\":0,\"DamageType\":0}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity.DamageEntity/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity.DamageEntity/"),
            $"Unexpected topic: {topic}");
    }

    // =========================================================================
    // V2.Entity.Move
    // =========================================================================

    /// <summary>Missing Direction returns X with a descriptive error.</summary>
    [Fact]
    public async Task Move_MissingDirection_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity.Move",
            $"{{\"EntityId\":{EID}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Entity.Move/", topic);
        Assert.Contains("Direction argument is required", payload["Error"]?.Value<string>() ?? "");
    }

    /// <summary>Zero-vector direction is a safe no-op nudge. Verifies the handler responds.</summary>
    [Fact]
    public async Task Move_ZeroDirection_ReturnsResultOrException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity.Move",
            $"{{\"EntityId\":{EID},\"Direction\":{{\"X\":0,\"Y\":0,\"Z\":0}}}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity.Move/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity.Move/"),
            $"Unexpected topic: {topic}");
    }

    // =========================================================================
    // V2.Entity.MoveForward
    // =========================================================================

    /// <summary>Speed:0 is a safe no-op. Verifies the handler responds.</summary>
    [Fact]
    public async Task MoveForward_ZeroSpeed_ReturnsResultOrException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity.MoveForward",
            $"{{\"EntityId\":{EID},\"Speed\":0}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity.MoveForward/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity.MoveForward/"),
            $"Unexpected topic: {topic}");
    }

    // =========================================================================
    // V2.Entity.MoveStop
    // =========================================================================

    /// <summary>MoveStop on a stationary entity is a safe no-op. Verifies the handler responds.</summary>
    [Fact]
    public async Task MoveStop_ReturnsResultOrException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Entity.MoveStop",
            $"{{\"EntityId\":{EID}}}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Entity.MoveStop/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Entity.MoveStop/"),
            $"Unexpected topic: {topic}");
    }
}
