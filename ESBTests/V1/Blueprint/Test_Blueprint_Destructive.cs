using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IBlueprint;

/// <summary>
/// Destructive integration tests for the V1 Blueprint topic handler.
///
/// Blueprint.Finish instantly completes any in-progress blueprint in a player's factory.
/// The EntityId is the player entity ID, not a factory/constructor entity ID -- a player
/// can only have one blueprint active in the factory at a time.
///
/// Prerequisites (game-enforced -- a generic Exception is returned if either is not met):
///   1. KnownState.PlayerEntityId must identify an online player with a blueprint selected
///      and queued in their factory.
///   2. All required resources must already be present in the factory inventory.
///      Use V1.Blueprint.Resources to add them if needed.
///   - Run only when you are prepared to accept the blueprint being completed immediately.
///
/// Run with: dotnet test --filter "Category=Integration_Destructive"
/// </summary>
[Trait("Category", "Integration_Destructive")]
public class Test_Blueprint_Destructive
{
    // -------------------------------------------------------------------------
    // V1.Blueprint.Finish -- instantly completes the in-progress blueprint
    // EntityId is the player, not a factory structure. One active blueprint per player.
    // Game returns a generic Exception if no blueprint is queued or resources are insufficient.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Finish_ActiveFactory_ReturnsOk()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Blueprint.Finish",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Blueprint.Finish/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        Assert.True(payload["Ok"]!.Value<bool>());
    }
}
