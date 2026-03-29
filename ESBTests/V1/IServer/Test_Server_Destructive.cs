using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IServer;

/// <summary>
/// Destructive integration tests for the V1 Server topic handler.
///
/// Each test mutates server state and restores it before returning, so net
/// change is zero on success. If a test fails after the mutation step,
/// run the unban command manually from the server console:
///   unban {KnownState.PlayerEntityId}
///
/// WARNING: The ban test may disconnect the active player from the server.
/// Run this class last -- after all other Integration_Destructive tests that
/// require an active player connection (e.g. Test_Message_Destructive).
///
/// Run alone with: dotnet test --filter "FullyQualifiedName~Test_Server_Destructive"
/// </summary>
[Trait("Category", "Integration_Destructive")]
public class Test_Server_Destructive
{
    // -------------------------------------------------------------------------
    // V1.Server.BannedPlayers -- ban adds entry; unban removes it
    // Step 1: read player Steam64 ID from PlayerInfo for verification
    // Step 2: ban <entityId> 1hr
    // Step 3: verify entry appears in banned list (matched by steam64Id)
    // Step 4: unban <entityId>
    // Step 5: verify entry is gone
    // Restore: unban in step 4 -- if test fails after step 2, run unban manually.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task BannedPlayers_BanAndUnban_EntryAppearsAndDisappears()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: get the player's Steam64 ID for ban-entry verification
        var (infoTopic, infoPayload) = await mqtt.RequestAsync(
            "V1.Player.GetInfo",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            infoTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInfo/"),
            $"GetInfo failed: {infoTopic} -- {infoPayload["Error"]?.Value<string>()}");

        string steamId    = infoPayload["Data"]!["steamId"]!.Value<string>()!;
        string playerName = infoPayload["Data"]!["playerName"]!.Value<string>()!;
        ulong steam64Id   = ulong.Parse(steamId);
        Assert.NotEqual(0UL, steam64Id);
        Assert.False(string.IsNullOrEmpty(playerName));

        // Step 2: ban the player for 1 hour -- fails here = nothing mutated yet
        // duration format is "1h", "2h", etc. (not "1hr")
        var (banTopic, banPayload) = await mqtt.RequestAsync(
            "V1.Server.ConsoleCommand",
            $"{{\"Command\":\"ban {playerName} 1h\"}}",
            appId: KnownState.V1AppId);

        Assert.True(
            banTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Server.ConsoleCommand/"),
            $"ban command failed: {banTopic} -- {banPayload["Error"]?.Value<string>()}");

        // Allow the server to process the ban before querying the list
        await Task.Delay(500);

        // Step 3: verify the entry appears
        var (listTopic, listPayload) = await mqtt.RequestAsync(
            "V1.Server.BannedPlayers",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            listTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Server.BannedPlayers/"),
            $"BannedPlayers failed: {listTopic} -- {listPayload["Error"]?.Value<string>()}");

        var banned = Assert.IsType<JArray>(listPayload["Data"]);
        Assert.Contains(banned, t => t["steam64Id"]!.Value<ulong>() == steam64Id);

        // Step 4: unban -- restore server state
        var (unbanTopic, unbanPayload) = await mqtt.RequestAsync(
            "V1.Server.ConsoleCommand",
            $"{{\"Command\":\"unban {playerName}\"}}",
            appId: KnownState.V1AppId);

        Assert.True(
            unbanTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Server.ConsoleCommand/"),
            $"unban command failed: {unbanTopic} -- {unbanPayload["Error"]?.Value<string>()}");

        await Task.Delay(500);

        // Step 5: verify the entry is gone
        var (verifyTopic, verifyPayload) = await mqtt.RequestAsync(
            "V1.Server.BannedPlayers",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            verifyTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Server.BannedPlayers/"),
            $"BannedPlayers (verify) failed: {verifyTopic} -- {verifyPayload["Error"]?.Value<string>()}");

        var verifyBanned = Assert.IsType<JArray>(verifyPayload["Data"]);
        Assert.DoesNotContain(verifyBanned, t => t["steam64Id"]!.Value<ulong>() == steam64Id);
    }
}
