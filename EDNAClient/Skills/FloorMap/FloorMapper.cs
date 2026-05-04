using EDNAClient.Core;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Skills.FloorMap
{
    // Orchestrates the three-step MQTT sequence that produces a floor map:
    //   1. Player/GetProperties -- get player world position, current structure, and playfield
    //   2. Structure/GlobalToStructPos -- convert world pos -> struct block pos (gives Y)
    //   3. Structure/ScanFloor x2 -- scan Y (walls) and Y-1 (floor) in parallel
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

        // Returns a FloorMapDocument on success, null on failure (e.g. not in a structure).
        // statusCallback receives progress/error text for immediate UI feedback.
        public async Task<FloorMapDocument?> RefreshAsync(
            string solarSystem, string playfield,
            Action<string> statusCallback)
        {
            if (_messenger == null) throw new InvalidOperationException("FloorMapper not started");
            EdnaLogger.Detail("[FloorMapper] RefreshAsync start");
            try
            {
                EdnaLogger.Log($"[FloorMapper] querying Player/GetProperties (playfield={playfield} solarSystem={solarSystem})");
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

                var cseToken = pj["CurrentStructureEntityId"];
                int entityId = cseToken != null ? (int)cseToken : 0;
                var pos  = pj["Position"];
                var posX = pos?["X"];  var posY = pos?["Y"];  var posZ = pos?["Z"];
                float worldX = posX != null ? (float)posX : 0f;
                float worldY = posY != null ? (float)posY : 0f;
                float worldZ = posZ != null ? (float)posZ : 0f;

                EdnaLogger.Log($"[FloorMapper] querying Structure/GlobalToStructPos for entity={entityId} worldPos=({worldX},{worldY},{worldZ})");
                var structPosJson = await _messenger.RequestAsync(
                    "Structure", "GlobalToStructPos",
                    $"{{\"EntityId\":{entityId},\"Pos\":{{\"X\":{worldX},\"Y\":{worldY},\"Z\":{worldZ}}}}}",
                    RequestTimeout);

                EdnaLogger.Log($"[FloorMapper] Structure/GlobalToStructPos response: {structPosJson}");
                var sp      = JObject.Parse(structPosJson);
                var spos  = sp["StructPos"];
                var sposY = spos?["Y"];  var sposX = spos?["X"];  var sposZ = spos?["Z"];
                int structY = sposY != null ? (int)sposY : 0;
                int playerX = sposX != null ? (int)sposX : 0;
                int playerZ = sposZ != null ? (int)sposZ : 0;

                statusCallback($"Scanning entity {entityId} Y={structY}...");

                // Structure/ScanFloor: pending server implementation
                var wallTask  = _messenger.RequestAsync("Structure", "ScanFloor",
                    $"{{\"EntityId\":{entityId},\"Y\":{structY}}}", RequestTimeout);
                var floorTask = _messenger.RequestAsync("Structure", "ScanFloor",
                    $"{{\"EntityId\":{entityId},\"Y\":{structY - 1}}}", RequestTimeout);

                await Task.WhenAll(wallTask, floorTask);

                EdnaLogger.Log($"[FloorMapper] ScanFloor Y={structY} response: {wallTask.Result}");
                EdnaLogger.Log($"[FloorMapper] ScanFloor Y={structY - 1} response: {floorTask.Result}");
                var wallScan  = JObject.Parse(wallTask.Result);
                var floorScan = JObject.Parse(floorTask.Result);

                var wallBlocks  = ParseBlocks(wallScan);
                var floorBlocks = ParseBlocks(floorScan);

                var (minX, minZ, maxX, maxZ) = CombinedBounds(wallScan, floorScan);
                int width  = maxX - minX + 1;
                int height = maxZ - minZ + 1;

                EdnaLogger.Detail($"[FloorMapper] scan done: entity={entityId} Y={structY} {width}x{height}");

                return FloorMapDocument.FromScan(
                    entityId, structY, playfield, solarSystem,
                    minX, minZ, width, height, playerX, playerZ,
                    wallBlocks, floorBlocks);
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

        // ── Internal helpers ──────────────────────────────────────────────────

        private static readonly HashSet<int> SkippedTypes = new HashSet<int>();

        private static List<BlockEntry> ParseBlocks(JObject scan)
        {
            var list   = new List<BlockEntry>();
            var blocks = scan["Blocks"] as JArray;
            if (blocks == null) return list;
            foreach (var b in blocks)
            {
                int type = (int)(b["Type"] ?? 0);
                if (SkippedTypes.Contains(type)) continue;
                list.Add(new BlockEntry
                {
                    X        = (int)b["X"]!,
                    Z        = (int)b["Z"]!,
                    Type     = type,
                    Shape    = (int)(b["Shape"]    ?? 0),
                    Rotation = (int)(b["Rotation"] ?? 0),
                });
            }
            return list;
        }

        private static (int minX, int minZ, int maxX, int maxZ) CombinedBounds(
            JObject wallScan, JObject floorScan)
        {
            static (int, int, int, int) FromScan(JObject s)
            {
                var mn = s["MinPos"]!;
                var mx = s["MaxPos"]!;
                return ((int)mn["X"]!, (int)mn["Z"]!, (int)mx["X"]!, (int)mx["Z"]!);
            }
            var (w0, w2, w1, w3) = FromScan(wallScan);
            var (f0, f2, f1, f3) = FromScan(floorScan);
            return (Math.Min(w0, f0), Math.Min(w2, f2), Math.Max(w1, f1), Math.Max(w3, f3));
        }
    }
}
