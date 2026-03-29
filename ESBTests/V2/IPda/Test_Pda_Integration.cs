using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V2.IPda;

/// <summary>
/// Integration tests for the Pda topic handlers.
///
/// NOTE: These tests will always fail. IPda is restricted to scenario script mods
/// by the game engine — ModApi.PDA is always null for general client mods like ESB,
/// regardless of whether a PDA scenario is active. See the block comment at the top
/// of ESB/TopicHandlers/V2/Pda.cs for the full investigation.
///
/// The tests and handler code are retained as a reference implementation.
/// </summary>
[Trait("Category", "Integration")]
public class Test_Pda_Integration
{
    private const string PdaUnavailable =
        "IPda is not accessible from client mods — ModApi.PDA is always null. " +
        "See block comment in ESB/TopicHandlers/V2/Pda.cs.";

    // -------------------------------------------------------------------------
    // V2.Pda.ShowMessage — minimum payload (Message only)
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task ShowMessage_MinPayload_ReturnsEchoedMessage()
    {
        Skip.If(true, PdaUnavailable);
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Pda.ShowMessage",
            "{\"Message\":\"ESB Pda Integration Test\"}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Pda.ShowMessage/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Pda.ShowMessage/"),
            $"Handler did not respond: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
            Assert.Equal("ESB Pda Integration Test", payload["Message"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // V2.Pda.ShowMessage — full parameter set
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task ShowMessage_WithAllParams_ReturnsResponse()
    {
        Skip.If(true, PdaUnavailable);
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Pda.ShowMessage",
            "{\"Message\":\"ESB Full Params Test\",\"Duration\":3.0,\"HasPrio\":true,\"CleanupFirst\":false,\"PlayerId\":-1}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Pda.ShowMessage/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Pda.ShowMessage/"),
            $"Handler did not respond: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
            Assert.Equal("ESB Full Params Test", payload["Message"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // V2.Pda.SetMapMarker — activate a marker (side effect: marker appears on map)
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetMapMarker_Activate_ReturnsEchoedMarkerName()
    {
        Skip.If(true, PdaUnavailable);
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Pda.SetMapMarker",
            "{\"Activate\":true,\"Position\":{\"X\":0.0,\"Y\":0.0,\"Z\":0.0},\"MarkerName\":\"ESB Test Marker\",\"Distance\":100}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Pda.SetMapMarker/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Pda.SetMapMarker/"),
            $"Handler did not respond: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.Equal("ESB Test Marker", payload["MarkerName"]!.Value<string>());
            Assert.True(payload["Activate"]!.Value<bool>());
        }
    }

    // -------------------------------------------------------------------------
    // V2.Pda.GetPoiEntityId — returns entity ID (0 if not found) or X on error
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task GetPoiEntityId_AnyPoiName_RespondsWithEntityId()
    {
        Skip.If(true, PdaUnavailable);
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Pda.GetPoiEntityId",
            "{\"PoiName\":\"Akua\"}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Pda.GetPoiEntityId/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Pda.GetPoiEntityId/"),
            $"Handler did not respond: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
            Assert.NotNull(payload["EntityId"]);
    }

    // -------------------------------------------------------------------------
    // V2.Pda.GetPoiLocation — returns position (Vector3.zero if not found) or X on error
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task GetPoiLocation_AnyPoiName_RespondsWithPosition()
    {
        Skip.If(true, PdaUnavailable);
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Pda.GetPoiLocation",
            "{\"PoiName\":\"Akua\"}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Pda.GetPoiLocation/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Pda.GetPoiLocation/"),
            $"Handler did not respond: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.NotNull(payload["Position"]);
            Assert.NotNull(payload["Position"]!["X"]);
            Assert.NotNull(payload["Position"]!["Y"]);
            Assert.NotNull(payload["Position"]!["Z"]);
        }
    }
}
