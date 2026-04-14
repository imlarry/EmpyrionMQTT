using ESBTests.Infrastructure;
using System.Threading.Tasks;

namespace ESBTests.V2.IPda;

/// <summary>
/// Integration tests for the Pda topic handlers.
///
/// All scenarios are consolidated in one test so that a single skip appears when
/// ModApi.PDA is null (always the case for client mods). The skip decision is
/// driven by the live server response, not a hardcoded flag: if the first request
/// comes back as X with the PDA-unavailable error, the whole test skips.
///
/// See the block comment at the top of ESB/TopicHandlers/V2/Pda.cs for why PDA
/// is never available to general client mods.
/// </summary>
[Trait("Category", "Integration")]
public class Test_Pda_Integration
{
    private const string PdaUnavailableError =
        "PDA interface is not available in this game context";

    private const string PdaUnavailableSkip =
        "IPda is not accessible from client mods -- ModApi.PDA is always null. " +
        "See block comment in ESB/TopicHandlers/V2/Pda.cs.";

    // -------------------------------------------------------------------------
    // All Pda handler scenarios in one fact so one skip appears in output.
    // If PDA becomes available (ESB restructured as scenario script host) each
    // Assert line identifies exactly which scenario failed.
    // -------------------------------------------------------------------------
    [SkippableFact]
    public async Task PdaHandlers_WhenPdaAvailable_AllRespond()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // -- ShowMessage (min payload) -----------------------------------------
        var (smTopic, smPayload) = await mqtt.RequestAsync(
            "V2.Pda.ShowMessage",
            "{\"Message\":\"ESB Pda Integration Test\"}");

        // Preflight: if the server reports PDA unavailable, skip the whole test.
        Skip.If(
            smTopic.Contains("/X/") && (string?)smPayload["Error"] == PdaUnavailableError,
            PdaUnavailableSkip);

        Assert.True(
            smTopic.StartsWith($"{KnownState.AppId}/R/V2.Pda.ShowMessage/"),
            $"ShowMessage (min): unexpected topic {smTopic}");
        Assert.Equal("ESB Pda Integration Test", (string)smPayload["Message"]!);

        // -- ShowMessage (all params) ------------------------------------------
        var (smFullTopic, smFullPayload) = await mqtt.RequestAsync(
            "V2.Pda.ShowMessage",
            "{\"Message\":\"ESB Full Params Test\",\"Duration\":3.0,\"HasPrio\":true,\"CleanupFirst\":false,\"PlayerId\":-1}");

        Assert.True(
            smFullTopic.StartsWith($"{KnownState.AppId}/R/V2.Pda.ShowMessage/"),
            $"ShowMessage (full): unexpected topic {smFullTopic}");
        Assert.Equal("ESB Full Params Test", (string)smFullPayload["Message"]!);

        // -- SetMapMarker (activate) -------------------------------------------
        var (mmTopic, mmPayload) = await mqtt.RequestAsync(
            "V2.Pda.SetMapMarker",
            "{\"Activate\":true,\"Position\":{\"X\":0.0,\"Y\":0.0,\"Z\":0.0},\"MarkerName\":\"ESB Test Marker\",\"Distance\":100}");

        Assert.True(
            mmTopic.StartsWith($"{KnownState.AppId}/R/V2.Pda.SetMapMarker/"),
            $"SetMapMarker: unexpected topic {mmTopic}");
        Assert.Equal("ESB Test Marker", (string)mmPayload["MarkerName"]!);
        Assert.True((bool)mmPayload["Activate"]!);

        // -- GetPoiEntityId ----------------------------------------------------
        var (peiTopic, peiPayload) = await mqtt.RequestAsync(
            "V2.Pda.GetPoiEntityId",
            "{\"PoiName\":\"Akua\"}");

        Assert.True(
            peiTopic.StartsWith($"{KnownState.AppId}/R/V2.Pda.GetPoiEntityId/"),
            $"GetPoiEntityId: unexpected topic {peiTopic}");
        Assert.NotNull(peiPayload["EntityId"]);

        // -- GetPoiLocation ---------------------------------------------------
        var (plTopic, plPayload) = await mqtt.RequestAsync(
            "V2.Pda.GetPoiLocation",
            "{\"PoiName\":\"Akua\"}");

        Assert.True(
            plTopic.StartsWith($"{KnownState.AppId}/R/V2.Pda.GetPoiLocation/"),
            $"GetPoiLocation: unexpected topic {plTopic}");
        Assert.NotNull(plPayload["Position"]);
        Assert.NotNull(plPayload["Position"]!["X"]);
        Assert.NotNull(plPayload["Position"]!["Y"]);
        Assert.NotNull(plPayload["Position"]!["Z"]);
    }
}
