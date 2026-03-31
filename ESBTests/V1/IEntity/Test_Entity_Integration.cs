using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IEntity;

/// <summary>
/// Integration tests for the V1 Entity topic handler.
/// Requires a MULTIPLAYER game running with a DedicatedServer and the ESB mod loaded.
///
/// V1 (ModBase/DediAPI) is only available on DedicatedServer in multiplayer.
/// These tests will time out in SinglePlayer -- V1 is never initialized in SP.
///
/// KnownState.BaseEntityId must be a structure entity present on KnownState.Playfield.
/// KnownState.V1AppId must be "DedicatedServer".
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Entity_Integration
{
    // -------------------------------------------------------------------------
    // V1.Entity.GetPosAndRot -- valid structure entity, expects position/rotation data
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetPosAndRot_ValidEntity_ReturnsPosAndRot()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Entity.GetPosAndRot",
            $"{{\"EntityId\":{KnownState.BaseEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Entity.GetPosAndRot/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.Equal(KnownState.BaseEntityId, data["id"]!.Value<int>());
        Assert.NotNull(data["pos"] as JObject);
        Assert.NotNull(data["rot"] as JObject);
    }

    // -------------------------------------------------------------------------
    // V1.Entity.NewId -- allocates a new entity ID, must be a positive integer
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NewId_ReturnsPositiveEntityId()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Entity.NewId",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Entity.NewId/"),
            $"Expected R/ but got: {topic} -- {payload["Error"]?.Value<string>()}");

        var data = payload["Data"] as JObject;
        Assert.NotNull(data);
        Assert.True(data["id"]!.Value<int>() > 0, "Expected a positive entity ID");
    }
}
