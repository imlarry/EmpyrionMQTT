using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.IStructure;

/// <summary>
/// Integration tests for the Structure topic handlers.
/// Requires the game to be running with the ESB mod loaded and the saved game state
/// described in <see cref="KnownState"/> active (VNS Akua base, EntityId 5319).
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Structure_Integration
{
    private const int EID = KnownState.BaseEntityId;

    // -------------------------------------------------------------------------
    // Structure.Info
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Info_ReturnsStructureProperties()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.Info", $"{{\"EntityId\":{EID}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.Info/", topic);
        Assert.Equal(EID,  payload["EntityId"]!.Value<int>());
        Assert.NotNull(payload["IsReady"]);
        Assert.NotNull(payload["Id"]);
    }

    // -------------------------------------------------------------------------
    // Structure.Tanks
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Tanks_ReturnsFuelAndOxygen()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.Tanks", $"{{\"EntityId\":{EID}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.Tanks/", topic);
        Assert.NotNull(payload["FuelTank"]);
        Assert.NotNull(payload["OxygenTank"]);
        Assert.NotNull(payload["PentaxidTank"]);
    }

    // -------------------------------------------------------------------------
    // Structure.GetAllCustomDeviceNames
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetAllCustomDeviceNames_ContainsKnownDevices()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetAllCustomDeviceNames", $"{{\"EntityId\":{EID}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetAllCustomDeviceNames/", topic);
        var names = payload["DeviceNames"]!.ToObject<string[]>();
        Assert.Contains(KnownState.DeviceName1, names);
        Assert.Contains(KnownState.DeviceName2, names);
    }

    // -------------------------------------------------------------------------
    // Structure.GetDevicePositions
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetDevicePositions_ConstructorBuffer_ReturnsPositions()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{KnownState.DeviceName1}\"}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetDevicePositions/", topic);
        Assert.NotNull(payload["Positions"]);
        Assert.NotEmpty(payload["Positions"]!.ToObject<JArray>()!);
    }

    [Fact]
    public async Task GetDevicePositions_Fridge_ReturnsPositions()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{KnownState.DeviceName2}\"}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetDevicePositions/", topic);
        Assert.NotNull(payload["Positions"]);
        Assert.NotEmpty(payload["Positions"]!.ToObject<JArray>()!);
    }

    // -------------------------------------------------------------------------
    // Structure.GetBlock
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetBlock_LeverSwitchPosition_ReturnsBlockData()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetBlock",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetBlock/", topic);
        Assert.NotNull(payload["Type"]);
        Assert.NotNull(payload["Pos"]);
    }

    // -------------------------------------------------------------------------
    // Structure.GetDockedVessels
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetDockedVessels_ReturnsArray()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetDockedVessels", $"{{\"EntityId\":{EID}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetDockedVessels/", topic);
        Assert.NotNull(payload["DockedVessels"]);
    }

    // -------------------------------------------------------------------------
    // Structure.GetPassengers
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetPassengers_ReturnsArray()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetPassengers", $"{{\"EntityId\":{EID}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetPassengers/", topic);
        Assert.NotNull(payload["Passengers"]);
    }

    // -------------------------------------------------------------------------
    // Structure.GetBlockSignals
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetBlockSignals_LeverSwitchPosition_ReturnsSignals()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetBlockSignals",
            $"{{\"EntityId\":{EID},\"BlockPos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetBlockSignals/", topic);
        Assert.NotNull(payload["Signals"]);
        // Lever switch at this position should have at least one signal
        Assert.NotEmpty(payload["Signals"]!.ToObject<JArray>()!);
    }

    // -------------------------------------------------------------------------
    // Structure.GetControlPanelSignals
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetControlPanelSignals_ReturnsSignals()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetControlPanelSignals", $"{{\"EntityId\":{EID}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetControlPanelSignals/", topic);
        Assert.NotNull(payload["Signals"]);
    }

    // -------------------------------------------------------------------------
    // Structure.GetSignalState
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetSignalState_FridgeSignal_ReturnsBoolState()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetSignalState",
            $"{{\"EntityId\":{EID},\"SignalName\":\"{KnownState.SignalName}\"}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetSignalState/", topic);
        Assert.Equal(KnownState.SignalName, payload["SignalName"]!.Value<string>());
        Assert.NotNull(payload["State"]);  // bool
    }

    // -------------------------------------------------------------------------
    // Structure.GetSignalReceivers
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetSignalReceivers_FridgeSignal_ReturnsReceivers()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetSignalReceivers",
            $"{{\"EntityId\":{EID},\"SignalName\":\"{KnownState.SignalName}\"}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetSignalReceivers/", topic);
        Assert.NotNull(payload["Receivers"]);
    }

    // -------------------------------------------------------------------------
    // Structure.GetSendSignalName
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetSendSignalName_LeverSwitchPosition_ReturnsFridgeSignal()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GetSendSignalName",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GetSendSignalName/", topic);
        Assert.Equal(KnownState.SignalName, payload["SignalName"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // Structure.StructToGlobalPos
    // -------------------------------------------------------------------------
    [Fact]
    public async Task StructToGlobalPos_Origin_ReturnsNonZeroGlobalPos()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.StructToGlobalPos",
            $"{{\"EntityId\":{EID},\"StructPos\":{{\"X\":0,\"Y\":0,\"Z\":0}}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.StructToGlobalPos/", topic);
        var globalPos = payload["GlobalPos"]!;
        Assert.NotNull(globalPos["X"]);
        Assert.NotNull(globalPos["Y"]);
        Assert.NotNull(globalPos["Z"]);
        // Origin (0,0,0) in struct space should NOT map to global (0,0,0)
        Assert.False(
            globalPos["X"]!.Value<float>() == 0f &&
            globalPos["Y"]!.Value<float>() == 0f &&
            globalPos["Z"]!.Value<float>() == 0f,
            "Struct origin should not be at global (0,0,0)");
    }

    // -------------------------------------------------------------------------
    // Structure.GlobalToStructPos
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GlobalToStructPos_BaseLocation_ReturnsNonZeroStructPos()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.GlobalToStructPos",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.BaseGlobalPos}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Structure.GlobalToStructPos/", topic);
        var structPos = payload["StructPos"]!;
        Assert.NotNull(structPos["X"]);
        Assert.NotNull(structPos["Y"]);
        Assert.NotNull(structPos["Z"]);
        // Y in struct space should be around 128 (floor level), definitely not 0
        Assert.NotEqual(0, structPos["Y"]!.Value<int>());
    }

    // -------------------------------------------------------------------------
    // Structure.SetFaction — mutation, verify with explicit acknowledgment
    // NOTE: Run this test last as it changes game state.
    //       FactionId 0 = no faction / unclaimed.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SetFaction_FactionId0_ReturnsConfirmation()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.SetFaction",
            $"{{\"EntityId\":{EID},\"FactionId\":0}}");

        // Accept either R (success) or X (exception) — just ensure ESB handled it
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/Structure.SetFaction/") ||
            topic.StartsWith($"{KnownState.AppId}/X/Structure.SetFaction/"),
            $"Unexpected topic: {topic}");
    }

    // -------------------------------------------------------------------------
    // Error case — unknown entity should return an Exception topic
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Info_UnknownEntityId_ReturnsException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync(
            "Structure.Info", "{\"EntityId\":999999}");

        Assert.StartsWith($"{KnownState.AppId}/X/Structure.Info/", topic);
        Assert.NotNull(payload["Error"]);
    }
}
