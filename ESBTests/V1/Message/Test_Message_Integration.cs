using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IMessage;

/// <summary>
/// Integration tests for the V1 Message topic handler.
/// Requires a MULTIPLAYER game running with a DedicatedServer and the ESB mod loaded.
///
/// ToPlayer/ToAll/ToFaction are fire-and-forget -- Ok confirms dispatch only.
/// Messages will be visible in-game during the test run.
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Message_Integration
{
    // -------------------------------------------------------------------------
    // V1.Message.ToPlayer -- message appears for the test player
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ToPlayer_ValidPlayer_ReturnsOk()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Message.ToPlayer",
            $"{{\"EntityId\":{KnownState.PlayerEntityId},\"Message\":\"ESB test\",\"Priority\":2,\"Duration\":5}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Message.ToPlayer/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        Assert.True(payload["Ok"]!.Value<bool>());
    }

    // -------------------------------------------------------------------------
    // V1.Message.ToAll -- broadcast; only one player in test session but Ok confirms dispatch
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ToAll_ReturnsOk()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Message.ToAll",
            "{\"Message\":\"ESB test\",\"Priority\":2,\"Duration\":5}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Message.ToAll/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        Assert.True(payload["Ok"]!.Value<bool>());
    }

    // -------------------------------------------------------------------------
    // V1.Message.ToFaction -- uses the test player's faction ID from GetInfo
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ToFaction_ValidFaction_ReturnsOk()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Read the test player's faction ID
        var (infoTopic, infoPayload) = await mqtt.RequestAsync(
            "V1.Player.GetInfo",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            infoTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInfo/"),
            $"GetInfo failed: {infoTopic} -- {infoPayload["Error"]?.Value<string>()}");

        int factionId = infoPayload["Data"]!["factionId"]!.Value<int>();
        Assert.NotEqual(0, factionId);

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Message.ToFaction",
            $"{{\"FactionId\":{factionId},\"Message\":\"ESB test\",\"Priority\":2,\"Duration\":5}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Message.ToFaction/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        Assert.True(payload["Ok"]!.Value<bool>());
    }
}
