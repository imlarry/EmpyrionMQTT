using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Skills.FloorMap
{
    // Orchestrates the three-step MQTT sequence that produces a floor map:
    //   1. V2.Player -- get player world Position and CurrentStructureId
    //   2. V2.Structure.GlobalToStructPos -- convert world pos -> struct block pos (gives Y)
    //   3. V2.Structure.ScanFloor x2 -- scan Y (walls) and Y-1 (floor) in parallel
    //
    // All ESB responses arrive via the shared Messenger subscription registered in
    // StartAsync. Concurrent requests are correlated by sequence number via a
    // TaskCompletionSource dictionary.
    //
    // Note: ESB routing uses "Client" as the authoritative source. This is correct
    // for SinglePlayer. Multiplayer support requires EdnaContext.AuthoritativeSource
    // to be threaded through -- a known limitation shared with ThreatTracker.
    internal class FloorMapper
    {
        private const string EsbApp = "Client";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        private readonly IMessenger        _messenger;
        private readonly FloorMapViewModel _viewModel;

        private int _seqId;
        private readonly Dictionary<int, TaskCompletionSource<(bool Ok, string Body)>> _pending = new();

        public FloorMapper(IMessenger messenger, FloorMapViewModel viewModel)
        {
            _messenger = messenger;
            _viewModel = viewModel;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        // Subscribe to response and exception topics for all handlers we use.
        // SubscribeEventAsync registers the callback by SubjectId; calling it for
        // both R and X leaves both broker subscriptions active while the callback
        // (OnAnyResponse) handles both message classes.
        public async Task StartAsync()
        {
            foreach (var subject in new[] {
                "V2.Player",
                "V2.Structure.GlobalToStructPos",
                "V2.Structure.ScanFloor" })
            {
                await _messenger.SubscribeEventAsync($"+/R/{subject}/+/+", OnAnyResponse);
                await _messenger.SubscribeEventAsync($"+/X/{subject}/+/+", OnAnyResponse);
            }
        }

        public void Stop()
        {
            // Cancel any in-flight requests so awaiting callers are released.
            lock (_pending)
            {
                foreach (var tcs in _pending.Values)
                    tcs.TrySetCanceled();
                _pending.Clear();
            }
        }

        // ── On-demand refresh ─────────────────────────────────────────────────

        public async Task RefreshAsync()
        {
            try
            {
                // Step 1: player position and current structure.
                var playerJson = await RequestAsync(
                    "V2.Player",
                    "{\"Properties\":[\"Position\",\"CurrentStructureId\",\"CurrentStructureEntityId\"]}");

                var pj  = JObject.Parse(playerJson);
                var csi = pj["CurrentStructureId"];
                if (csi == null || csi.Type == JTokenType.Null)
                {
                    SetStatus("Not inside a structure");
                    return;
                }

                var csei = pj["CurrentStructureEntityId"];
                SetStatus($"StructureId={csi} EntityId={csei} -- determining correct lookup key");

                int  entityId = (int)csei;
                var  pos      = pj["Position"]!;
                // World position as floats
                float worldX = (float)pos["X"]!;
                float worldY = (float)pos["Y"]!;
                float worldZ = (float)pos["Z"]!;

                // Step 2: convert world position -> struct block position to get the Y level.
                var structPosJson = await RequestAsync(
                    "V2.Structure.GlobalToStructPos",
                    $"{{\"EntityId\":{entityId},\"Pos\":{{\"X\":{worldX},\"Y\":{worldY},\"Z\":{worldZ}}}}}");

                var sp     = JObject.Parse(structPosJson);
                var spos   = sp["StructPos"]!;
                int structY = (int)spos["Y"]!;

                // Step 3: scan wall level (Y) and floor level (Y-1) in parallel.
                // Y is the player's block position in structure space; Y-1 is the floor surface.
                var wallTask  = RequestAsync(
                    "V2.Structure.ScanFloor",
                    $"{{\"EntityId\":{entityId},\"Y\":{structY}}}");
                var floorTask = RequestAsync(
                    "V2.Structure.ScanFloor",
                    $"{{\"EntityId\":{entityId},\"Y\":{structY - 1}}}");

                await Task.WhenAll(wallTask, floorTask);

                var wallScan  = JObject.Parse(wallTask.Result);
                var floorScan = JObject.Parse(floorTask.Result);

                // Build block dictionaries keyed by (X,Z).
                var wallBlocks  = ParseBlocks(wallScan);
                var floorBlocks = ParseBlocks(floorScan);

                // Combined bounding box from both scans.
                var (minX, minZ, maxX, maxZ) = CombinedBounds(wallScan, floorScan);
                int width  = maxX - minX + 1;
                int height = maxZ - minZ + 1;

                // Player struct-space XZ (integer).
                int playerX = (int)spos["X"]!;
                int playerZ = (int)spos["Z"]!;

                int totalBlocks = wallBlocks.Count + floorBlocks.Count;
                string status = $"Entity {entityId}  Y={structY}  {width}x{height}  " +
                                $"{totalBlocks} blocks";

                // Render on the UI thread.
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _viewModel.Render(wallBlocks, floorBlocks, minX, minZ, width, height,
                                      playerX, playerZ, status);
                    _viewModel.IsLoading = false;
                });
            }
            catch (OperationCanceledException)
            {
                SetStatus("Scan cancelled");
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
            }
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        // Block type IDs that are invisible in-game and should not appear on the map.
        // Add IDs here as they are identified from live scan data (check the Type field
        // in the ScanFloor MQTT response). Known entries:
        //   <none confirmed yet -- add thin-plate spawner type ID when identified>
        private static readonly System.Collections.Generic.HashSet<int> SkippedTypes =
            new System.Collections.Generic.HashSet<int> { };

        private static IReadOnlyDictionary<(int, int), BlockInfo> ParseBlocks(JObject scan)
        {
            var dict   = new Dictionary<(int, int), BlockInfo>();
            var blocks = scan["Blocks"] as JArray;
            if (blocks == null) return dict;
            foreach (var b in blocks)
            {
                int type = (int)(b["Type"] ?? 0);
                if (SkippedTypes.Contains(type)) continue;

                int x = (int)b["X"]!;
                int z = (int)b["Z"]!;
                dict[(x, z)] = new BlockInfo
                {
                    Type     = type,
                    Shape    = (int)(b["Shape"]    ?? 0),
                    Rotation = (int)(b["Rotation"] ?? 0),
                };
            }
            return dict;
        }

        private static (int minX, int minZ, int maxX, int maxZ) CombinedBounds(
            JObject wallScan, JObject floorScan)
        {
            // Use the MinPos/MaxPos fields from both scans (same structure, same bounds).
            static (int mnX, int mnZ, int mxX, int mxZ) FromScan(JObject scan)
            {
                var mn = scan["MinPos"]!;
                var mx = scan["MaxPos"]!;
                return ((int)mn["X"]!, (int)mn["Z"]!, (int)mx["X"]!, (int)mx["Z"]!);
            }

            var (w0, w2, w1, w3) = FromScan(wallScan);
            var (f0, f2, f1, f3) = FromScan(floorScan);
            return (
                Math.Min(w0, f0),
                Math.Min(w2, f2),
                Math.Max(w1, f1),
                Math.Max(w3, f3));
        }

        // Sends a request and awaits the ESB response. Returns the response payload
        // string on R, or throws on X (exception from ESB).
        private async Task<string> RequestAsync(string subjectId, string payload)
        {
            int seq = Interlocked.Increment(ref _seqId);
            var tcs = new TaskCompletionSource<(bool Ok, string Body)>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pending) _pending[seq] = tcs;
            try
            {
                await _messenger.SendAsync(
                    MessageClass.Request,
                    $"{EsbApp}/Q/{subjectId}/*/{seq}",
                    payload);

                var result = await tcs.Task.WaitAsync(RequestTimeout);
                if (!result.Ok)
                    throw new Exception($"ESB {subjectId}: {result.Body}");
                return result.Body;
            }
            finally
            {
                lock (_pending) _pending.Remove(seq);
            }
        }

        // Receives ALL responses (R and X) for all subscribed SubjectIds.
        // Correlation is by sequence number in the topic's 5th segment.
        private Task OnAnyResponse(string topic, string payload)
        {
            var parts = topic.Split('/');
            if (parts.Length < 5) return Task.CompletedTask;
            if (!int.TryParse(parts[4], out int seq)) return Task.CompletedTask;

            bool isOk = parts[1] == "R";

            TaskCompletionSource<(bool, string)>? tcs;
            lock (_pending)
            {
                if (!_pending.TryGetValue(seq, out tcs)) return Task.CompletedTask;
                _pending.Remove(seq);
            }
            tcs.TrySetResult((isOk, payload ?? ""));
            return Task.CompletedTask;
        }

        private void SetStatus(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _viewModel.StatusText = text;
                _viewModel.IsLoading  = false;
            });
        }
    }
}
