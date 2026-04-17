using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.V1.IEntity;

/// <summary>
/// Destructive integration tests for the V1 Entity topic handler.
///
/// Each test reads the entity's current state, performs the mutation, then
/// restores to what was read -- so net game-state change is zero on success.
/// If a test fails mid-sequence the entity may be left in a modified state.
/// Exit without saving (or use Scripts/Restore-TestSave.ps1) before re-running.
///
/// Run with: dotnet test --filter "Category=Integration_Destructive"
///
/// Prerequisites:
///   KnownState.BaseEntityId -- a structure entity on KnownState.Playfield
///   KnownState.BaseName     -- the entity's original name (used for restore)
///   KnownState.PlayerEntityId -- active player entity (used for Teleport test)
/// </summary>
[Trait("Category", "Integration_Destructive")]
public class Test_Entity_Destructive
{
    // -------------------------------------------------------------------------
    // V1.Entity.SetName -- renames the base structure and restores the original name
    // Restore: original name written back via SetName
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SetName_RenamesAndRestores()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: rename to a test value
        const string testName = "ESB Test Entity";

        var (setTopic, setPayload) = await mqtt.RequestAsync(
            "V1.Entity.SetName",
            $"{{\"EntityId\":{KnownState.BaseEntityId}," +
            $"\"Playfield\":\"{KnownState.Playfield}\"," +
            $"\"Name\":\"{testName}\"}}",
            appId: KnownState.V1AppId);

        Assert.True(
            setTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Entity.SetName/"),
            $"SetName (rename) failed: {setTopic} -- {setPayload["Error"]?.Value<string>()}");

        Assert.True((bool?)setPayload["Ok"] == true);

        // Step 2: restore original name -- fails here = base is renamed, restore from save
        var (restoreTopic, restorePayload) = await mqtt.RequestAsync(
            "V1.Entity.SetName",
            $"{{\"EntityId\":{KnownState.BaseEntityId}," +
            $"\"Playfield\":\"{KnownState.Playfield}\"," +
            $"\"Name\":\"{KnownState.BaseName}\"}}",
            appId: KnownState.V1AppId);

        Assert.True(
            restoreTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Entity.SetName/"),
            $"SetName (restore) failed: {restoreTopic} -- {restorePayload["Error"]?.Value<string>()}");
    }

    // -------------------------------------------------------------------------
    // V1.Entity.Teleport -- teleports the player entity within the playfield and
    // restores to the original position.
    // Restore: second Teleport call returns the entity to the captured position.
    // Note: the player entity is used as the test subject to avoid displacing the
    // base structure. Y is advisory -- terrain height takes precedence on arrival.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Teleport_MovesAndRestores()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: capture current position via GetPosAndRot
        var (getTopic, getPayload) = await mqtt.RequestAsync(
            "V1.Entity.GetPosAndRot",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            getTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Entity.GetPosAndRot/"),
            $"GetPosAndRot failed: {getTopic} -- {getPayload["Error"]?.Value<string>()}");

        var data = getPayload["Data"] as JObject;
        Assert.NotNull(data);
        var origPos = data["Pos"] as JObject;
        var origRot = data["Rot"] as JObject;
        Assert.NotNull(origPos);
        Assert.NotNull(origRot);

        // Step 2: teleport to spawn point (safe landing near the base)
        var (goTopic, goPayload) = await mqtt.RequestAsync(
            "V1.Entity.Teleport",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}," +
            $"\"Pos\":{KnownState.PlayerSpawnPosV1}," +
            $"\"Rot\":{KnownState.PlayerSpawnRotV1}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            goTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Entity.Teleport/"),
            $"Teleport (go) failed: {goTopic} -- {goPayload["Error"]?.Value<string>()}");

        // Step 3: restore to captured position -- fails here = player moved, restore from save
        float ox = origPos["X"]!.Value<float>();
        float oy = origPos["Y"]!.Value<float>();
        float oz = origPos["Z"]!.Value<float>();
        float rx = origRot["X"]!.Value<float>();
        float ry = origRot["Y"]!.Value<float>();
        float rz = origRot["Z"]!.Value<float>();

        var restorePos = $"{{\"X\":{ox},\"Y\":{oy},\"Z\":{oz}}}";
        var restoreRot = $"{{\"X\":{rx},\"Y\":{ry},\"Z\":{rz}}}";

        var (returnTopic, returnPayload) = await mqtt.RequestAsync(
            "V1.Entity.Teleport",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}," +
            $"\"Pos\":{restorePos}," +
            $"\"Rot\":{restoreRot}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            returnTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Entity.Teleport/"),
            $"Teleport (restore) failed: {returnTopic} -- {returnPayload["Error"]?.Value<string>()}");
    }

}
