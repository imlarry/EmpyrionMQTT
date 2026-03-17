using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESBTests.ILcd;

/// <summary>
/// Integration tests for the Lcd topic handlers.
/// Requires the game to be running with the ESB mod loaded and the saved game state
/// described in <see cref="KnownState"/> active.
///
/// Setup: place an LCD panel on the test base and set its custom name to KnownState.LcdName.
/// The tests discover its struct-space position at runtime via Structure.GetDevicePositions.
///
/// InitializeAsync writes a timestamp to the LCD so it is in a known initialized state
/// before any read tests run, and so you can visually confirm the test ran in-game.
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Lcd_Integration : IAsyncLifetime
{
    private const int EID = KnownState.BaseEntityId;

    // Shared across all tests in this class.
    private MqttTestClient _mqtt = null!;
    private string _lcdPos = null!;
    private int _currentFontSize = 1;  // populated in InitializeAsync from Lcd.Get
    private string? _skipReason = null; // set if required device is not found on the base

    // -------------------------------------------------------------------------
    // IAsyncLifetime — connect once, discover LCD position, write a timestamp,
    // then read back current font size so SetFontSize always uses a valid value.
    // -------------------------------------------------------------------------
    public async Task InitializeAsync()
    {
        _mqtt = await MqttTestClient.ConnectAsync();
        try
        {
            _lcdPos = await GetLcdPosAsync(_mqtt);

            // Write a timestamp so the LCD is initialized and human-readable in-game.
            string initText = $"ESB {DateTime.Now:HH:mm:ss}";
            await _mqtt.RequestAsync(
                "Lcd.SetText",
                $"{{\"EntityId\":{EID},\"Pos\":{_lcdPos},\"Text\":\"{initText}\"}}");

            // Read back current font size — whatever the game reports is a valid value
            // we can safely pass back to SetFontSize.
            var (_, getPayload) = await _mqtt.RequestAsync(
                "Lcd.Get",
                $"{{\"EntityId\":{EID},\"Pos\":{_lcdPos}}}");
            _currentFontSize = getPayload["FontSize"]?.Value<int>() ?? 1;
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

    /// <summary>Returns the struct-space position string of the InfoLcd device on the base.</summary>
    private static async Task<string> GetLcdPosAsync(MqttTestClient mqtt)
    {
        var (_, payload) = await mqtt.RequestAsync(
            "Structure.GetDevicePositions",
            $"{{\"EntityId\":{EID},\"DeviceName\":\"{KnownState.LcdName}\"}}");

        var positions = payload["Positions"] as JArray
            ?? throw new Exception($"No Positions array in GetDevicePositions response");
        if (positions.Count == 0)
            throw new Exception($"No device named '{KnownState.LcdName}' found on entity {EID}");

        return positions[0].ToString(Newtonsoft.Json.Formatting.None);
    }

    // -------------------------------------------------------------------------
    // Lcd.SetText — writes a human-readable timestamp visible in-game
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetText_WritesTimestamp_ReturnsConfirmation()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        string text = $"ESB {DateTime.Now:HH:mm:ss}";
        var (topic, payload) = await _mqtt.RequestAsync(
            "Lcd.SetText",
            $"{{\"EntityId\":{EID},\"Pos\":{_lcdPos},\"Text\":\"{text}\"}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Lcd.SetText/", topic);
        Assert.Equal(text, payload["Text"]!.Value<string>());
    }

    // -------------------------------------------------------------------------
    // Lcd.Get — read all properties; LCD is initialized by InitializeAsync
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task Get_LcdPosition_ReturnsAllProperties()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "Lcd.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{_lcdPos}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Lcd.Get/", topic);
        Assert.NotNull(payload["Text"]);
        Assert.NotNull(payload["TextColor"]);
        Assert.NotNull(payload["BackgroundColor"]);
        Assert.NotNull(payload["FontSize"]);

        // Color objects should have RGBA components
        Assert.NotNull(payload["TextColor"]!["R"]);
        Assert.NotNull(payload["TextColor"]!["G"]);
        Assert.NotNull(payload["TextColor"]!["B"]);
        Assert.NotNull(payload["TextColor"]!["A"]);
    }

    // -------------------------------------------------------------------------
    // Lcd.SetFontSize — uses the font size read back from Lcd.Get in
    // InitializeAsync so the value is always valid for the running game version.
    // NOTE: changes game state — LCD font size is modified.
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task SetFontSize_ReturnsConfirmation()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        var (topic, payload) = await _mqtt.RequestAsync(
            "Lcd.SetFontSize",
            $"{{\"EntityId\":{EID},\"Pos\":{_lcdPos},\"FontSize\":{_currentFontSize}}}");

        Assert.StartsWith($"{KnownState.AppId}/R/Lcd.SetFontSize/", topic);
        Assert.Equal(_currentFontSize, payload["FontSize"]!.Value<int>());
    }

    // -------------------------------------------------------------------------
    // Error case — no LCD at position returns Exception
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task Get_NoLcdAtPosition_ReturnsException()
    {
        Skip.If(_skipReason != null, _skipReason ?? string.Empty);
        // Use the lever switch position — a switch block, not an LCD
        var (topic, payload) = await _mqtt.RequestAsync(
            "Lcd.Get",
            $"{{\"EntityId\":{EID},\"Pos\":{KnownState.LeverSwitchBlock}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/Lcd.Get/", topic);
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
            "Lcd.Get",
            $"{{\"EntityId\":999999,\"Pos\":{_lcdPos}}}");

        Assert.StartsWith($"{KnownState.AppId}/X/Lcd.Get/", topic);
        Assert.NotNull(payload["Error"]);
    }
}
