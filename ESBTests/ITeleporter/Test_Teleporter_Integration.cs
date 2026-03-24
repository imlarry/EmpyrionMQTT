using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESBTests.ITeleporter;

/// <summary>
/// Integration tests for the Teleporter topic handlers.
/// Requires the game to be running with the ESB mod loaded and the saved game state
/// described in <see cref="KnownState"/> active.
///
/// Setup: place a teleporter pad on the test base and set its custom name to
/// KnownState.TeleporterName ("Teleport"). Note that "Teleport" is also the default
/// name of a signal and a switch — the device position lookup uses GetDevicePositions
/// which matches on device type, so only the actual teleporter pad is returned.
///
/// The Set test round-trips the current TargetData so it is idempotent (no change
/// to game state).
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Teleporter_Integration : IAsyncLifetime
{
    private const int EID = KnownState.BaseEntityId;

    private MqttTestClient _mqtt          = null!;
    private string         _teleporterPos = null!;
    private string?        _skipReason    = null; // set if required device is not found on the base

    // Current TargetData read from Teleporter.Get in InitializeAsync —
    // used so Set is idempotent.
    private string _targetEntityNameOrGroup = null!;
    private string _targetPlayfield         = null!;
    private string _targetSolarSystemName   = null!;
    private int    _origin                  = byte.MaxValue;

    // -------------------------------------------------------------------------
    // IAsyncLifetime — connect once, discover teleporter position, read target data.
    // -------------------------------------------------------------------------
    public async Task InitializeAsync()
    {
        _mqtt = await MqttTestClient.ConnectAsync();
        try
        {
            _teleporterPos = await GetTeleporterPosAsync(_mqtt);

            var (_, payload) = await _mqtt.RequestAsync(
                "V2.Teleporter.Get",
                $"{{\"EntityId\":{EID},\"Pos\":{_teleporterPos}}}");

            _targetEntityNameOrGroup = payload["TargetEntityNameOrGroup"]?.Value<string>() ?? string.Empty;
            _targetPlayfield         = payload["TargetPlayfield"]?.Value<string>()         ?? string.Empty;
            _targetSolarSystemName   = payload["TargetSolarSystemName"]?.Value<string>()   ?? string.Empty;
            _origin                  = payload["Origin"]?.Value<int>()                     ?? byte.MaxValue;
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

    /// <summary>Returns the struct-space position string of the Teleporter device on the base.</summary>
    private static async Task<string> GetTeleporterPosAsync(MqttTestClient mqtt)
    {
        var (_, payload) = await mqtt.RequestAsync(
            "V2.Structure.GetDevicePositions",
            $"{{\"EntityId\":{KnownState.BaseEntityId},\"DeviceName\":\"{KnownState.TeleporterName}\"}}");

        var positions = payload["Positions"] as JArray
            ?? throw new Exception("No Positions array in GetDevicePositions response");
        if (positions.Count == 0)
            throw new Exception($"No device named '{KnownState.TeleporterName}' found on entity {KnownState.BaseEntityId}");

        return positions[0].ToString(Newtonsoft.Json.Formatting.None);
    }

    // -------------------------------------------------------------------------
    // Teleporter.Get — reads TargetData fields
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task Get_TeleporterPosition_ReturnsTargetData()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Teleporter.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{_teleporterPos}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Teleporter.Get/", topic);
        Assert.NotNull(payload["TargetEntityNameOrGroup"]);
        Assert.NotNull(payload["TargetPlayfield"]);
        Assert.NotNull(payload["TargetSolarSystemName"]);
        Assert.NotNull(payload["Origin"]);
    }

    // -------------------------------------------------------------------------
    // Teleporter.Set — round-trips current TargetData (idempotent, no state change)
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task Set_CurrentTargetData_RoundTrips()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        // Escape null/empty strings as JSON null so the server receives valid JSON.
        string nameOrGroupJson = _targetEntityNameOrGroup == null
            ? "null"
            : $"\"{_targetEntityNameOrGroup}\"";
        string playfieldJson = _targetPlayfield == null
            ? "null"
            : $"\"{_targetPlayfield}\"";
        string solarSystemJson = _targetSolarSystemName == null
            ? "null"
            : $"\"{_targetSolarSystemName}\"";

        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Teleporter.Set",
            $"{{\"EntityId\":{EID},\"Pos\":{_teleporterPos}," +
            $"\"TargetEntityNameOrGroup\":{nameOrGroupJson}," +
            $"\"TargetPlayfield\":{playfieldJson}," +
            $"\"TargetSolarSystemName\":{solarSystemJson}," +
            $"\"Origin\":{_origin}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/V2.Teleporter.Set/", topic);
        Assert.Equal(_origin, payload["Origin"]!.Value<int>());
    }

    // -------------------------------------------------------------------------
    // Error case — no teleporter at position returns Exception
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task Get_NoTeleporterAtPosition_ReturnsException()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "V2.Teleporter.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Teleporter.Get/", topic);
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
            "V2.Teleporter.Get",
            $"{{\"EntityId\":999999,\"Pos\":{_teleporterPos}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/V2.Teleporter.Get/", topic);
        Assert.NotNull(payload["Error"]);
    }
}
