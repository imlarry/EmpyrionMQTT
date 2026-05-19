using EDNAClient.Core;
using EDNAClient.Helpers;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Skills.ThreatRadar
{
    /// <summary>
    /// Subscribes to ESB Feeds.Scan and keeps ThreatViewModel current with
    /// directional threat levels. Each snapshot atomically updates both the
    /// player position and the threat entity set -- no polling, no per-entity
    /// TraceEntity subscriptions.
    /// </summary>
    public class ThreatTracker
    {
        private readonly EdnaContext     _ctx;
        private readonly IMessageBus     _bus;
        private readonly ThreatViewModel _viewModel;

        // Threat entities keyed by entity id; value is last-known XZ position
        private readonly Dictionary<int, (float X, float Z)> _threats = new Dictionary<int, (float X, float Z)>();

        private float _playerX, _playerZ;
        private float _fwdX = 0f, _fwdZ = 1f;   // default: facing +Z (world north)

        public ThreatTracker(EdnaContext ctx, ThreatViewModel viewModel)
        {
            _ctx       = ctx;
            _bus       = ctx.Bus;
            _viewModel = viewModel;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        public Task StartAsync()
        {
            _bus.OnEvent("App", "PlayfieldEntered", OnPlayfieldEntered);
            _bus.OnEvent("App", "Feeds.Scan",       OnScanSnapshot);
            return Task.CompletedTask;
        }

        private Task OnPlayfieldEntered(MessageEnvelope env)
        {
            _ = RequestScanAsync().ContinueWith(t =>
                EdnaLogger.Error("RequestScanAsync failed on PlayfieldEntered", t.Exception?.InnerException),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            _threats.Clear();
            _viewModel.ClearAll();
        }

        // ── Scan request ───────────────────────────────────────────────────

        private async Task RequestScanAsync()
        {
            // Feeds.Scan: pending ESB server implementation. TODO: target a specific
            // server MachineId discovered via Connect announcement; today this routes to
            // GameRcId which no longer carries req/res under the new addressing rule.
            var rcId = _ctx.GameRcId ?? _ctx.Bus.ContextRcId;
            await _bus.RequestAsync<object>(rcId, "App", "Feeds.Scan",
                new { Duration = 300, RefreshRate = 2 }, System.TimeSpan.FromSeconds(30));
        }

        // ── Scan snapshot handler ──────────────────────────────────────────

        private Task OnScanSnapshot(MessageEnvelope env)
        {
            try
            {
                var j = env.PayloadJson;
                if (j == null) return Task.CompletedTask;

                // Terminal event — re-arm the feed
                if (j["Status"] != null)
                {
                    _ = RequestScanAsync().ContinueWith(t =>
                        EdnaLogger.Error("RequestScanAsync failed on re-arm", t.Exception?.InnerException),
                        System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
                    return Task.CompletedTask;
                }

                // Extract player
                float px = _playerX, pz = _playerZ, fx = _fwdX, fz = _fwdZ;
                var player = j["Player"];
                if (player != null)
                {
                    // Piloting — tacradar has it; clear EDNA display and yield
                    if (player["IsPilot"]?.Value<bool>() == true)
                    {
                        UI.Invoke(() => { _threats.Clear(); _viewModel.ClearAll(); });
                        return Task.CompletedTask;
                    }

                    var pos = player["Position"];
                    var fwd = player["Forward"];
                    if (pos != null)
                    {
                        px = pos["X"]!.Value<float>();
                        pz = pos["Z"]!.Value<float>();
                        fx = fwd?["X"]?.Value<float>() ?? 0f;
                        fz = fwd?["Z"]?.Value<float>() ?? 1f;
                    }
                }

                // Extract threat entities from snapshot
                var newThreats = new Dictionary<int, (float X, float Z)>();
                var entities = j["Entities"] as JArray;
                if (entities != null)
                {
                    foreach (var e in entities)
                    {
                        var faction = e["Faction"]?.ToString() ?? "";
                        if (!faction.StartsWith("Predator", StringComparison.OrdinalIgnoreCase))
                            continue;

                        int id  = e["EntityId"]!.Value<int>();
                        var pos = e["Position"];
                        if (pos == null) continue;

                        newThreats[id] = (pos["X"]!.Value<float>(), pos["Z"]!.Value<float>());
                    }
                }

                EdnaLogger.Detail($"[ThreatTracker] snapshot: {newThreats.Count} threats");
                UI.Invoke(() =>
                {
                    _playerX = px;
                    _playerZ = pz;
                    _fwdX    = fx;
                    _fwdZ    = fz;

                    _threats.Clear();
                    foreach (var kv in newThreats)
                        _threats[kv.Key] = kv.Value;

                    Recompute();
                });
            }
            catch (Exception ex)
            {
#if DEBUG
                EdnaLogger.Warn($"OnScanSnapshot malformed payload: {ex.Message}");
#endif
            }
            return Task.CompletedTask;
        }

        // ── Quadrant computation ───────────────────────────────────────────

        // Called only from the UI thread.
        private void Recompute()
        {
            var q = new ThreatLevel[4]; // 0=N(ahead) 1=E(right) 2=S(behind) 3=W(left)

            foreach (var (_, pos) in _threats)
            {
                float dx   = pos.X - _playerX;
                float dz   = pos.Z - _playerZ;
                float dist = MathF.Sqrt(dx * dx + dz * dz);

                if (dist > 100f) continue;

                ThreatLevel level = dist < 20f ? ThreatLevel.Imminent
                                  : dist < 50f ? ThreatLevel.Close
                                               : ThreatLevel.Near;

                float bearingWorld  = MathF.Atan2(dx, dz);
                float playerHeading = MathF.Atan2(_fwdX, _fwdZ);
                float bearing       = bearingWorld - playerHeading;

                const float TwoPi = 2f * MathF.PI;
                bearing = ((bearing % TwoPi) + TwoPi) % TwoPi;

                int qi = (int)((bearing + MathF.PI / 4f) / (MathF.PI / 2f)) % 4;
                if (level > q[qi]) q[qi] = level;
            }

            _viewModel.NorthThreat = q[0];
            _viewModel.EastThreat  = q[1];
            _viewModel.SouthThreat = q[2];
            _viewModel.WestThreat  = q[3];
        }
    }
}
