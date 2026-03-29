using ESBTests.Infrastructure;
using System.Threading.Tasks;

namespace ESBTests.V1.IServer;

/// <summary>
/// Destructive integration tests for the V1 Server topic handler.
///
/// Currently uses the "help" console command as a safe probe. The original
/// ban/unban test was removed because banning the active player disconnects
/// them from the server, invalidating subsequent tests in the same run.
/// A better non-destructive command should be identified before re-introducing
/// a mutation test here.
///
/// Run alone with: dotnet test --filter "FullyQualifiedName~Test_Server_Destructive"
/// </summary>
[Trait("Category", "Integration_Destructive")]
public class Test_Server_Destructive
{
    // -------------------------------------------------------------------------
    // V1.Server.ConsoleCommand -- verify the handler responds to a safe command
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ConsoleCommand_Help_ReturnsResponse()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Server.ConsoleCommand",
            "{\"Command\":\"help\"}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Server.ConsoleCommand/"),
            $"ConsoleCommand failed: {topic} -- {payload["Error"]?.Value<string>()}");
    }
}
