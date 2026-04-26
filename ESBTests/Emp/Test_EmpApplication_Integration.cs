using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.Emp;

/// <summary>
/// Integration tests for the emp/ Application handlers.
/// Requires the game running with the ESB mod loaded.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_EmpApplication_Integration
{
    [Fact]
    public async Task GameTicks_ReturnsPositiveNumber()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "app", "get/GameTicks", "{}");

        Assert.NotNull(payload["GameTicks"]);
        Assert.True(payload["GameTicks"]!.Value<long>() > 0);
    }

    [Fact]
    public async Task Mode_ReturnsModeString()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "app", "get/Mode", "{}");

        Assert.NotNull(payload["Mode"]);
        Assert.False(string.IsNullOrEmpty(payload["Mode"]!.Value<string>()));
    }

    [Fact]
    public async Task State_ReturnsStateString()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "app", "get/State", "{}");

        Assert.NotNull(payload["State"]);
        Assert.False(string.IsNullOrEmpty(payload["State"]!.Value<string>()));
    }

    [Fact]
    public async Task GetAllPlayfields_ContainsAkua()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "app", "get/AllPlayfields", "{}");

        // Handler returns a JSON array; EmpTestClient wraps it under "items"
        var array = payload["items"] as JArray ?? new JArray();
        Assert.NotEmpty(array);
        Assert.Contains(array, t => t["PlayfieldName"]?.Value<string>() == KnownState.Playfield);
    }

    [Fact]
    public async Task GetPathFor_Root_ReturnsPath()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "app", "call/GetPathFor",
            "{\"AppFolder\":\"Root\"}");

        Assert.Equal("Root", payload["AppFolder"]!.Value<string>());
        Assert.False(string.IsNullOrEmpty(payload["Path"]!.Value<string>()));
    }

    [Fact]
    public async Task GetStructure_BaseEntity_ReturnsStructureInfo()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "app", "call/GetStructure",
            $"{{\"EntityId\":{KnownState.BaseEntityId}}}", timeoutMs: 10000);

        Assert.Equal(KnownState.BaseEntityId, payload["Id"]!.Value<int>());
        Assert.NotNull(payload["Name"]);
        Assert.NotNull(payload["Pos"]);
    }
}
