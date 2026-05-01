using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// <summary>
/// Integration tests for StructureHandler.
/// Requires the game running with the ESB mod loaded and the saved game described
/// in KnownState active (VNS Akua base, EntityId 5320).
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Structure_Integration
{
    private const int EID = KnownState.BaseEntityId;

    // =========================================================================
    // Structure/Info
    // =========================================================================

    [Fact]
    public async Task Info_ReturnsStructureProperties()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "Info",
            $"{{\"EntityId\":{EID}}}");

        Assert.Equal(EID, payload["EntityId"]!.Value<int>());
        Assert.NotNull(payload["IsReady"]);
        Assert.NotNull(payload["Id"]);
    }

    [Fact]
    public async Task Info_UnknownEntityId_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "Info",
            "{\"EntityId\":999999}");

        Assert.NotNull(payload["Error"]);
    }

    // =========================================================================
    // Structure/Tanks
    // =========================================================================

    [Fact]
    public async Task Tanks_ReturnsFuelOxygenPentaxid()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "Tanks",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["FuelTank"]);
        Assert.NotNull(payload["OxygenTank"]);
        Assert.NotNull(payload["PentaxidTank"]);
    }

    // =========================================================================
    // Structure/GetAllCustomDeviceNames
    // =========================================================================

    [Fact]
    public async Task GetAllCustomDeviceNames_ContainsKnownDevices()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetAllCustomDeviceNames",
            $"{{\"EntityId\":{EID}}}");

        var names = payload["DeviceNames"]!.ToObject<string[]>();
        Assert.NotNull(names);
        Assert.Contains(KnownState.DeviceName1, names);
        Assert.Contains(KnownState.DeviceName2, names);
    }

    // =========================================================================
    // Structure/GetDevicePositions
    // =========================================================================

    [Fact]
    public async Task GetDevicePositions_Constructor_ReturnsPositions()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{KnownState.DeviceName1}\"}}");

        Assert.NotNull(payload["Positions"]);
        Assert.NotEmpty(payload["Positions"]!.ToObject<JArray>()!);
    }

    [Fact]
    public async Task GetDevicePositions_Fridge_ReturnsPositions()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{KnownState.DeviceName2}\"}}");

        Assert.NotNull(payload["Positions"]);
        Assert.NotEmpty(payload["Positions"]!.ToObject<JArray>()!);
    }

    // =========================================================================
    // Structure/AddTankContent -- add 0 fuel (safe no-op)
    // =========================================================================

    [Fact]
    public async Task AddTankContent_FuelZero_ReturnsContentAndCapacity()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "AddTankContent",
            $"{{\"EntityId\":{EID},\"TankType\":\"Fuel\",\"Amount\":0.0}}");

        Assert.Equal("Fuel", payload["TankType"]!.Value<string>());
        Assert.NotNull(payload["Content"]);
        Assert.NotNull(payload["Capacity"]);
    }

    [Fact]
    public async Task AddTankContent_UnknownTankType_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "AddTankContent",
            $"{{\"EntityId\":{EID},\"TankType\":\"Plasma\",\"Amount\":0.0}}");

        Assert.NotNull(payload["Error"]);
    }

    // =========================================================================
    // Structure/GetDockedVessels
    // =========================================================================

    [Fact]
    public async Task GetDockedVessels_ReturnsArray()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetDockedVessels",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["DockedVessels"]);
    }

    // =========================================================================
    // Structure/GetPassengers
    // =========================================================================

    [Fact]
    public async Task GetPassengers_ReturnsArray()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetPassengers",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["Passengers"]);
    }

    // =========================================================================
    // Structure/GetBlockSignals
    // Filter is optional; no filter returns all signals on the structure.
    // =========================================================================

    [Fact]
    public async Task GetBlockSignals_NoFilter_ReturnsSignals()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetBlockSignals",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["Signals"]);
    }

    [Fact]
    public async Task GetBlockSignals_FilterByName_ReturnsFridgeSignal()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetBlockSignals",
            $"{{\"EntityId\":{EID},\"Filter\":\"{KnownState.SignalName}\"}}");

        Assert.NotNull(payload["Signals"]);
        var signals = payload["Signals"]!.ToObject<JArray>()!;
        Assert.NotEmpty(signals);
        Assert.All(signals, t => Assert.Contains(KnownState.SignalName,
            t["Name"]!.Value<string>()!, System.StringComparison.OrdinalIgnoreCase));
    }

    // =========================================================================
    // Structure/GetControlPanelSignals
    // =========================================================================

    [Fact]
    public async Task GetControlPanelSignals_ReturnsSignals()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetControlPanelSignals",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["Signals"]);
    }

    // =========================================================================
    // Structure/GetSignalState
    // =========================================================================

    [Fact]
    public async Task GetSignalState_FridgeSignal_ReturnsBoolState()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetSignalState",
            $"{{\"EntityId\":{EID},\"SignalName\":\"{KnownState.SignalName}\"}}");

        Assert.Equal(KnownState.SignalName, payload["SignalName"]!.Value<string>());
        Assert.NotNull(payload["State"]);
    }

    // =========================================================================
    // Structure/GetSignalReceivers
    // =========================================================================

    [Fact]
    public async Task GetSignalReceivers_FridgeSignal_ReturnsReceivers()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetSignalReceivers",
            $"{{\"EntityId\":{EID},\"SignalName\":\"{KnownState.SignalName}\"}}");

        Assert.NotNull(payload["Receivers"]);
    }

    // =========================================================================
    // Structure/GetSendSignalName
    // =========================================================================

    [Fact]
    public async Task GetSendSignalName_LeverSwitchPosition_ReturnsFridgeSignal()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GetSendSignalName",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.Equal(KnownState.SignalName, payload["SignalName"]!.Value<string>());
    }

    // =========================================================================
    // Structure/StructToGlobalPos
    // =========================================================================

    [Fact]
    public async Task StructToGlobalPos_Origin_ReturnsNonZeroGlobalPos()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "StructToGlobalPos",
            $"{{\"EntityId\":{EID},\"StructPos\":{{\"X\":0,\"Y\":0,\"Z\":0}}}}");

        var globalPos = payload["GlobalPos"]!;
        Assert.NotNull(globalPos["X"]);
        Assert.NotNull(globalPos["Y"]);
        Assert.NotNull(globalPos["Z"]);
        Assert.False(
            globalPos["X"]!.Value<float>() == 0f &&
            globalPos["Y"]!.Value<float>() == 0f &&
            globalPos["Z"]!.Value<float>() == 0f,
            "Struct origin should not map to global (0,0,0)");
    }

    // =========================================================================
    // Structure/GlobalToStructPos
    // =========================================================================

    [Fact]
    public async Task GlobalToStructPos_BaseLocation_ReturnsNonZeroStructPos()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "GlobalToStructPos",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.BaseGlobalPos}}}");

        var structPos = payload["StructPos"]!;
        Assert.NotNull(structPos["X"]);
        Assert.NotNull(structPos["Y"]);
        Assert.NotNull(structPos["Z"]);
        Assert.NotEqual(0, structPos["Y"]!.Value<int>());
    }

    // =========================================================================
    // Structure/SetFaction
    // FactionGroup is a string enum; FactionEntityId is the owning entity's ID.
    // Using FactionGroup=None and FactionEntityId=0 as a safe probe.
    // =========================================================================

    [Fact]
    public async Task SetFaction_NoneGroup_ReturnsConfirmationOrError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "SetFaction",
            $"{{\"EntityId\":{EID},\"FactionGroup\":\"None\",\"FactionEntityId\":0}}");

        Assert.True(payload["EntityId"] != null || payload["Error"] != null,
            "Expected either EntityId (success) or Error in response");
    }

    [Fact]
    public async Task SetFaction_UnknownGroup_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "SetFaction",
            $"{{\"EntityId\":{EID},\"FactionGroup\":\"NotAGroup\",\"FactionEntityId\":0}}");

        Assert.NotNull(payload["Error"]);
    }
}
