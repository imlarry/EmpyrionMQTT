using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.IPlayer;

/// <summary>
/// Integration tests for the Player topic handlers.
/// Requires the game to be running with the ESB mod loaded.
///
/// Player.SteamId, Player.Stats, and Player.Teleport all require
/// an active local player (single-player / hosted game).
/// In dedicated-server mode LocalPlayer is null and handlers return X.
///
/// Run with: dotnet test --filter "Category=Integration"
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Player_Integration
{
    // -------------------------------------------------------------------------
    // Player.SteamId — read-only, needs LocalPlayer
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SteamId_ReturnsSteamIdOrException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("Player.SteamId", "{}");

        // R (LocalPlayer present) or X (dedicated / no active player)
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/Player.SteamId/") ||
            topic.StartsWith($"{KnownState.AppId}/X/Player.SteamId/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            Assert.NotNull(payload["SteamId"]);
            Assert.False(string.IsNullOrEmpty(payload["SteamId"]!.Value<string>()),
                "SteamId should be a non-empty string");
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    // -------------------------------------------------------------------------
    // Player.Stats — read-only, needs LocalPlayer
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Stats_ReturnsVitalStats()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        var (topic, payload) = await mqtt.RequestAsync("Player.Stats", "{}");

        // R (LocalPlayer present) or X (dedicated / no active player)
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/Player.Stats/") ||
            topic.StartsWith($"{KnownState.AppId}/X/Player.Stats/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
        {
            // Core vitals must be present
            Assert.NotNull(payload["Health"]);
            Assert.NotNull(payload["HealthMax"]);
            Assert.NotNull(payload["Oxygen"]);
            Assert.NotNull(payload["Food"]);
            Assert.NotNull(payload["Stamina"]);
            Assert.NotNull(payload["Credits"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    // -------------------------------------------------------------------------
    // Player.Teleport — mutation (pos-only variant, stays on current playfield)
    // NOTE: will move the player if LocalPlayer is active. Use carefully.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Teleport_PosOnly_ReturnsBoolOrException()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();
        // Teleport to the base location — safe to re-enter repeatedly
        var (topic, payload) = await mqtt.RequestAsync(
            "Player.Teleport",
            $"{{\"Pos\":{KnownState.BaseGlobalPos}}}");

        // R (success) or X (no LocalPlayer in dedicated mode)
        Assert.True(
            topic.StartsWith($"{KnownState.AppId}/R/Player.Teleport/") ||
            topic.StartsWith($"{KnownState.AppId}/X/Player.Teleport/"),
            $"Unexpected topic: {topic}");

        if (topic.StartsWith($"{KnownState.AppId}/R/"))
            Assert.NotNull(payload["Teleport"]);
    }
}
