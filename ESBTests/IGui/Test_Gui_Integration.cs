using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.IGui;

[Trait("Category", "Integration")]
public class Test_Gui_Integration
{
    // -------------------------------------------------------------------------
    // Edna.TestSelf — smoke test, no game state dependency
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Edna_TestSelf_ReturnsOk()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("Edna.TestSelf", "{}");

        Assert.StartsWith($"{KnownState.AppId}/R/Edna.TestSelf/", topic);
        Assert.Equal("Edna_Selftest OK", payload["Message"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // Gui.IsWorldVisible — read-only, no side effects
    // -------------------------------------------------------------------------
    [Fact]
    public async Task IsWorldVisible_ReturnsBool()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("Gui.IsWorldVisible", "{}");

        Assert.StartsWith($"{KnownState.AppId}/R/Gui.IsWorldVisible/", topic);
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
            "Gui.ShowGameMessage",
            "{\"Text\":\"ESB Integration Test\",\"Prio\":0,\"Duration\":3.0}");

        Assert.StartsWith($"{KnownState.AppId}/R/Gui.ShowGameMessage/", topic);
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
            "Gui.ShowDialog",
            "{\"TitleText\":\"Test\",\"BodyText\":\"Integration test dialog\",\"ButtonTexts\":[\"OK\"]}");

        // Accept R (displayed) or X (no active player in dedicated mode)
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/Gui.ShowDialog/") ||
            topic.StartsWith($"{KnownState.AppId}/X/Gui.ShowDialog/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
            Assert.NotNull(payload["Displayed"]);
    }
}
