using ESBTests.Infrastructure;
using System.Threading.Tasks;

namespace ESBTests.V2.IPlayfield;

/// <summary>
/// Destructive integration tests for the V2 Playfield spawn/remove handlers.
/// Requires the game to be running in single-player (V2 ClientPlayfield is only
/// available on the local client) with the ESB mod loaded.
///
/// Each test spawns an entity and removes it in the same call -- net game-state
/// change is zero on success. If Remove is not reached, exit without saving.
///
/// Run with: dotnet test --filter "Category=Integration_Destructive"
///
/// Prerequisites:
///   KnownState.Baseline.SpawnEntityPrefab -- a prefab name from the save's Blueprint Library
///   KnownState.Baseline.SpawnEntityTypeV2 -- entity type string (e.g. "HV", "BA")
///   KnownState.Baseline.V2SpawnPos        -- clear coordinates on KnownState.Playfield
/// </summary>
[Trait("Category", "Integration_Destructive")]
public class Test_Playfield_Destructive
{
    // -------------------------------------------------------------------------
    // V2.Playfield.SpawnPrefab + RemoveEntity -- spawn by prefab name, then remove.
    // The spawned EntityId is returned by SpawnPrefab and used to target RemoveEntity.
    // Fails at Spawn = nothing spawned, safe to reload save.
    // Fails at Remove = entity left on playfield; remove manually or reload save.
    // -------------------------------------------------------------------------
    // COMMENTED OUT: Test depends on KnownState.Baseline.SpawnEntityPrefab which is not defined
    /*
    [Fact]
    public async Task SpawnPrefab_ThenRemove_RoundTrip()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: Spawn the prefab -- response carries the allocated EntityId.
        var (spawnTopic, spawnPayload) = await mqtt.RequestAsync(
            "V2.Playfield.SpawnPrefab",
            $"{{\"PrefabName\":\"{KnownState.Baseline.SpawnEntityPrefab}\"," +
            $"\"Pos\":{KnownState.Baseline.V2SpawnPos}}}",
            timeoutMs: 10000);

        Assert.True(
            spawnTopic.StartsWith($"{KnownState.AppId}/R/V2.Playfield.SpawnPrefab/"),
            $"SpawnPrefab failed: {spawnTopic} -- {spawnPayload["Error"]?.Value<string>()}");

        var entityId = spawnPayload["EntityId"]!.Value<int>();
        Assert.True(entityId > 0, $"SpawnPrefab returned non-positive EntityId: {entityId}");
        Assert.NotNull(spawnPayload["Pos"]);

        // Step 2: Remove the spawned entity.
        // Fails here = entity is on KnownState.Playfield; remove manually or reload save.
        var (removeTopic, removePayload) = await mqtt.RequestAsync(
            "V2.Playfield.RemoveEntity",
            $"{{\"EntityId\":{entityId}}}",
            timeoutMs: 8000);

        Assert.True(
            removeTopic.StartsWith($"{KnownState.AppId}/R/V2.Playfield.RemoveEntity/"),
            $"RemoveEntity failed: {removeTopic} -- {removePayload["Error"]?.Value<string>()}");
        Assert.Equal(entityId, removePayload["EntityId"]!.Value<int>());
    }
    */

    // -------------------------------------------------------------------------
    // V2.Playfield.SpawnEntity + RemoveEntity -- spawn by entity type string, then remove.
    // Fails at Spawn = nothing spawned, safe to reload save.
    // Fails at Remove = entity left on playfield; remove manually or reload save.
    // -------------------------------------------------------------------------
    // COMMENTED OUT: Test depends on KnownState.Baseline.SpawnEntityTypeV2 which is not defined
    /*
    [Fact]
    public async Task SpawnEntity_ThenRemove_RoundTrip()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: Spawn by entity type -- response carries EntityId and EntityType.
        var (spawnTopic, spawnPayload) = await mqtt.RequestAsync(
            "V2.Playfield.SpawnEntity",
            $"{{\"EntityType\":\"{KnownState.Baseline.SpawnEntityTypeV2}\"," +
            $"\"Pos\":{KnownState.Baseline.V2SpawnPos}}}",
            timeoutMs: 10000);

        Assert.True(
            spawnTopic.StartsWith($"{KnownState.AppId}/R/V2.Playfield.SpawnEntity/"),
            $"SpawnEntity failed: {spawnTopic} -- {spawnPayload["Error"]?.Value<string>()}");

        var entityId = spawnPayload["EntityId"]!.Value<int>();
        Assert.True(entityId > 0, $"SpawnEntity returned non-positive EntityId: {entityId}");
        Assert.Equal(KnownState.Baseline.SpawnEntityTypeV2, spawnPayload["EntityType"]!.Value<string>());

        // Step 2: Remove the spawned entity.
        // Fails here = entity is on KnownState.Playfield; remove manually or reload save.
        var (removeTopic, removePayload) = await mqtt.RequestAsync(
            "V2.Playfield.RemoveEntity",
            $"{{\"EntityId\":{entityId}}}",
            timeoutMs: 8000);

        Assert.True(
            removeTopic.StartsWith($"{KnownState.AppId}/R/V2.Playfield.RemoveEntity/"),
            $"RemoveEntity failed: {removeTopic} -- {removePayload["Error"]?.Value<string>()}");
        Assert.Equal(entityId, removePayload["EntityId"]!.Value<int>());
    }
    */
}
