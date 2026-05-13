using ESBTests.Infrastructure;
using System.Threading.Tasks;

namespace ESBTests.TopicHandlerTests;

/// <summary>
/// Integration tests for ESB/ dot-suffix introspection behavior.
/// Requires the game running with the ESB mod loaded.
/// Run with: dotnet test --filter "Category=Integration"
///
/// App scope: introspection endpoints removed -- typed handlers have no meta-operation path.
/// Player and Structure scopes: introspection tests remain until those scopes are converted.
/// </summary>
[Trait("Category", "Integration")]
public class Test_Introspection_Integration
{
    // -------------------------------------------------------------------------
    // App/GetPathFor.Unknown -- dot-suffix on a typed handler
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPathFor_UnknownSuffix_ReturnsError()
    {
        await using var mqtt = await SBTestClient.ConnectAsync();
        var payload = await mqtt.RequestAsync("App", "GetPathFor.Unknown", "{}");

        // Typed handler receives the dot-suffix request as a plain GetPathFor call;
        // it returns an error because the payload lacks a valid AppFolder.
        Assert.NotNull(payload["Error"]);
    }
}
