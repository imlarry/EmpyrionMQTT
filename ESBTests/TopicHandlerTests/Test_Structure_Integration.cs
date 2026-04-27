using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// <summary>
/// Integration tests for StructureHandler and its Device sub-scope handlers.
/// Requires the game running with the ESB mod loaded and the saved game described
/// in KnownState active (VNS Akua base, EntityId 5320).
///
/// Device tests (Lcd, Light, Container, Teleporter) require the named devices from
/// KnownState to be present on the base. Device position is discovered via
/// Structure/Req/get/GetDevicePositions before each test.
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Structure_Integration
{
    private const int EID = KnownState.BaseEntityId;

    // =========================================================================
    // Structure/Req/get/Info
    // =========================================================================

    [Fact]
    public async Task Info_ReturnsStructureProperties()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/Info",
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
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/Info",
            "{\"EntityId\":999999}");

        Assert.NotNull(payload["Error"]);
    }

    // =========================================================================
    // Structure/Req/get/Tanks
    // =========================================================================

    [Fact]
    public async Task Tanks_ReturnsFuelOxygenPentaxid()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/Tanks",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["FuelTank"]);
        Assert.NotNull(payload["OxygenTank"]);
        Assert.NotNull(payload["PentaxidTank"]);
    }

    // =========================================================================
    // Structure/Req/get/GetAllCustomDeviceNames
    // =========================================================================

    [Fact]
    public async Task GetAllCustomDeviceNames_ContainsKnownDevices()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetAllCustomDeviceNames",
            $"{{\"EntityId\":{EID}}}");

        var names = payload["DeviceNames"]!.ToObject<string[]>();
        Assert.NotNull(names);
        Assert.Contains(KnownState.DeviceName1, names);
        Assert.Contains(KnownState.DeviceName2, names);
    }

    // =========================================================================
    // Structure/Req/get/GetDevicePositions
    // =========================================================================

    [Fact]
    public async Task GetDevicePositions_Constructor_ReturnsPositions()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{KnownState.DeviceName1}\"}}");

        Assert.NotNull(payload["Positions"]);
        Assert.NotEmpty(payload["Positions"]!.ToObject<JArray>()!);
    }

    [Fact]
    public async Task GetDevicePositions_Fridge_ReturnsPositions()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{KnownState.DeviceName2}\"}}");

        Assert.NotNull(payload["Positions"]);
        Assert.NotEmpty(payload["Positions"]!.ToObject<JArray>()!);
    }

    // =========================================================================
    // Structure/Req/set/AddTankContent -- add 0 fuel (safe no-op)
    // =========================================================================

    [Fact]
    public async Task AddTankContent_FuelZero_ReturnsContentAndCapacity()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "set/AddTankContent",
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
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "set/AddTankContent",
            $"{{\"EntityId\":{EID},\"TankType\":\"Plasma\",\"Amount\":0.0}}");

        Assert.NotNull(payload["Error"]);
    }

    // =========================================================================
    // Structure/Req/get/GetDockedVessels
    // =========================================================================

    [Fact]
    public async Task GetDockedVessels_ReturnsArray()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetDockedVessels",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["DockedVessels"]);
    }

    // =========================================================================
    // Structure/Req/get/GetPassengers
    // =========================================================================

    [Fact]
    public async Task GetPassengers_ReturnsArray()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetPassengers",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["Passengers"]);
    }

    // =========================================================================
    // Structure/Req/get/GetBlockSignals
    // Filter is optional; no filter returns all signals on the structure.
    // =========================================================================

    [Fact]
    public async Task GetBlockSignals_NoFilter_ReturnsSignals()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetBlockSignals",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["Signals"]);
    }

    [Fact]
    public async Task GetBlockSignals_FilterByName_ReturnsFridgeSignal()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetBlockSignals",
            $"{{\"EntityId\":{EID},\"Filter\":\"{KnownState.SignalName}\"}}");

        Assert.NotNull(payload["Signals"]);
        var signals = payload["Signals"]!.ToObject<JArray>()!;
        Assert.NotEmpty(signals);
        Assert.All(signals, t => Assert.Contains(KnownState.SignalName,
            t["Name"]!.Value<string>()!, System.StringComparison.OrdinalIgnoreCase));
    }

    // =========================================================================
    // Structure/Req/get/GetControlPanelSignals
    // =========================================================================

    [Fact]
    public async Task GetControlPanelSignals_ReturnsSignals()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetControlPanelSignals",
            $"{{\"EntityId\":{EID}}}");

        Assert.NotNull(payload["Signals"]);
    }

    // =========================================================================
    // Structure/Req/get/GetSignalState
    // =========================================================================

    [Fact]
    public async Task GetSignalState_FridgeSignal_ReturnsBoolState()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetSignalState",
            $"{{\"EntityId\":{EID},\"SignalName\":\"{KnownState.SignalName}\"}}");

        Assert.Equal(KnownState.SignalName, payload["SignalName"]!.Value<string>());
        Assert.NotNull(payload["State"]);
    }

    // =========================================================================
    // Structure/Req/get/GetSignalReceivers
    // =========================================================================

    [Fact]
    public async Task GetSignalReceivers_FridgeSignal_ReturnsReceivers()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetSignalReceivers",
            $"{{\"EntityId\":{EID},\"SignalName\":\"{KnownState.SignalName}\"}}");

        Assert.NotNull(payload["Receivers"]);
    }

    // =========================================================================
    // Structure/Req/get/GetSendSignalName
    // =========================================================================

    [Fact]
    public async Task GetSendSignalName_LeverSwitchPosition_ReturnsFridgeSignal()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetSendSignalName",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.Equal(KnownState.SignalName, payload["SignalName"]!.Value<string>());
    }

    // =========================================================================
    // Structure/Req/get/StructToGlobalPos
    // =========================================================================

    [Fact]
    public async Task StructToGlobalPos_Origin_ReturnsNonZeroGlobalPos()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/StructToGlobalPos",
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
    // Structure/Req/get/GlobalToStructPos
    // =========================================================================

    [Fact]
    public async Task GlobalToStructPos_BaseLocation_ReturnsNonZeroStructPos()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GlobalToStructPos",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.BaseGlobalPos}}}");

        var structPos = payload["StructPos"]!;
        Assert.NotNull(structPos["X"]);
        Assert.NotNull(structPos["Y"]);
        Assert.NotNull(structPos["Z"]);
        Assert.NotEqual(0, structPos["Y"]!.Value<int>());
    }

    // =========================================================================
    // Structure/Req/set/SetFaction
    // FactionGroup is a string enum; FactionEntityId is the owning entity's ID.
    // Using FactionGroup=None and FactionEntityId=0 as a safe probe.
    // =========================================================================

    [Fact]
    public async Task SetFaction_NoneGroup_ReturnsConfirmationOrError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "set/SetFaction",
            $"{{\"EntityId\":{EID},\"FactionGroup\":\"None\",\"FactionEntityId\":0}}");

        Assert.True(payload["EntityId"] != null || payload["Error"] != null,
            "Expected either EntityId (success) or Error in response");
    }

    [Fact]
    public async Task SetFaction_UnknownGroup_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "Structure", "set/SetFaction",
            $"{{\"EntityId\":{EID},\"FactionGroup\":\"NotAGroup\",\"FactionEntityId\":0}}");

        Assert.NotNull(payload["Error"]);
    }

    // =========================================================================
    // Device sub-scope -- LCD
    // Topic: Structure/Device/{name}/Req/get/Lcd
    // Payload: { EntityId, Pos }  -- Pos is discovered via GetDevicePositions
    // =========================================================================

    private async Task<(SBTestClient? mqtt, string? connId, JToken? pos)> GetDevicePos(
        string deviceName)
    {
        var mqtt   = await SBTestClient.ConnectAsync();
        var connId = await mqtt.FindConnectionAsync("Client");
        var result = await mqtt.RequestAsync(connId, "Client", "Structure", "get/GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{deviceName}\"}}");
        var positions = result["Positions"] as JArray;
        if (positions == null || positions.Count == 0)
        {
            await mqtt.DisposeAsync();
            return (null, null, null);
        }
        return (mqtt, connId, positions[0]);
    }


    [Fact]
    public async Task Lcd_Get_ReturnsTextAndColors()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.LcdName);
        Skip.If(mqttN == null, $"Device '{KnownState.LcdName}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        var payload = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.LcdName}", "get/Lcd",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        Assert.Equal(EID,              payload["EntityId"]!.Value<int>());
        Assert.Equal(KnownState.LcdName, payload["DeviceName"]!.Value<string>());
        Assert.NotNull(payload["Text"]);
        Assert.NotNull(payload["FontSize"]);
        Assert.NotNull(payload["TextColor"]);
        Assert.NotNull(payload["BackgroundColor"]);
    }

    [Fact]
    public async Task Lcd_SetText_RoundTrips()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.LcdName);
        Skip.If(mqttN == null, $"Device '{KnownState.LcdName}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        // Read current text
        var before = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.LcdName}", "get/Lcd",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");
        string originalText = before["Text"]!.Value<string>() ?? string.Empty;

        // Set same text (no-op)
        var set = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.LcdName}", "set/Text",
            $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Value\":{Newtonsoft.Json.JsonConvert.SerializeObject(originalText)}}}");

        Assert.Equal(originalText, set["Value"]!.Value<string>());
    }

    [Fact]
    public async Task Lcd_SetFontSize_RoundTrips()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.LcdName);
        Skip.If(mqttN == null, $"Device '{KnownState.LcdName}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        var before = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.LcdName}", "get/Lcd",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");
        int fontSize = before["FontSize"]!.Value<int>();

        var set = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.LcdName}", "set/FontSize",
            $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Value\":{fontSize}}}");

        Assert.Equal(fontSize, set["Value"]!.Value<int>());
    }

    // =========================================================================
    // Device sub-scope -- Light
    // =========================================================================

    [Fact]
    public async Task Light_Get_ReturnsAllProperties()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.LightName);
        Skip.If(mqttN == null, $"Device '{KnownState.LightName}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        var payload = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.LightName}", "get/Light",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        Assert.NotNull(payload["Color"]);
        Assert.NotNull(payload["Intensity"]);
        Assert.NotNull(payload["Range"]);
        Assert.NotNull(payload["LightType"]);
        Assert.NotNull(payload["SpotAngle"]);
        Assert.NotNull(payload["BlinkInterval"]);
    }

    [Fact]
    public async Task Light_SetIntensity_RoundTrips()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.LightName);
        Skip.If(mqttN == null, $"Device '{KnownState.LightName}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        var before = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.LightName}", "get/Light",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");
        float intensity = before["Intensity"]!.Value<float>();

        var set = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.LightName}", "set/Intensity",
            $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Value\":{intensity}}}");

        Assert.Equal(intensity, set["Value"]!.Value<float>(), 3);
    }

    // =========================================================================
    // Device sub-scope -- Container (Fridge)
    // =========================================================================

    [Fact]
    public async Task Container_Get_ReturnsContentArray()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.DeviceName2);
        Skip.If(mqttN == null, $"Device '{KnownState.DeviceName2}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        var payload = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.DeviceName2}", "get/Container",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        Assert.NotNull(payload["Content"]);
        Assert.NotNull(payload["VolumeCapacity"]);
        Assert.NotNull(payload["DecayFactor"]);
    }

    [Fact]
    public async Task Container_Contains_ReturnsBool()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.DeviceName2);
        Skip.If(mqttN == null, $"Device '{KnownState.DeviceName2}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        // Discover a real item type from the container
        var get = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.DeviceName2}", "get/Container",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");
        var content = get["Content"] as JArray;
        Skip.If(content == null || content.Count == 0, "Container is empty -- skip Contains test");

        int itemType = content![0]["Id"]!.Value<int>();
        var payload = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.DeviceName2}", "get/Contains",
            $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Type\":{itemType}}}");

        Assert.NotNull(payload["Contains"]);
        Assert.True(payload["Contains"]!.Value<bool>());
    }

    [Fact]
    public async Task Container_GetTotalItems_ReturnsCount()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.DeviceName2);
        Skip.If(mqttN == null, $"Device '{KnownState.DeviceName2}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        var get = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.DeviceName2}", "get/Container",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");
        var content = get["Content"] as JArray;
        Skip.If(content == null || content.Count == 0, "Container is empty -- skip GetTotalItems test");

        int itemType = content![0]["Id"]!.Value<int>();
        var payload = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.DeviceName2}", "get/GetTotalItems",
            $"{{\"EntityId\":{EID},\"Pos\":{pos},\"Type\":{itemType}}}");

        Assert.True(payload["Count"]!.Value<int>() > 0);
    }

    // =========================================================================
    // Device sub-scope -- Teleporter
    // =========================================================================

    [Fact]
    public async Task Teleporter_Get_ReturnsTargetData()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.TeleporterName);
        Skip.If(mqttN == null, $"Device '{KnownState.TeleporterName}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        var payload = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.TeleporterName}", "get/Teleporter",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        Assert.NotNull(payload["TargetEntityNameOrGroup"]);
        Assert.NotNull(payload["TargetPlayfield"]);
        Assert.NotNull(payload["TargetSolarSystemName"]);
        Assert.NotNull(payload["Origin"]);
    }

    [Fact]
    public async Task Teleporter_Set_RoundTrips()
    {
        var (mqttN, connIdN, posN) = await GetDevicePos(KnownState.TeleporterName);
        Skip.If(mqttN == null, $"Device '{KnownState.TeleporterName}' not found on entity {EID}");
        var mqtt = mqttN!; var connId = connIdN!; var pos = posN!;
        await using var _ = mqtt;

        var before = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.TeleporterName}", "get/Teleporter",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}}}");

        string target   = before["TargetEntityNameOrGroup"]!.Value<string>() ?? string.Empty;
        string playfield = before["TargetPlayfield"]!.Value<string>() ?? string.Empty;
        byte   origin   = before["Origin"]!.Value<byte>();

        var set = await mqtt.RequestAsync(connId, "Client",
            $"Structure/Device/{KnownState.TeleporterName}", "set/Teleporter",
            $"{{\"EntityId\":{EID},\"Pos\":{pos}," +
            $"\"TargetEntityNameOrGroup\":{Newtonsoft.Json.JsonConvert.SerializeObject(target)}," +
            $"\"TargetPlayfield\":{Newtonsoft.Json.JsonConvert.SerializeObject(playfield)}," +
            $"\"Origin\":{origin}}}");

        Assert.Equal(target,    set["TargetEntityNameOrGroup"]!.Value<string>());
        Assert.Equal(playfield, set["TargetPlayfield"]!.Value<string>());
        Assert.Equal(origin,    set["Origin"]!.Value<byte>());
    }
}
