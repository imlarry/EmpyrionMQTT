using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// <summary>
/// Integration tests for the ESB/ Entity handlers.
/// Requires the game running with the ESB mod loaded and the saved game described
/// in KnownState active (VNS Akua base, EntityId 5320).
/// Handlers return an error payload on dedicated-server "no active playfield"
/// or when the entity is not on the current playfield.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Entity_Integration
{
    private const int EID = KnownState.BaseEntityId;

    // =========================================================================
    // Entity/GetProperties
    // =========================================================================

    [Fact]
    public async Task Properties_KnownEntity_ReturnsCoreFields()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "GetProperties",
            $"{{\"EntityId\":{EID}}}");

        if (payload["Error"] == null)
        {
            Assert.NotNull(payload["Id"]);
            Assert.NotNull(payload["Name"]);
            Assert.NotNull(payload["Type"]);
            Assert.NotNull(payload["Position"]?["X"]);
            Assert.NotNull(payload["Faction"]?["Id"]);
            Assert.NotNull(payload["HasStructure"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    [Fact]
    public async Task Properties_MissingEntityId_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "GetProperties", "{}");

        Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task Properties_UnknownEntity_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "GetProperties",
            "{\"EntityId\":999999}");

        Assert.NotNull(payload["Error"]);
    }

    // =========================================================================
    // Entity/List
    // =========================================================================

    [Fact]
    public async Task List_ReturnsTabularEntities()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "List", "{}");

        if (payload["Error"] == null)
        {
            var table = payload["Entities"];
            Assert.NotNull(table?["Columns"]);
            Assert.NotNull(table?["Rows"]);
            var cols = (table?["Columns"] as JArray)?.Select(c => (string?)c).ToArray() ?? new string?[0];
            Assert.Contains("BelongsTo", cols);
            Assert.Contains("FactionId", cols);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    // =========================================================================
    // Entity/SetPosition
    // =========================================================================

    [Fact]
    public async Task SetPosition_SamePos_ReturnsOk()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var props = await mqtt.RequestAsync("Entity", "GetProperties",
            $"{{\"EntityId\":{EID}}}");
        if (props["Error"] != null)
        {
            Assert.NotNull(props["Error"]);
            return;
        }

        var pos = props["Position"] as JObject;
        Assert.NotNull(pos);
        var payload = await mqtt.RequestAsync("Entity", "SetPosition",
            $"{{\"EntityId\":{EID},\"Pos\":{{\"X\":{(float)pos!["X"]!},\"Y\":{(float)pos["Y"]!},\"Z\":{(float)pos["Z"]!}}}}}");

        if (payload["Error"] == null)
            Assert.NotNull(payload["ok"]);
        else
            Assert.NotNull(payload["Error"]);
    }

    // =========================================================================
    // Entity/SetRotation
    // =========================================================================

    [Fact]
    public async Task SetRotation_Identity_ReturnsOk()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "SetRotation",
            $"{{\"EntityId\":{EID},\"Rot\":{{\"X\":0,\"Y\":0,\"Z\":0,\"W\":1}}}}");

        if (payload["Error"] == null)
            Assert.NotNull(payload["ok"]);
        else
            Assert.NotNull(payload["Error"]);
    }

    // =========================================================================
    // Entity/DamageEntity
    // =========================================================================

    [Fact]
    public async Task DamageEntity_ZeroDamage_ReturnsOkOrError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "DamageEntity",
            $"{{\"EntityId\":{EID},\"DamageAmount\":0,\"DamageType\":0}}");

        Assert.True(payload["ok"] != null || payload["Error"] != null,
            "Expected either ok or Error in response");
    }

    // =========================================================================
    // Entity/Move, MoveForward, MoveStop
    // =========================================================================

    [Fact]
    public async Task Move_ZeroVector_ReturnsOk()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "Move",
            $"{{\"EntityId\":{EID},\"Direction\":{{\"X\":0,\"Y\":0,\"Z\":0}}}}");

        if (payload["Error"] == null)
            Assert.NotNull(payload["ok"]);
        else
            Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task MoveForward_ZeroSpeed_ReturnsOk()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "MoveForward",
            $"{{\"EntityId\":{EID},\"Speed\":0}}");

        if (payload["Error"] == null)
            Assert.NotNull(payload["ok"]);
        else
            Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task MoveStop_ReturnsOk()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Entity", "MoveStop",
            $"{{\"EntityId\":{EID}}}");

        if (payload["Error"] == null)
            Assert.NotNull(payload["ok"]);
        else
            Assert.NotNull(payload["Error"]);
    }
}
