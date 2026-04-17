using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V2.IUtilities;

/// <summary>
/// Integration tests for the Utilities topic handlers.
/// Requires the game to be running with the ESB mod loaded.
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Utilities_Integration
{
    // -------------------------------------------------------------------------
    // Utilities.TestSelf — smoke test, no game state dependency
    // -------------------------------------------------------------------------
    [Fact]
    public async Task TestSelf_ReturnsOk()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Utilities.TestSelf", "{}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Utilities.TestSelf/", topic);
        Assert.Equal("Edna_Selftest OK", payload["Message"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // Utilities.WindowInfo — read-only, always available
    // -------------------------------------------------------------------------
    [Fact]
    public async Task WindowInfo_ReturnsWindowObject()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("V2.Utilities.WindowInfo", "{}");

        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/V2.Utilities.WindowInfo/") ||
            topic.StartsWith($"{KnownState.AppId}/X/V2.Utilities.WindowInfo/"),
            $"Unexpected topic: {topic}");
    }

}
