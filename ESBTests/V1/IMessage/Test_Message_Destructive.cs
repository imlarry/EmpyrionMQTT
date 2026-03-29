using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IMessage;

/// <summary>
/// Destructive integration tests for the V1 Message topic handler.
///
/// Dialog requires the player to click a button during the test run.
/// The test will time out (default MqttTestClient timeout) if no button is clicked.
/// Click the positive button ("OK") to let the test pass.
///
/// Run with: dotnet test --filter "Category=Integration_Destructive"
/// </summary>
[Trait("Category", "Integration_Destructive")]
public class Test_Message_Destructive
{
    // -------------------------------------------------------------------------
    // V1.Message.Dialog -- shows a two-button dialog; player must click OK
    // Response value: 0 = positive button (OK), 1 = negative button (Cancel)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Dialog_PlayerClicksOk_ReturnsZero()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Message.Dialog",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}," +
            $"\"Message\":\"ESB Dialog test -- click OK\"," +
            $"\"PosButton\":\"OK\"," +
            $"\"NegButton\":\"Cancel\"}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Message.Dialog/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.Equal(KnownState.PlayerEntityId, data["Id"]!.Value<int>());
        // 0 = positive button (OK)
        Assert.Equal(0, data["Value"]!.Value<int>());
    }
}
