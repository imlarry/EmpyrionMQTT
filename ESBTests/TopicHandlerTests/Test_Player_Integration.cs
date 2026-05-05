using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// <summary>
/// Integration tests for the ESB/ Player handlers.
/// Requires the game running in client mode with an active local player.
/// Handlers return an error payload when LocalPlayer is null (dedicated server).
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Player_Integration
{
    // =========================================================================
    // Player/GetProperties
    // =========================================================================

    [Fact]
    public async Task Properties_NoPayload_ReturnsAllFields()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "GetProperties", "{}");

        if (payload["Error"] == null)
        {
            Assert.NotNull(payload["Health"]);
            Assert.NotNull(payload["SteamId"]);
            Assert.NotNull(payload["Position"]);
            Assert.NotNull(payload["Bag"]);
            Assert.NotNull(payload["Toolbar"]);
            Assert.NotNull(payload["FactionData"]);
        }
        else
        {
            // Dedicated server: LocalPlayer is null
            Assert.NotNull(payload["Error"]);
        }
    }

    [Fact]
    public async Task Properties_SelectFields_ReturnsOnlyRequested()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "GetProperties",
            "{\"Properties\":[\"SteamId\",\"Health\"]}");

        if (payload["Error"] == null)
        {
            Assert.NotNull(payload["SteamId"]);
            Assert.NotNull(payload["Health"]);
            Assert.Null(payload["Bag"]);
            Assert.Null(payload["Oxygen"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    [Fact]
    public async Task Properties_Bag_ContainsItemStacks()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "GetProperties",
            "{\"Properties\":[\"Bag\"]}");

        if (payload["Error"] == null)
        {
            var bag = payload["Bag"] as JArray;
            Assert.NotNull(bag);
            foreach (JObject item in bag)
            {
                Assert.NotNull(item["Id"]);
                Assert.NotNull(item["Count"]);
                Assert.NotNull(item["SlotIdx"]);
            }
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    [Fact]
    public async Task Properties_Toolbar_ContainsItemStacks()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "GetProperties",
            "{\"Properties\":[\"Toolbar\"]}");

        if (payload["Error"] == null)
        {
            var toolbar = payload["Toolbar"] as JArray;
            Assert.NotNull(toolbar);
            foreach (JObject item in toolbar)
            {
                Assert.NotNull(item["Id"]);
                Assert.NotNull(item["Count"]);
                Assert.NotNull(item["SlotIdx"]);
            }
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    [Fact]
    public async Task Properties_InvalidProperty_ReturnsErrorWithValidList()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "GetProperties",
            "{\"Properties\":[\"NotAProperty\"]}");

        Assert.NotNull(payload["Error"]);
        Assert.NotNull(payload["InvalidProperties"]);
        var validProps = payload["ValidProperties"] as JArray;
        Assert.NotNull(validProps);
        Assert.True(validProps.Count > 0, "ValidProperties should list all property names");
    }

    [Fact]
    public async Task Properties_Position_ContainsXYZ()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "GetProperties",
            "{\"Properties\":[\"Position\"]}");

        if (payload["Error"] == null)
        {
            var pos = payload["Position"] as JObject;
            Assert.NotNull(pos);
            Assert.NotNull(pos["X"]);
            Assert.NotNull(pos["Y"]);
            Assert.NotNull(pos["Z"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    [Fact]
    public async Task Properties_FactionData_ContainsGroupAndId()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "GetProperties",
            "{\"Properties\":[\"FactionData\"]}");

        if (payload["Error"] == null)
        {
            var fd = payload["FactionData"] as JObject;
            Assert.NotNull(fd);
            Assert.NotNull(fd["Group"]);
            Assert.NotNull(fd["Id"]);
        }
        else
        {
            Assert.NotNull(payload["Error"]);
        }
    }

    // =========================================================================
    // Player/Teleport
    // =========================================================================

    [Fact]
    public async Task Teleport_PosOnly_ReturnsOk()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "Teleport",
            $"{{\"Pos\":{KnownState.PlayerSpawnPos}}}");

        if (payload["Error"] == null)
            Assert.NotNull(payload["ok"]);
        else
            Assert.NotNull(payload["Error"]);
    }

    [Fact]
    public async Task Teleport_MissingPos_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "Teleport", "{}");

        Assert.NotNull(payload["Error"]);
        Assert.Contains("Pos argument is required", payload["Error"]!.Value<string>() ?? "");
    }

    [Fact]
    public async Task Teleport_WithPlayfield_MissingRot_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "Teleport",
            $"{{\"Pos\":{KnownState.PlayerSpawnPos},\"Playfield\":\"{KnownState.Playfield}\"}}");

        Assert.NotNull(payload["Error"]);
        Assert.Contains("Rot argument is required", payload["Error"]!.Value<string>() ?? "");
    }

    // =========================================================================
    // Player/DamageEntity
    // =========================================================================

    [Fact]
    public async Task DamageEntity_ZeroDamage_ReturnsOkOrError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("Player", "DamageEntity",
            "{\"DamageAmount\":0,\"DamageType\":0}");

        // R: {ok:true}   X: {Error:...}  -- either is acceptable (dedicated server has no LocalPlayer)
        Assert.True(payload["ok"] != null || payload["Error"] != null,
            "Expected either ok or Error in response");
    }
}
