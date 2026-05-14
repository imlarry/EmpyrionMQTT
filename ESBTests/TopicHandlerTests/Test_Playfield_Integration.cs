using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// Integration tests for the Playfield-scope handlers (PlayfieldHandler.cs).
/// Requires the game running with the ESB mod loaded.
/// Run with: dotnet test --filter "Category=Integration"
[Trait("Category", "Integration")]
public class Test_Playfield_Integration
{
    // -------------------------------------------------------------------------
    // Playfield/GetProperties -- no input, all scalar fields + Players/Entities
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetProperties_ReturnsAllScalarFields()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetProperties", "{}");

        Assert.Null(payload["Error"]);
        Assert.False(string.IsNullOrEmpty((string)payload["Name"]));
        Assert.False(string.IsNullOrEmpty((string)payload["PlayfieldType"]));
        Assert.NotNull(payload["PlanetType"]);
        Assert.NotNull(payload["PlanetClass"]);
        Assert.NotNull(payload["SolarSystemName"]);
        Assert.NotNull(payload["SolarSystemCoordinates"]);
        Assert.NotNull(payload["IsPvP"]);
    }

    [Fact]
    public async Task GetProperties_IncludesPlayersAndEntitiesArrays()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetProperties", "{}");

        Assert.NotNull(payload["Players"]?["Columns"]);
        Assert.NotNull(payload["Players"]?["Rows"]);
        Assert.NotNull(payload["Entities"]?["Columns"]);
        Assert.NotNull(payload["Entities"]?["Rows"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/Name -- no input, returns string
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Name_ReturnsKnownPlayfield()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "Name", "{}");

        Assert.Equal(KnownState.Playfield, (string)payload["Name"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/PlayfieldType -- no input, returns string
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PlayfieldType_ReturnsString()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "PlayfieldType", "{}");

        Assert.False(string.IsNullOrEmpty((string)payload["PlayfieldType"]));
    }

    // -------------------------------------------------------------------------
    // Playfield/PlanetType -- no input, returns string
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PlanetType_ReturnsString()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "PlanetType", "{}");

        Assert.NotNull(payload["PlanetType"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/PlanetClass -- no input, returns string
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PlanetClass_ReturnsString()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "PlanetClass", "{}");

        Assert.NotNull(payload["PlanetClass"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/SolarSystemName -- no input, returns string
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SolarSystemName_ReturnsString()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "SolarSystemName", "{}");

        Assert.NotNull(payload["SolarSystemName"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/SolarSystemCoordinates -- no input, returns VectorInt3 {X,Y,Z}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SolarSystemCoordinates_ReturnsXYZ()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "SolarSystemCoordinates", "{}");

        var coords = payload["SolarSystemCoordinates"];
        Assert.NotNull(coords);
        Assert.NotNull(coords["X"]);
        Assert.NotNull(coords["Y"]);
        Assert.NotNull(coords["Z"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/IsPvP -- no input, returns bool
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IsPvP_ReturnsBool()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "IsPvP", "{}");

        Assert.NotNull(payload["IsPvP"]);
        Assert.IsType<JValue>(payload["IsPvP"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/GetEntities -- no input, returns Entities array
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEntities_ReturnsArray()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetEntities", "{}");

        Assert.NotNull(payload["Entities"]?["Columns"]);
        Assert.NotNull(payload["Entities"]?["Rows"]);
    }

    [Fact]
    public async Task GetEntities_ItemsHaveExpectedFields()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetEntities", "{}");

        var rows = TabularJson.Rows(payload["Entities"]).ToList();
        Assert.NotEmpty(rows);
        var first = rows[0];
        Assert.NotNull(first["EntityId"]);
        Assert.NotNull(first["Name"]);
        Assert.NotNull(first["EntityType"]);
        Assert.NotNull(first["Position"]);
        Assert.NotNull(first["IsProxy"]);
    }

    [Fact]
    public async Task GetEntities_ContainsKnownBase()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetEntities", "{}");

        Assert.Contains(TabularJson.Rows(payload["Entities"]),
            t => (int)t["EntityId"]! == KnownState.BaseEntityId);
    }

    // -------------------------------------------------------------------------
    // Playfield/GetPlayers -- no input, returns Players array
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPlayers_ReturnsArray()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetPlayers", "{}");

        Assert.NotNull(payload["Players"]?["Columns"]);
        Assert.NotNull(payload["Players"]?["Rows"]);
    }

    [Fact]
    public async Task GetPlayers_ItemsHaveExpectedFields()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetPlayers", "{}");

        var rows = TabularJson.Rows(payload["Players"]).ToList();
        Assert.NotEmpty(rows);
        var first = rows[0];
        Assert.NotNull(first["EntityId"]);
        Assert.NotNull(first["Name"]);
        Assert.NotNull(first["SteamId"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/GetTerrainHeight -- { "X": float, "Z": float }
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTerrainHeight_KnownPosition_ReturnsHeight()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetTerrainHeight",
            "{\"X\":-156.5,\"Z\":50.5}");

        Assert.Null(payload["Error"]);
        Assert.NotNull(payload["Height"]);
        Assert.Equal(-156.5f, (float)payload["X"], 3);
        Assert.Equal(50.5f,   (float)payload["Z"], 3);
    }

    [Fact]
    public async Task GetTerrainHeight_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "GetTerrainHeight", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/IsStructureDeviceLocked -- { "StructureId": int, "PosInStruct": {X,Y,Z} }
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IsStructureDeviceLocked_KnownBlock_ReturnsIsLocked()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "IsStructureDeviceLocked",
            $"{{\"StructureId\":{KnownState.BaseEntityId},\"PosInStruct\":{KnownState.FridgeBlock}}}");

        Assert.Null(payload["Error"]);
        Assert.NotNull(payload["IsLocked"]);
    }

    [Fact]
    public async Task IsStructureDeviceLocked_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "IsStructureDeviceLocked", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/LockStructureDevice -- async callback; { "StructureId": int, "PosInStruct": {X,Y,Z}, "DoLock": bool }
    // -------------------------------------------------------------------------

    // IPlayfield.LockStructureDevice returns queued=false in both SP and coop MP test
    // environments against a verified fridge that is not currently locked, even when the
    // call is marshaled to the Unity main thread. Read-side coverage exists in the
    // IsStructureDeviceLocked tests above. Re-enable once a working usage example is found.
    [Fact(Skip = "LockStructureDevice returns queued=false in test environments; cause unknown")]
    public async Task LockStructureDevice_KnownBlock_CallbackFires()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();

        var payload = await mqtt.RequestAsync("Playfield", "LockStructureDevice",
            $"{{\"StructureId\":{KnownState.BaseEntityId},\"PosInStruct\":{KnownState.FridgeBlock},\"DoLock\":true}}",
            timeoutMs: 10000);

        Assert.Null(payload["Error"]);
        Assert.Equal(KnownState.BaseEntityId, (int)payload["StructureId"]);
        Assert.NotNull(payload["PosInStruct"]);
        Assert.NotNull(payload["Success"]);

        // Release the lock regardless of success to avoid leaving state dirty.
        await mqtt.RequestAsync("Playfield", "LockStructureDevice",
            $"{{\"StructureId\":{KnownState.BaseEntityId},\"PosInStruct\":{KnownState.FridgeBlock},\"DoLock\":false}}",
            timeoutMs: 10000);
    }

    [Fact]
    public async Task LockStructureDevice_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "LockStructureDevice", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/SpawnTestPlayer + RemoveTestPlayer -- safe test-only operations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SpawnAndRemoveTestPlayer_CompletesSuccessfully()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();

        var spawnPayload = await mqtt.RequestAsync("Playfield", "SpawnTestPlayer",
            $"{{\"Pos\":{KnownState.PlayerSpawnPos}}}");

        Assert.Null(spawnPayload["Error"]);
        int entityId = (int)spawnPayload["EntityId"];
        Assert.True(entityId > 0);

        var removePayload = await mqtt.RequestAsync("Playfield", "RemoveTestPlayer",
            $"{{\"EntityId\":{entityId}}}");

        Assert.Null(removePayload["Error"]);
        Assert.True((bool)removePayload["ok"]);
    }

    [Fact]
    public async Task SpawnTestPlayer_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "SpawnTestPlayer", "{}");

        Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task RemoveTestPlayer_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "RemoveTestPlayer", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/SpawnEntity -- input validation only; success case is destructive
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SpawnEntity_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "SpawnEntity", "{}");

        Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task SpawnEntity_MissingPos_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "SpawnEntity",
            $"{{\"EntityType\":\"{KnownState.Baseline.SpawnEntityTypeV2}\"}}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/SpawnPrefab -- input validation only; no known stable prefab name
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SpawnPrefab_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "SpawnPrefab", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/RemoveEntity -- input validation only; success case requires a spawned entity
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveEntity_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "RemoveEntity", "{}");

        Assert.NotNull(payload["Error"]);
    }

    // -------------------------------------------------------------------------
    // Playfield/AddVoxelArea, MoveVoxelArea, RemoveVoxelArea -- input validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddVoxelArea_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "AddVoxelArea", "{}");

        Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task MoveVoxelArea_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "MoveVoxelArea", "{}");

        Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task RemoveVoxelArea_EmptyPayload_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Playfield", "RemoveVoxelArea", "{}");

        Assert.NotNull(payload["Error"]);
    }
}
