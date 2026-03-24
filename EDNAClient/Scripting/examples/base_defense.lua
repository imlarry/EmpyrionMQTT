--[[
  base_defense.lua — example user script

  Three drones defend a base. This is the complete user-authored script;
  all plumbing is in the library.

  Execution model — event-driven, no polling:
    This script runs once at load time to set up subscriptions and state.
    After that it is silent until ESB publishes a Feeds.Scan event.
    On each scan: threats are assigned, each drone's BT evaluates once,
    commands are published, signals are emitted. Then silence again.

  To activate: copy to %LocalAppData%\EDNA\scripts\
  Hot-reload:  edit and save — reloads without restarting EDNA.

  Blackboard keys written by this script:
    "threat_count"    number — active threat assignments across the squadron
    "sq_has_threats"  bool   — true when any drone has an assigned threat

  MQTT commands published (to ESB / drone controller):
    EDNA/Q/Drone.Command/{id}/0   { DroneId, Command, [TargetId] }

  MQTT signals emitted:
    EDNA/E/Signal.BaseThreat/lua/0    "true"/"false"
    EDNA/E/Signal.ThreatCount/lua/0   number as string
]]

local Drone    = require("edna.drone")
local Squadron = require("edna.squadron")
local BT       = require("edna.bt")
local Signals  = require("edna.signals")

-- ── Drones ────────────────────────────────────────────────────────────────
-- Each Drone.create() call subscribes to +/E/Drone.State.{id}/+/+
-- and maintains isolated x,y,z,fuel state updated on each delivery.

local d1 = Drone.create(1)
local d2 = Drone.create(2)
local d3 = Drone.create(3)

-- ── Shared behavior tree ──────────────────────────────────────────────────
-- BT.when / BT.do_ call the named method on whichever drone is being evaluated.
-- The tree itself is a pure function — safe to share across all three instances.

local defend = BT.selector(
  BT.sequence(BT.when("fuel_low"),   BT.do_("return_to_base")),
  BT.sequence(BT.when("has_target"), BT.do_("engage")),
  BT.do_("patrol")
)

d1:set_tree(defend)
d2:set_tree(defend)   -- same tree object, independent state per drone
d3:set_tree(defend)

-- ── Squadron coordination ─────────────────────────────────────────────────
-- sq:watch() owns the single Feeds.Scan subscription for the whole group.
-- On each event: distinct threats assigned, all three BTs evaluate once.
-- The optional callback fires after decide() completes — use for signals
-- and blackboard writes that downstream scripts may be watching.

local sq = Squadron.create({ d1, d2, d3 })

sq:watch("+/E/Feeds.Scan/+/+", function(sq)
  local has   = sq:has_threats()
  local count = sq:threat_count()

  -- Signals — consumed by HUD skills, external clients, other scripts
  Signals.emit("BaseThreat",   has)
  Signals.emit("ThreatCount",  count)

  -- Blackboard — consumed by other scripts in this EDNA session via bb:watch()
  bb:set("sq_has_threats", has)
  bb:set("threat_count",   count)
end)

-- ── Example: react to blackboard from another script perspective ──────────
-- (Shown here for illustration — in practice this would live in a separate
--  script such as hud_overlay.lua, watching the key written above.)
--
-- bb:watch("threat_count", function(n)
--   if n > 2 then Signals.emit("HighAlert", true) end
-- end)
