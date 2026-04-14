using ESBTests.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable CS8600, CS8604   // null-state analysis: casts are guarded by explicit != null checks above

namespace ESBTests.V1.IStructure;

/// <summary>
/// Destructive integration tests for the V1 Structure topic handler.
///
/// Structure.Update overwrites server-side structure metadata. The test reads
/// the current name from ListGlobal and writes it back unchanged (net-zero mutation),
/// but the write itself exercises the full path.
///
/// Run with: dotnet test --filter "Category=Integration_Destructive"
/// </summary>
[Trait("Category", "Integration_Destructive")]
public class Test_Structure_Destructive
{
    // -------------------------------------------------------------------------
    // V1.Structure.Update -- read name via ListGlobal, write same name back
    // Net game-state change: zero. Exercises the write path.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Update_KnownBase_NameRoundTrip()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: read current name from the global structure list
        var (listTopic, listPayload) = await mqtt.RequestAsync(
            "V1.Structure.ListGlobal",
            "{}",
            appId: KnownState.V1AppId);

        Assert.True(
            listTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Structure.ListGlobal/"),
            $"ListGlobal failed: {listTopic} -- {listPayload["Error"]?.Value<string>()}");

        var dict = listPayload["Data"]!["globalStructures"] as JObject;
        Assert.NotNull(dict);

        // Find the playfield key that contains the base, and grab all current fields
        string playfieldName = null;
        JObject baseEntry = null;
        foreach (var prop in dict.Properties())
        {
            var arr = prop.Value as JArray;
            if (arr == null) continue;
            var found = arr.OfType<JObject>()
                .FirstOrDefault(e => e["id"] != null && (int)e["id"] == KnownState.BaseEntityId);
            if (found != null)
            {
                playfieldName = prop.Name;
                baseEntry = found;
                break;
            }
        }

        Assert.NotNull(baseEntry);
        Assert.NotNull(playfieldName);

        string originalName        = baseEntry["name"]        != null ? (string)baseEntry["name"]        : "";
        int    originalFactionId   = baseEntry["factionId"]   != null ? (int)baseEntry["factionId"]      : 0;
        int    originalFactionGroup= baseEntry["factionGroup"] != null ? (int)baseEntry["factionGroup"]  : 0;
        bool   originalPowered     = baseEntry["powered"]     != null ? (bool)baseEntry["powered"]       : false;
        int    originalFuel        = baseEntry["fuel"]        != null ? (int)baseEntry["fuel"]           : 0;
        int    originalType        = baseEntry["type"]        != null ? (int)baseEntry["type"]           : 0;

        // Step 2: write all current values back (net-zero mutation; PlayfieldName required for routing)
        var updateBody = new Newtonsoft.Json.Linq.JObject(
            new Newtonsoft.Json.Linq.JProperty("Id",            KnownState.BaseEntityId),
            new Newtonsoft.Json.Linq.JProperty("Name",          originalName),
            new Newtonsoft.Json.Linq.JProperty("PlayfieldName", playfieldName),
            new Newtonsoft.Json.Linq.JProperty("FactionId",     originalFactionId),
            new Newtonsoft.Json.Linq.JProperty("FactionGroup",  originalFactionGroup),
            new Newtonsoft.Json.Linq.JProperty("Powered",       originalPowered),
            new Newtonsoft.Json.Linq.JProperty("Fuel",          originalFuel),
            new Newtonsoft.Json.Linq.JProperty("Type",          originalType));

        var (updateTopic, updatePayload) = await mqtt.RequestAsync(
            "V1.Structure.Update",
            updateBody.ToString(Newtonsoft.Json.Formatting.None),
            appId: KnownState.V1AppId);

        Assert.True(
            updateTopic.StartsWith($"{KnownState.V1AppId}/R/V1.Structure.Update/"),
            $"Update failed: {updateTopic} -- {updatePayload["Error"]?.Value<string>()}");

        Assert.True(updatePayload["Ok"]!.Value<bool>());
    }
}
