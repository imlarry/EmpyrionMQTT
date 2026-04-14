using ESBTests.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ESBTests.V1.IPlayer;

/// <summary>
/// Destructive integration tests for the V1 Player topic handler.
///
/// Each test reads the player's current state, performs the mutation, then
/// restores to what was read — so net game-state change is zero on success.
/// If a test fails mid-sequence the player may be left in a modified state.
/// Exit without saving (or use Scripts/Restore-TestSave.ps1) before re-running.
///
/// Run with: dotnet test --filter "Category=Integration_Destructive"
///
/// Prerequisites:
///   KnownState.Baseline.TestPlayfield — a second playfield that exists in the save (set to "Skillon")
///   Player bag must contain at least one item for the AddItem test to run
/// </summary>
[Trait("Category", "Integration_Destructive")]
public class Test_Player_Destructive
{
    // -------------------------------------------------------------------------
    // V1.Player.GetAndRemoveInventory — clears inventory atomically
    // Restore: captured inventory written back via SetInventory
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetAndRemoveInventory_ClearsInventoryAndRestores()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: capture current inventory
        var (getTopic, getPayload) = await mqtt.RequestAsync(
            "V1.Player.GetInventory",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            getTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInventory/"),
            $"GetInventory failed: {getTopic} — {getPayload["Error"]?.Value<string>()}");

        var inv = getPayload["Data"] as JObject;
        Assert.NotNull(inv);
        var toolbelt = inv["toolbelt"] as JArray ?? new JArray();
        var bag      = inv["bag"]      as JArray ?? new JArray();

        // Step 2: clear inventory — fails here = nothing mutated yet
        var (removeTopic, removePayload) = await mqtt.RequestAsync(
            "V1.Player.GetAndRemoveInventory",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            removeTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetAndRemoveInventory/"),
            $"GetAndRemoveInventory failed: {removeTopic} — {removePayload["Error"]?.Value<string>()}");

        Assert.NotNull(removePayload["Data"] as JObject);

        // Step 3: restore — fails here = inventory is cleared, restore from save
        var restoreJson = $"{{\"PlayerId\":{KnownState.PlayerEntityId}," +
            $"\"Toolbelt\":{toolbelt.ToString(Formatting.None)}," +
            $"\"Bag\":{bag.ToString(Formatting.None)}}}";

        var (restoreTopic, restorePayload) = await mqtt.RequestAsync(
            "V1.Player.SetInventory", restoreJson,
            appId: KnownState.V1AppId);

