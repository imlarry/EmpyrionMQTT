using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.IGui;

[Trait("Category", "Integration")]
public class Test_Gui_Integration
{
    // -------------------------------------------------------------------------
    // Gui.IsWorldVisible — read-only, no side effects
    // -------------------------------------------------------------------------
    [Fact]
    public async Task IsWorldVisible_ReturnsBool()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Gui.IsWorldVisible", "{}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Gui.IsWorldVisible/", topic);
        Assert.NotNull(payload["IsWorldVisible"]);
        // Must be a bool value
        Assert.IsType<bool>(payload["IsWorldVisible"]!.ToObject<object>());
    }

    // -------------------------------------------------------------------------
    // Gui.ShowGameMessage — side effect (HUD message visible in game)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ShowGameMessage_ReturnsEchoedText()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Gui.ShowGameMessage",
            "{\"Text\":\"ESB Integration Test\",\"Prio\":0,\"Duration\":3.0}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Gui.ShowGameMessage/", topic);
        Assert.Equal("ESB Integration Test", payload["Text"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // Gui.ShowDialog — side effect (dialog displayed to player)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ShowDialog_ReturnsDisplayedBool()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Gui.ShowDialog",
            "{\"TitleText\":\"Test\",\"BodyText\":\"Integration test dialog\",\"ButtonTexts\":[\"OK\"]}");

        // Accept R (displayed) or X (no active player in dedicated mode)
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Gui.ShowDialog/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Gui.ShowDialog/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
            Assert.NotNull(payload["Displayed"]);
    }
}
