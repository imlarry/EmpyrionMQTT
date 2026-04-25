using EDNAClient.Core;
using EDNAClient.Helpers;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Skills.FloorMap
{
    // Orchestrates the three-step MQTT sequence that produces a floor map:
    //   1. V2.Player -- get player world position, current structure, and playfield
    //   2. V2.Structure.GlobalToStructPos -- convert world pos -> struct block pos (gives Y)
    //   3. V2.Structure.ScanFloor x2 -- scan Y (walls) and Y-1 (floor) in parallel
    internal class FloorMapper
    {
        private readonly MqttRequester _req = new MqttRequester();

        public async Task StartAsync(IMessenger messenger)
        {
            await _req.StartAsync(messenger,
                "V2.Player",
                "V2.Structure.GlobalToStructPos",
                "V2.Structure.ScanFloor");
        }

        public void Stop() => _req.Stop();

        // Returns a FloorMapDocument on success, null on failure (e.g. not in a structure).
        // statusCallback receives progress/error text for immediate UI feedback.
        public async Task<FloorMapDocument?> RefreshAsync(
            string solarSystem, string playfield,
            Action<string> statusCallback)
        {
            EdnaLogger.Detail("[FloorMapper] RefreshAsync start");
            try
            {
                EdnaLogger.Log($"[FloorMapper] querying V2.Player (playfield={playfield} solarSystem={solarSystem})");
                var playerJson = await _req.RequestAsync(
                    "V2.Player",
                    "{\"Properties\":[\"Position\",\"CurrentStructureId\",\"CurrentStructureEntityId\"]}");

                EdnaLogger.Log($"[FloorMapper] V2.Player response: {playerJson}");
                var pj  = JObject.Parse(playerJson);
                var csi = pj["CurrentStructureId"];
                if (csi == null || csi.Type == JTokenType.Null)
                {
                    statusCallback("Not inside a structure");
                    EdnaLogger.Log("[FloorMapper] aborted: CurrentStructureId is null");
                    return null;
                }

                int   entityId = pj["CurrentStructureEntityId"] != null ? (int)pj["CurrentStructureEntityId"]! : 0;
                var   pos      = pj["Position"]!;
                float worldX   = (float)pos["X"]!;
                float worldY   = (float)pos["Y"]!;
                float worldZ   = (float)pos["Z"]!;

                EdnaLogger.Log($"[FloorMapper] querying GlobalToStructPos for entity={entityId} worldPos=({worldX},{worldY},{worldZ})");
                var structPosJson = await _req.RequestAsync(
                    "V2.Structure.GlobalToStructPos",
                    $"{{\"EntityId\":{entityId},\"Pos\":{{\"X\":{worldX},\"Y\":{worldY},\"Z\":{worldZ}}}}}");

                EdnaLogger.Log($"[FloorMapper] GlobalToStructPos response: {structPosJson}");
                var sp      = JObject.Parse(structPosJson);
                var spos    = sp["StructPos"]!;
                int structY = (int)spos["Y"]!;
                int playerX = (int)spos["X"]!;
                int playerZ = (int)spos["Z"]!;

                statusCallback($"Scanning entity {entityId} Y={structY}...");

                var wallTask  = _req.RequestAsync("V2.Structure.ScanFloor",
                    $"{{\"EntityId\":{entityId},\"Y\":{structY}}}");
                var floorTask = _req.RequestAsync("V2.Structure.ScanFloor",
                    $"{{\"EntityId\":{entityId},\"Y\":{structY - 1}}}");

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
