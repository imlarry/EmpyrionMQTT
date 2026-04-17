--[[
  scripts.drone — Per-drone state object

  Drone.create(entity_id) returns a fully self-contained drone instance:
    - Subscribes to its own MQTT state topic on creation (Drone.State.{id})
    - Exposes conditions (fuel_low, has_target, at_destination)
    - Exposes actions (return_to_base, engage, patrol) that publish MQTT commands
    - Holds a behavior tree and evaluates it via :decide()

  Each create() call closes over its own local state table — three Drone.create()
  calls in the same script produce three completely independent instances even
  though they share the same engine and the same tree logic.

  Usage:
    local Drone = require("scripts.drone")
    local d = Drone.create(42)
    d:set_tree(my_tree)
    -- squadron calls d:decide() on each scan event

  Expected MQTT topics (published by ESB mod or drone controller):
    +/E/Drone.State.{id}/+/+   payload: { X, Y, Z, Fuel }
]]

local M = {}

function M.create(entity_id)
  local self = {
    id       = entity_id,
    x        = 0,
    y        = 0,
    z        = 0,
    fuel     = 1.0,
    mode     = "idle",
    assigned = nil,    -- threat entity id assigned by a Squadron coordinator
    _tree    = nil,
  }

  -- ── Self-subscription ────────────────────────────────────────────────────
  -- SubjectId = "Drone.State.{id}" — unique per drone, no collision risk.

  mqtt.subscribe(
    string.format("+/E/Drone.State.%d/+/+", entity_id),
    function(_, payload)
      local ok, data = pcall(json.parse, payload)
      if not ok or not data then return end
      if data.X    ~= nil then self.x    = data.X    end
      if data.Y    ~= nil then self.y    = data.Y    end
      if data.Z    ~= nil then self.z    = data.Z    end
      if data.Fuel ~= nil then self.fuel = data.Fuel end
    end)

  -- ── Conditions ───────────────────────────────────────────────────────────
  -- Return bool; used directly or wrapped with BT.when().

  function self:fuel_low(threshold)
    return self.fuel < (threshold or 0.2)
  end

  function self:has_target()
    return self.assigned ~= nil
  end

  -- at_destination requires a destination to be stored; extend as needed.
  function self:at_destination(tolerance)
    return false  -- placeholder
  end

  -- ── Commands (internal) ───────────────────────────────────────────────────

  local function send_command(payload_tbl)
    mqtt.request(
      string.format("EDNA/Q/Drone.Command/%d/0", entity_id),
      json.serialize(payload_tbl))
  end

  -- ── Actions ───────────────────────────────────────────────────────────────
  -- Return "success" | "failure" | "running" for BT node evaluation.
  -- Called via BT.do_("action_name") or directly in an action() node.

  function self:return_to_base()
    send_command({ DroneId = self.id, Command = "ReturnToBase" })
    self.mode = "returning"
    log.info(string.format("Drone %d returning to base (fuel=%.0f%%)", self.id, self.fuel * 100))
    return "running"
  end

  function self:engage()
    if not self.assigned then return "failure" end
    send_command({ DroneId = self.id, Command = "Engage", TargetId = self.assigned })
    self.mode = "engaging"
    log.info(string.format("Drone %d engaging target %s", self.id, tostring(self.assigned)))
    return "running"
  end

  function self:patrol()
    send_command({ DroneId = self.id, Command = "Patrol" })
    self.mode = "patrolling"
    return "running"
  end

  -- ── BT runner ─────────────────────────────────────────────────────────────

  function self:set_tree(tree)
    self._tree = tree
  end

  -- Evaluate the behavior tree once with self as context.
  -- Called by Squadron on each triggering event — not by C# on a timer.
  function self:decide()
    if not self._tree then return "failure" end
    return self._tree(self)
  end

  return self
end

return M
