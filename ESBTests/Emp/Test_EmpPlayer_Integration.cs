using ESBTests.Infrastructure;
using System.Threading.Tasks;

namespace ESBTests.Emp;

/// <summary>
/// Integration tests for the emp/ Player handlers.
/// Requires the game running in client mode with an active local player.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_EmpPlayer_Integration
{
    [Fact]
    public async Task GetProperties_All_ReturnsPlayerData()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "player", "get", "{}");

        Assert.NotNull(payload["Id"]);
        Assert.NotNull(payload["Health"]);
        Assert.NotNull(payload["Position"]);
    }

    [Fact]
    public async Task GetProperties_Selected_ReturnsOnlyRequested()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "player", "get",
            "{\"Properties\":[\"Health\",\"Credits\"]}");

        Assert.NotNull(payload["Health"]);
        Assert.NotNull(payload["Credits"]);
        Assert.Null(payload["Name"]);
    }

    [Fact]
    public async Task GetProperties_InvalidProperty_ReturnsError()
    {
        await using var mqtt = await EmpTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("client");
        var payload = await mqtt.RequestAsync(connId, "client", "player", "get",
            "{\"Properties\":[\"NotARealProperty\"]}");

        Assert.NotNull(payload["Error"]);
        Assert.NotNull(payload["InvalidProperties"]);
        Assert.NotNull(payload["ValidProperties"]);
    }
}
