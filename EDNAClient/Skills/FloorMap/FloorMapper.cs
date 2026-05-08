using System.IO;
using EDNAClient.Core;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Skills.FloorMap
{
    // Orchestrates the MQTT sequence that produces a floor map:
    //   1. Player/GetProperties -- get player world position and current structure entity id
    //   2. Structure/GetAllBlocks (if not cached) -- full block data saved to disk
    //   3. Structure/GlobalToStructPos (parallel with step 2) -- player Y in block space
    // On subsequent captures the cached file is used; only GlobalToStructPos is called.
    internal class FloorMapper
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        private IMessenger? _messenger;

        public Task StartAsync(IMessenger messenger)
        {
            _messenger = messenger;
            return Task.CompletedTask;
        }

        public void Stop() => _messenger = null;

        // Returns a rendered FloorMapDocument on success, null on failure.
        // statusCallback receives progress/error text for immediate UI feedback.
        // mapsDir may be null; when provided the result is cached per entity.
        public async Task<FloorMapDocument?> RefreshAsync(
            string solarSystem, string playfield,
            string? mapsDir,
            Action<string> statusCallback)
        {
            if (_messenger == null) throw new InvalidOperationException("FloorMapper not started");
            EdnaLogger.Detail("[FloorMapper] RefreshAsync start");
            try
            {
                var playerJson = await _messenger.RequestAsync(
                    "Player", "GetProperties",
                    "{\"Properties\":[\"Position\",\"CurrentStructureId\",\"CurrentStructureEntityId\"]}",
                    RequestTimeout);

                EdnaLogger.Log($"[FloorMapper] Player/GetProperties response: {playerJson}");
                var pj  = JObject.Parse(playerJson);
                var csi = pj["CurrentStructureId"];
                if (csi == null || csi.Type == JTokenType.Null)
                {
                    statusCallback("Not inside a structure");
                    EdnaLogger.Log("[FloorMapper] aborted: CurrentStructureId is null");
                    return null;
                }

                int entityId = (int)(pj["CurrentStructureEntityId"] ?? 0);
                var pos  = pj["Position"];
                float worldX = pos != null ? (float)(pos["X"] ?? 0) : 0f;
                float worldY = pos != null ? (float)(pos["Y"] ?? 0) : 0f;
                float worldZ = pos != null ? (float)(pos["Z"] ?? 0) : 0f;

                // Fire GlobalToStructPos immediately -- it is fast and runs while we handle blocks.
                var structPosTask = _messenger.RequestAsync(
                    "Structure", "GlobalToStructPos",
                    $"{{\"EntityId\":{entityId},\"Pos\":{{\"X\":{worldX},\"Y\":{worldY},\"Z\":{worldZ}}}}}",
                    RequestTimeout);

                // Resolve cache path when mapsDir is available.
                string? cachePath = null;
                if (mapsDir != null)
                {
                    var cacheDir = Path.Combine(mapsDir, SafeName(solarSystem), SafeName(playfield));
                    cachePath = Path.Combine(cacheDir, $"{entityId}.json");
                }

                FloorMapDocument? doc;
                if (cachePath != null && File.Exists(cachePath))
                {
                    EdnaLogger.Log($"[FloorMapper] loading cached blocks for entity={entityId}");
                    doc = FloorMapDocument.Load(cachePath);
                    if (doc == null)
                    {
                        statusCallback($"Failed to load cached data for entity {entityId}");
                        return null;
                    }
                }
                else
                {
                    statusCallback($"Fetching blocks for entity {entityId}...");
                    EdnaLogger.Log($"[FloorMapper] calling Structure/GetAllBlocks entity={entityId}");
                    var blocksJson = await _messenger.RequestAsync(
                        "Structure", "GetAllBlocks",
                        $"{{\"EntityId\":{entityId}}}", RequestTimeout);

                    EdnaLogger.Log($"[FloorMapper] GetAllBlocks response length={blocksJson.Length}");
                    var blocksObj = JObject.Parse(blocksJson);
                    var blocks    = blocksObj["Blocks"] as JArray ?? new JArray();
                    doc = FloorMapDocument.FromAllBlocks(entityId, playfield, solarSystem, blocks);
                }

                var structPosJson = await structPosTask;
                EdnaLogger.Log($"[FloorMapper] GlobalToStructPos response: {structPosJson}");
                var sp    = JObject.Parse(structPosJson);
                var spos  = sp["StructPos"];
                int structY = spos != null ? (int)(spos["Y"] ?? 0) : 0;
                int playerX = spos != null ? (int)(spos["X"] ?? 0) : 0;
                int playerZ = spos != null ? (int)(spos["Z"] ?? 0) : 0;

                doc.Y       = structY;
                doc.PlayerX = playerX;
                doc.PlayerZ = playerZ;
                doc.Render();

                EdnaLogger.Detail($"[FloorMapper] render done: entity={entityId} Y={structY}");

                if (cachePath != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cachePath) ?? string.Empty);
                    doc.Save(cachePath);
                }

                return doc;
            }
            catch (OperationCanceledException)
            {
                statusCallback("Scan cancelled");
                return null;
            }
            catch (Exception ex)
            {
                statusCallback($"Error: {ex.Message}");
                EdnaLogger.Error("FloorMapper.RefreshAsync failed", ex);
                return null;
            }
        }

        private static string SafeName(string s) =>
            string.Join("_", s.Split(Path.GetInvalidFileNameChars()));
    }
}
