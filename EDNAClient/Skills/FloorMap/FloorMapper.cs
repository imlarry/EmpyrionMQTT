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

        private EdnaContext? _ctx;
        private IMessageBus? _bus;

        public Task StartAsync(EdnaContext ctx)
        {
            _ctx = ctx;
            _bus = ctx.Bus;
            return Task.CompletedTask;
        }

        public void Stop() { _bus = null; _ctx = null; }

        // Target rcId for in-game requests: playfield if known, else game, else broadcast.
        private string TargetRcId() =>
            _ctx?.CurrentPlayfieldRcId
            ?? _ctx?.GameRcId
            ?? RoutingContextId.BroadcastValue;

        // Returns a rendered FloorMapDocument on success, null on failure.
        // statusCallback receives progress/error text for immediate UI feedback.
        // mapsDir may be null; when provided the result is cached per entity.
        public async Task<FloorMapDocument?> RefreshAsync(
            string solarSystem, string playfield,
            string? mapsDir,
            Action<string> statusCallback)
        {
            if (_bus == null) throw new InvalidOperationException("FloorMapper not started");
            EdnaLogger.Detail("[FloorMapper] RefreshAsync start");
            try
            {
                var rcId = TargetRcId();
                var playerEnv = await _bus.RequestAsync<object>(
                    rcId, "Player", "GetProperties", new { }, RequestTimeout);

                EdnaLogger.Log($"[FloorMapper] Player/GetProperties response: {playerEnv.RawPayload}");
                var pj = playerEnv.PayloadJson ?? new JObject();
                var cs = pj["CurrentStructure"];
                if (cs == null || cs.Type == JTokenType.Null)
                {
                    statusCallback("Not inside a structure");
                    EdnaLogger.Log("[FloorMapper] aborted: CurrentStructure is null");
                    return null;
                }

                int entityId = cs["EntityId"] != null ? (int)cs["EntityId"] : 0;
                var pos  = pj["Position"];
                float worldX = pos != null ? (float)(pos["X"] ?? 0) : 0f;
                float worldY = pos != null ? (float)(pos["Y"] ?? 0) : 0f;
                float worldZ = pos != null ? (float)(pos["Z"] ?? 0) : 0f;

                // Fire GlobalToStructPos immediately -- it is fast and runs while we handle blocks.
                var structPosTask = _bus.RequestAsync<object>(
                    rcId, "Structure", "GlobalToStructPos",
                    new { EntityId = entityId, Pos = new { X = worldX, Y = worldY, Z = worldZ } },
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
                    var blocksEnv = await _bus.RequestAsync<object>(
                        rcId, "Structure", "GetAllBlocks", new { EntityId = entityId }, RequestTimeout);

                    EdnaLogger.Log($"[FloorMapper] GetAllBlocks response length={blocksEnv.RawPayload.Length}");
                    var blocksObj = JObject.Parse(blocksEnv.RawPayload);
                    var blocks    = blocksObj["Blocks"] as JObject ?? new JObject();
                    doc = FloorMapDocument.FromAllBlocks(entityId, playfield, solarSystem, blocks);
                }

                var structPosEnv  = await structPosTask;
                EdnaLogger.Log($"[FloorMapper] GlobalToStructPos response: {structPosEnv.RawPayload}");
                var sp    = structPosEnv.PayloadJson ?? new JObject();
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
