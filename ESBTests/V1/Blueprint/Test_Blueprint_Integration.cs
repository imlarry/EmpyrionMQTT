using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IBlueprint;

/// <summary>
/// Integration tests for the V1 Blueprint topic handler.
/// Requires a MULTIPLAYER game running with a DedicatedServer and the ESB mod loaded.
///
/// V1 (ModBase/DediAPI) is only available on DedicatedServer in multiplayer.
/// These tests will time out if run against a SinglePlayer session.
///
/// Blueprint.Resources is a setter -- it adds or replaces items in the factory.
/// The game returns no data payload; {"Data":null} is the expected normal response.
/// An empty item list with ReplaceExisting=false is a safe no-op probe.
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Blueprint_Integration
{
    // -------------------------------------------------------------------------
    // V1.Blueprint.Resources -- empty item list, ReplaceExisting=false (no-op probe)
    // Verifies the handler responds; {"Data":null} is the normal game response for this setter.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Resources_EmptyItems_ReturnsResponse()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Blueprint.Resources",
            $"{{\"PlayerId\":{KnownState.PlayerEntityId},\"Items\":[],\"ReplaceExisting\":false}}",
            appId: KnownState.V1AppId);

        // The handler must respond. Data:null on R/ is normal for this setter command.
        // A timeout here means the handler is not registered or the server is unreachable.
        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Blueprint.Resources/") ||
            topic.StartsWith($"{KnownState.V1AppId}/X/V1.Blueprint.Resources/"),
            $"No response from handler -- got: {topic}");
    }
}
