using ESBTests.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// <summary>
/// Integration tests for ESB/ dot-suffix introspection.
/// Requires the game running with the ESB mod loaded.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Introspection_Integration
{
    // -------------------------------------------------------------------------
    // App/GetPathFor.Describe
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetPathFor_Describe_ReturnsStructuredMetadata()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "GetPathFor.Describe", "{}");

        Assert.Equal("GetPathFor", payload["Operation"]!.Value<string>());
        Assert.Equal("App",        payload["Scope"]!.Value<string>());
        Assert.NotNull(payload["Summary"]);
        Assert.NotEmpty((payload["Input"]  as JArray)!);
        Assert.NotEmpty((payload["Output"] as JArray)!);
        var suffixes = payload["Suffixes"] as JArray;
        Assert.NotNull(suffixes);
        Assert.Contains(suffixes, t => t.Value<string>() == "Describe");
    }

    // -------------------------------------------------------------------------
    // App/GetPathFor.Unknown -- unknown meta-operation returns error
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetPathFor_UnknownSuffix_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "GetPathFor.Unknown", "{}");

        Assert.NotNull(payload["Error"]);
        Assert.Contains("Unknown", payload["Error"]!.Value<string>() ?? "");
    }

    // -------------------------------------------------------------------------
    // App/Describe -- scope manifest lists all operations
    // -------------------------------------------------------------------------
    [Fact]
    public async Task AppDescribe_ReturnsScopeManifest()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var connId  = await mqtt.FindConnectionAsync("Client");
        var payload = await mqtt.RequestAsync(connId, "Client", "App", "Describe", "{}");

        Assert.Equal("App", payload["Scope"]!.Value<string>());
        var ops = payload["Operations"] as JArray;
        Assert.NotNull(ops);
        Assert.True(ops!.Count >= 15, $"Expected at least 15 operations, got {ops.Count}");
        Assert.Contains(ops, t => t["Operation"]?.Value<string>() == "GetPathFor");
        Assert.Contains(ops, t => t["Operation"]?.Value<string>() == "Describe");
    }
}