        Assert.True(
            restoreTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.SetInventory/"),
            $"SetInventory restore failed: {restoreTopic} — {restorePayload["Error"]?.Value<string>()}");
    }

    // -------------------------------------------------------------------------
    // V1.Player.SetInventory — replaces inventory entirely
    // Restore: original inventory written back after test
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SetInventory_ReplacesAndRestores()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: capture current inventory
        var (getTopic, getPayload) = await mqtt.RequestAsync(
            "V1.Player.GetInventory",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            getTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInventory/"),
            $"GetInventory failed: {getTopic} — {getPayload["Error"]?.Value<string>()}");

        var inv = getPayload["Data"] as JObject;
        Assert.NotNull(inv);
        var toolbelt = inv["toolbelt"] as JArray ?? new JArray();
        var bag      = inv["bag"]      as JArray ?? new JArray();

        // Step 2: set empty inventory as the test mutation
        var (setTopic, setPayload) = await mqtt.RequestAsync(
            "V1.Player.SetInventory",
            $"{{\"PlayerId\":{KnownState.PlayerEntityId},\"Toolbelt\":[],\"Bag\":[]}}",
            appId: KnownState.V1AppId);

        Assert.True(
            setTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.SetInventory/"),
            $"SetInventory (empty) failed: {setTopic} — {setPayload["Error"]?.Value<string>()}");

        // Step 3: restore — fails here = inventory is cleared, restore from save
        var restoreJson = $"{{\"PlayerId\":{KnownState.PlayerEntityId}," +
            $"\"Toolbelt\":{toolbelt.ToString(Formatting.None)}," +
            $"\"Bag\":{bag.ToString(Formatting.None)}}}";

        var (restoreTopic, restorePayload) = await mqtt.RequestAsync(
            "V1.Player.SetInventory", restoreJson,
            appId: KnownState.V1AppId);

        Assert.True(
            restoreTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.SetInventory/"),
            $"SetInventory restore failed: {restoreTopic} — {restorePayload["Error"]?.Value<string>()}");
    }

    // -------------------------------------------------------------------------
    // V1.Player.AddItem — adds one item to the player's bag
    // Restore: original inventory written back via SetInventory
    // Uses the first item found in the pre-test bag — no hardcoded item ID needed.
    // The bag must contain at least one item for this test to run.
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task AddItem_AddsItemAndRestores()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: capture inventory BEFORE AddItem (diagnostic + precondition)
        var (getTopic, getPayload) = await mqtt.RequestAsync(
            "V1.Player.GetInventory",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            getTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInventory/"),
            $"GetInventory (before) failed: {getTopic} -- {getPayload["Error"]?.Value<string>()}");

        var inv = getPayload["Data"] as JObject;
        Assert.NotNull(inv);
        var toolbelt = inv["toolbelt"] as JArray ?? new JArray();
        var bag      = inv["bag"]      as JArray ?? new JArray();

        // Pick the bag item with the lowest count — most likely to have stack room.
        var testItem = bag.OfType<JObject>()
            .OrderBy(t => (int?)t["count"] ?? int.MaxValue)
            .FirstOrDefault();
        Skip.If(testItem == null,
            "Player bag is empty -- place at least one item in the player bag " +
            "(not armor or escape pod) before running the AddItem destructive test.");
        int testItemId = (int)testItem!["id"]!;

        // Step 2: add one of that item (the call under test)
        var (addTopic, addPayload) = await mqtt.RequestAsync(
            "V1.Player.AddItem",
            $"{{\"EntityId\":{KnownState.PlayerEntityId},\"ItemId\":{testItemId},\"Count\":1}}",
            appId: KnownState.V1AppId);

        Assert.True(
            addTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.AddItem/"),
            $"AddItem failed: {addTopic} -- {addPayload["Error"]?.Value<string>()}");

        Assert.True((bool?)addPayload["Ok"] == true);

        // Step 3: capture inventory AFTER AddItem (diagnostic)
        var (afterTopic, afterPayload) = await mqtt.RequestAsync(
            "V1.Player.GetInventory",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            afterTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInventory/"),
            $"GetInventory (after) failed: {afterTopic} -- {afterPayload["Error"]?.Value<string>()}");

        var invAfter = afterPayload["Data"] as JObject;
        Assert.NotNull(invAfter);
        // Attach the after-inventory to any subsequent failure so it is visible in the log.
        string afterSnapshot = invAfter!.ToString(Formatting.None);

        // Step 4: restore — fails here = extra item in bag, restore from save
        var restoreJson = $"{{\"PlayerId\":{KnownState.PlayerEntityId}," +
            $"\"Toolbelt\":{toolbelt.ToString(Formatting.None)}," +
            $"\"Bag\":{bag.ToString(Formatting.None)}}}";

        var (restoreTopic, restorePayload) = await mqtt.RequestAsync(
            "V1.Player.SetInventory", restoreJson,
            appId: KnownState.V1AppId);

        Assert.True(
            restoreTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.SetInventory/"),
            $"SetInventory restore failed: {restoreTopic} -- inventory after AddItem was: {afterSnapshot}");
    }

    // -------------------------------------------------------------------------
    // V1.Player.SetInfo — patches player health
    // Restore: original health written back via SetInfo
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SetInfo_PatchesHealthAndRestores()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: read current PlayerInfo
        var (infoTopic, infoPayload) = await mqtt.RequestAsync(
            "V1.Player.GetInfo",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            infoTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.GetInfo/"),
            $"GetInfo failed: {infoTopic} — {infoPayload["Error"]?.Value<string>()}");

        var data = infoPayload["Data"] as JObject;
        Assert.NotNull(data);
        int originalHealth = data["health"]!.Value<int>();

        // Step 2: patch health to a different value (avoid underflow)
        int testHealth = originalHealth == 100 ? 90 : 100;

        var (setTopic, setPayload) = await mqtt.RequestAsync(
            "V1.Player.SetInfo",
            $"{{\"EntityId\":{KnownState.PlayerEntityId},\"Health\":{testHealth}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            setTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.SetInfo/"),
            $"SetInfo failed: {setTopic} — {setPayload["Error"]?.Value<string>()}");

        // Step 3: restore — fails here = player health is at testHealth, restore from save
        var (restoreTopic, restorePayload) = await mqtt.RequestAsync(
            "V1.Player.SetInfo",
            $"{{\"EntityId\":{KnownState.PlayerEntityId},\"Health\":{originalHealth}}}",
            appId: KnownState.V1AppId);

        Assert.True(
            restoreTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.SetInfo/"),
            $"SetInfo restore failed: {restoreTopic} — {restorePayload["Error"]?.Value<string>()}");
    }

    // -------------------------------------------------------------------------
    // V1.Player.ChangePlayfield — teleports player to TestPlayfield then back
    // Restore: second ChangePlayfield returns player to Akua at PlayerSpawnPos
    // Requires: KnownState.Baseline.TestPlayfield is set to a valid playfield name
    // Note: the game places the player at terrain height — the Y coord is advisory.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ChangePlayfield_TeleportsAndReturns()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 0: pre-warm Skillon so the playfield server is running before the player
        // arrives — without this the player spawns into a loading playfield and falls
        // through the terrain. X/ (PlayfieldAlreadyLoaded) is acceptable here.
        await mqtt.RequestAsync(
            "V1.Playfield.Load",
            $"{{\"Playfield\":\"{KnownState.Baseline.TestPlayfield}\"}}",
            timeoutMs: 5000,
            appId: KnownState.V1AppId);

        // Give the playfield server time to come online before the player arrives.
        await Task.Delay(8000);

        // Step 1: teleport to test playfield
        var (goTopic, goPayload) = await mqtt.RequestAsync(
            "V1.Player.ChangePlayfield",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}," +
            $"\"Playfield\":\"{KnownState.Baseline.TestPlayfield}\"," +
            $"\"Pos\":{KnownState.Baseline.TestPlayfieldSpawnPos}," +
            $"\"Rot\":{KnownState.Baseline.TestPlayfieldSpawnRot}}}",
            timeoutMs: 10000,
            appId: KnownState.V1AppId);

        Assert.True(
            goTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.ChangePlayfield/"),
            $"ChangePlayfield (go) failed: {goTopic} — {goPayload["Error"]?.Value<string>()}");

        // Wait for the client to finish loading the destination playfield before
        // sending the return command — R/ arrives when the server queues the transfer,
        // not when the player lands. Sending the return too early drops it.
        await Task.Delay(8000);

        // Extra observation window — time to check where you landed on the ground
        // before the return teleport fires. Adjust TestPlayfieldSpawnPos.Y in KnownState
        // if you're spawning underground or too high.
        await Task.Delay(5000);

        // Step 2: teleport back to Akua at spawn point
        var (returnTopic, returnPayload) = await mqtt.RequestAsync(
            "V1.Player.ChangePlayfield",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}," +
            $"\"Playfield\":\"{KnownState.Playfield}\"," +
            $"\"Pos\":{KnownState.PlayerSpawnPosV1}," +
            $"\"Rot\":{KnownState.PlayerSpawnRotV1}}}",
            timeoutMs: 10000,
            appId: KnownState.V1AppId);

        Assert.True(
            returnTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.ChangePlayfield/"),
            $"ChangePlayfield (return) failed: {returnTopic} — {returnPayload["Error"]?.Value<string>()}");
    }

    // -------------------------------------------------------------------------
    // V1.Player.ItemExchange — shows an item-exchange dialog to the player
    // Observed behaviour in co-op/dedicated mode: Request_Player_ItemExchange
    // returns immediately when the client acknowledges the dialog, not when the
    // player clicks. The result is an empty ItemExchangeInfo (no submitted items).
    // An empty Items array does not trigger the dialog — at least one item is required.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ItemExchange_ShowsDialogAndReturns()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Offer one Promethium Fuel Pack (id=4314) — required for the dialog to appear.
        var (topic, payload) = await mqtt.RequestAsync(
            "V1.Player.ItemExchange",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}," +
            $"\"Title\":\"ESB Test\"," +
            $"\"Desc\":\"Click OK to continue.\"," +
            $"\"ButtonText\":\"OK\"," +
            $"\"Items\":[{{\"id\":4314,\"count\":1,\"slotIdx\":15,\"ammo\":0,\"decay\":0}}]}}",
            appId: KnownState.V1AppId);

        Assert.True(
            topic.StartsWith($"{KnownState.V1AppId}/R/V1.Player.ItemExchange/"),
            $"ItemExchange failed: {topic} — {payload["Error"]?.Value<string>()}");

        Assert.NotNull(payload["Data"] as JObject);
    }
}
