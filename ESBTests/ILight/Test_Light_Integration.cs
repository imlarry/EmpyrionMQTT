using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESBTests.ILight;

/// <summary>
/// Integration tests for the Light topic handlers.
/// Requires the game to be running with the ESB mod loaded and the saved game state
/// described in <see cref="KnownState"/> active.
///
/// Setup: place a light block on the test base and set its custom name to KnownState.LightName.
/// The tests discover its struct-space position at runtime via Structure.GetDevicePositions.
///
/// InitializeAsync reads all current light properties so that set-tests can round-trip
/// the current values (idempotent — no visible change in-game).
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Light_Integration : IAsyncLifetime
{
    private const int EID = KnownState.BaseEntityId;

    private MqttTestClient _mqtt     = null!;
    private string         _lightPos = null!;
    private string?        _skipReason = null; // set if required device is not found on the base

    // Current values read back from Light.Get in InitializeAsync — used so set-tests
    // are idempotent (they restore whatever the game currently has).
    private float  _currentIntensity  = 1f;
    private float  _currentRange      = 10f;
    private float  _currentSpotAngle  = 30f;
    private string _currentLightType  = "Point";

    // -------------------------------------------------------------------------
    // IAsyncLifetime — connect once, discover light position, read current state.
    // -------------------------------------------------------------------------
    public async Task InitializeAsync()
    {
        _mqtt = await MqttTestClient.ConnectAsync();
        try
        {
            _lightPos = await GetLightPosAsync(_mqtt);

            var (_, getPayload) = await _mqtt.RequestAsync(
                "V2.Light.Get",
                $"{{\"EntityId\":{EID},\"Pos\":{_lightPos}}}");

            _currentIntensity = getPayload["Intensity"]?.Value<float>() ?? 1f;
            _currentRange     = getPayload["Range"]?.Value<float>()     ?? 10f;
            _currentSpotAngle = getPayload["SpotAngle"]?.Value<float>() ?? 30f;
            _currentLightType = getPayload["LightType"]?.Value<string>() ?? "Point";
        }
        catch (Exception ex)
        {
            _skipReason = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (_mqtt != null)
            await _mqtt.DisposeAsync();
    }

    /// <summary>Returns the struct-space position string of the Light device on the base.</summary>
    private static async Task<string> GetLightPosAsync(MqttTestClient mqtt)
    {
        var (_, payload) = await mqtt.RequestAsync(
            "V2.Structure.GetDevicePositions",
            $"{{\"EntityId\":{KnownState.BaseEntityId},\"DeviceName\":\"{KnownState.LightName}\"}}");

        var positions = payload["Positions"] as JArray
            ?? throw new Exception("No Positions array in GetDevicePositions response");
        if (positions.Count == 0)
            throw new Exception($"No device named '{KnownState.LightName}' found on entity {KnownState.BaseEntityId}");

        return positions[0].ToString(Newtonsoft.Json.Formatting.None);
    }

    // -------------------------------------------------------------------------
    // Light.Get — all properties; device is pre-existing on the base
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task Get_LightPosition_ReturnsAllProperties()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{_lightPos}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Light.Get/", topic);
        Assert.NotNull(payload["Color"]);
        Assert.NotNull(payload["Color"]!["R"]);
        Assert.NotNull(payload["Color"]!["G"]);
        Assert.NotNull(payload["Color"]!["B"]);
        Assert.NotNull(payload["Color"]!["A"]);
        Assert.NotNull(payload["Intensity"]);
        Assert.NotNull(payload["Range"]);
        Assert.NotNull(payload["LightType"]);
        Assert.NotNull(payload["BlinkInterval"]);
        Assert.NotNull(payload["BlinkLength"]);
        Assert.NotNull(payload["BlinkOffset"]);
        Assert.NotNull(payload["SpotAngle"]);
    }

    // -------------------------------------------------------------------------
    // Light.SetColor — sets white; visible in-game as a side effect
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetColor_White_ReturnsConfirmation()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.SetColor",
            $"{{\"EntityId\":{EID},\"Pos\":{_lightPos},\"Color\":{{\"R\":1.0,\"G\":1.0,\"B\":1.0,\"A\":1.0}}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Light.SetColor/", topic);
        Assert.NotNull(payload["Color"]);
    }

    // -------------------------------------------------------------------------
    // Light.SetIntensity — round-trips the current value (idempotent)
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetIntensity_CurrentValue_ReturnsConfirmation()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.SetIntensity",
            $"{{\"EntityId\":{EID},\"Pos\":{_lightPos},\"Intensity\":{_currentIntensity}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Light.SetIntensity/", topic);
        Assert.Equal(_currentIntensity, payload["Intensity"]!.Value<float>());
    }

    // -------------------------------------------------------------------------
    // Light.SetRange — round-trips the current value (idempotent)
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetRange_CurrentValue_ReturnsConfirmation()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.SetRange",
            $"{{\"EntityId\":{EID},\"Pos\":{_lightPos},\"Range\":{_currentRange}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Light.SetRange/", topic);
        Assert.Equal(_currentRange, payload["Range"]!.Value<float>());
    }

    // -------------------------------------------------------------------------
    // Light.SetLightType — round-trips the current type (idempotent)
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetLightType_CurrentType_ReturnsConfirmation()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.SetLightType",
            $"{{\"EntityId\":{EID},\"Pos\":{_lightPos},\"LightType\":\"{_currentLightType}\"}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Light.SetLightType/", topic);
        Assert.Equal(_currentLightType, payload["LightType"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // Light.SetBlinkData — disables blinking (interval=0 → steady on)
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetBlinkData_ZeroValues_ReturnsConfirmation()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.SetBlinkData",
            $"{{\"EntityId\":{EID},\"Pos\":{_lightPos},\"BlinkInterval\":0.0,\"BlinkLength\":0.0,\"BlinkOffset\":0.0}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Light.SetBlinkData/", topic);
        Assert.Equal(0f, payload["BlinkInterval"]!.Value<float>());
        Assert.Equal(0f, payload["BlinkLength"]!.Value<float>());
        Assert.Equal(0f, payload["BlinkOffset"]!.Value<float>());
    }

    // -------------------------------------------------------------------------
    // Light.SetSpotAngle — round-trips the current value (idempotent)
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetSpotAngle_CurrentValue_ReturnsConfirmation()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.SetSpotAngle",
            $"{{\"EntityId\":{EID},\"Pos\":{_lightPos},\"SpotAngle\":{_currentSpotAngle}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Light.SetSpotAngle/", topic);
        Assert.Equal(_currentSpotAngle, payload["SpotAngle"]!.Value<float>());
    }

    // -------------------------------------------------------------------------
    // Error case — no light at position returns Exception
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task Get_NoLightAtPosition_ReturnsException()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Light.Get/", topic);
        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Error case — unknown entity returns Exception
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task Get_UnknownEntityId_ReturnsException()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Light.Get",
            $"{{\"EntityId\":999999,\"Pos\":{_lightPos}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Light.Get/", topic);
        Assert.NotNull(payload["Error"]);
    }
}
