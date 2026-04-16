using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V2.IContainer;

/// <summary>
/// Integration tests for the Container topic handlers.
/// Requires the game to be running with the ESB mod loaded and the saved game state
/// described in <see cref="KnownState"/> active.
///
/// The tests discover the Fridge container position at runtime via Structure.GetDevicePositions.
/// GetTotalItems and AddItems tests dynamically select an item type from the container
/// contents so they always use a valid type, even if the Fridge is restocked.
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Container_Integration
{
    private const int EID = KnownState.BaseEntityId;

    /// <summary>Returns the struct-space position string of the first Fridge device on the base.</summary>
    private static async Task<string> GetFridgePosAsync(MqttTestClient mqtt)
    {
        await KnownState.WaitForStructureReadyAsync(mqtt, EID);

        var (_, payload) = await mqtt.RequestAsync(
            "V2.Structure.GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{KnownState.DeviceName2}\"}}");

        var positions = payload["Positions"] as JArray
            ?? throw new System.Exception("No Positions array in GetDevicePositions response");
        if (positions.Count == 0)
            throw new System.Exception($"No device named '{KnownState.DeviceName2}' found on entity {EID}");

        return positions[0].ToString(Newtonsoft.Json.Formatting.None);
    }

    // -------------------------------------------------------------------------
    // Container.Get
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Get_FridgePosition_ReturnsContentAndCapacity()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        string pos = await GetFridgePosAsync(mqtt);

        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Container.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Container.Get/", topic);
        Assert.NotNull(payload["VolumeCapacity"]);
        Assert.NotNull(payload["DecayFactor"]);
        Assert.NotNull(payload["Content"]);
    }

    // -------------------------------------------------------------------------
    // Container.Contains — uses first item type from Container.Get so the query
    // is always valid regardless of what is stocked in the Fridge.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Contains_FirstContentType_ReturnsBool()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        string pos = await GetFridgePosAsync(mqtt);

        // Discover a valid item type from the current fridge content.
        var (_, getPayload) = await mqtt.RequestAsync(
            "V2.Container.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        var content = getPayload["Content"] as JArray;
        if (content == null || content.Count == 0)
        {
            // Fridge is empty — verify Contains handler responds (R or X for type 0).
            var (topic, _) = await mqtt.RequestAsync(
                "V2.Container.Contains",
                $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Type\":0}}");
            Assert.True(
                topic.StartsWith($"{KnownState.AppId}/R/V2.Container.Contains/") ||
                topic.StartsWith($"{KnownState.AppId}/X/V2.Container.Contains/"),
                $"Handler did not respond: {topic}");
            return;
        }

        int type = content[0]["Id"]!.Value<int>();
        var (responseTopic, responsePayload) = await mqtt.RequestAsync(
            "V2.Container.Contains",
            $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Type\":{type}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Container.Contains/", responseTopic);
        Assert.Equal(type, responsePayload["Type"]!.Value<int>());
        Assert.NotNull(responsePayload["Contains"]);
    }

    // -------------------------------------------------------------------------
    // Container.GetTotalItems — uses first item type from Container.Get so the
    // query is always valid regardless of what is stocked in the Fridge.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetTotalItems_FirstContentType_ReturnsCount()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        string pos = await GetFridgePosAsync(mqtt);

        // Discover a valid item type from the current fridge content.
        var (_, getPayload) = await mqtt.RequestAsync(
            "V2.Container.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        var content = getPayload["Content"] as JArray;
        if (content == null || content.Count == 0)
        {
            // Fridge is empty — verify GetTotalItems handler responds (R or X).
            var (topic, _) = await mqtt.RequestAsync(
                "V2.Container.GetTotalItems",
                $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Type\":0}}");
            Assert.True(
                topic.StartsWith($"{KnownState.AppId}/R/V2.Container.GetTotalItems/") ||
                topic.StartsWith($"{KnownState.AppId}/X/V2.Container.GetTotalItems/"),
                $"Handler did not respond: {topic}");
            return;
        }

        int type = content[0]["Id"]!.Value<int>();
        var (responseTopic, responsePayload) = await mqtt.RequestAsync(
            "V2.Container.GetTotalItems",
            $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Type\":{type}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Container.GetTotalItems/", responseTopic);
        Assert.Equal(type, responsePayload["Type"]!.Value<int>());
        Assert.NotNull(responsePayload["Count"]);
    }

    // -------------------------------------------------------------------------
    // Container.AddItems — adds 0 items of the first content type (safe no-op).
    // Uses the same dynamic type selection as GetTotalItems.
    // NOTE: count > 0 changes game state.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task AddItems_ZeroCount_ReturnsConfirmation()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        string pos = await GetFridgePosAsync(mqtt);

        // Discover a valid item type from the current fridge content.
        var (_, getPayload) = await mqtt.RequestAsync(
            "V2.Container.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        var content = getPayload["Content"] as JArray;
        if (content == null || content.Count == 0)
        {
            // Fridge is empty — verify AddItems handler responds (R or X).
            var (topic, _) = await mqtt.RequestAsync(
                "V2.Container.AddItems",
                $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Type\":0,\"Count\":0}}");
            Assert.True(
                topic.StartsWith($"{KnownState.AppId}/R/V2.Container.AddItems/") ||
                topic.StartsWith($"{KnownState.AppId}/X/V2.Container.AddItems/"),
                $"Handler did not respond: {topic}");
            return;
        }

        int type = content[0]["Id"]!.Value<int>();
        var (responseTopic, responsePayload) = await mqtt.RequestAsync(
            "V2.Container.AddItems",
            $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Type\":{type},\"Count\":0}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Container.AddItems/", responseTopic);
        Assert.NotNull(responsePayload["CouldNotAdd"]);
    }

    // -------------------------------------------------------------------------
    // Error case — no container at position returns Exception
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Get_NoContainerAtPosition_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Use the lever switch position — a switch block, not a container
        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Container.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Container.Get/", topic);
        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Error case — unknown entity returns Exception
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Get_UnknownEntityId_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        string pos = await GetFridgePosAsync(mqtt);

        var (topic, payload) = await mqtt.RequestAsync(
            "V2.Container.Get",
            $"{{\"EntityId\":999999,\"Pos\":{pos}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Container.Get/", topic);
        Assert.NotNull(payload["Error"]);
    }
}
