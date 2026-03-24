--[[
  edna.squadron — Multi-drone coordinator

  Owns a single MQTT subscription for scan events on behalf of all member drones.
  On each event: assigns distinct threats (Predator faction) round-robin across
  drones, then calls decide() on each so their behavior trees evaluate once.

  Coordination logic lives here — individual drones only know about themselves.
  The squadron is the shared thing; drone state remains isolated.

  Usage:
    local Squadron = require("edna.squadron")
    local sq = Squadron.create({ d1, d2, d3 })

    -- Wire to scan events; optional callback fires after all drones have decided
    sq:watch("+/E/Feeds.Scan/+/+", function(sq)
      Signals.emit("BaseThreat", sq:has_threats())
      bb:set("threat_count", sq:threat_count())
    end)
]]

local M = {}

function M.create(drones)
  local self = { drones = drones or {} }

  -- ── Subscription ──────────────────────────────────────────────────────────

  -- Subscribe to topicFilter. On each scan snapshot:
  --   1. Assign distinct threats to member drones
  --   2. Call decide() on each drone (BT evaluates once)
  --   3. If provided, call after_fn(self) — use for signals or blackboard writes
  --
  -- Only one subscription per squadron regardless of how many drones are members.
  function self:watch(topicFilter, after_fn)
    mqtt.subscribe(topicFilter, function(_, payload)
      local ok, data = pcall(json.parse, payload)
      if not ok or not data then return end

      -- Terminal status (e.g. feed expired) — ignore; feed auto-rearms via C# ThreatTracker
      if data.Status then return end

      self:assign_threats(data.Entities or {})

      for _, drone in ipairs(self.drones) do
        drone:decide()
      end

      if after_fn then after_fn(self) end
    end)
  end

  -- ── Threat assignment ──────────────────────────────────────────────────────

  -- Distribute distinct Predator-faction entity ids across member drones.
  -- Drones beyond the threat count have their assignment cleared (nil).
  -- Extend faction filtering here if other hostile factions are added.
  function self:assign_threats(entities)
    local threats = {}
    for _, e in ipairs(entities) do
      local faction = e.Faction or ""
      if faction:find("Predator") then
        table.insert(threats, e.EntityId)
      end
    end

    for i, drone in ipairs(self.drones) do
      drone.assigned = threats[i]    -- nil when i > #threats
    end
  end

  -- ── Queries ───────────────────────────────────────────────────────────────

  -- True if at least one member drone has a threat assigned.
  function self:has_threats()
    for _, drone in ipairs(self.drones) do
      if drone.assigned ~= nil then return true end
    end
    return false
  end

  -- Number of member drones currently holding a threat assignment.
  function self:threat_count()
    local n = 0
    for _, drone in ipairs(self.drones) do
      if drone.assigned ~= nil then n = n + 1 end
    end
    return n
  end

  -- ── Membership ────────────────────────────────────────────────────────────

  -- Add a drone after squadron creation (e.g. when a new entity is discovered).
  function self:add(drone)
    table.insert(self.drones, drone)
  end

  -- Remove a drone from the squadron by id.
  function self:remove(entity_id)
    for i, drone in ipairs(self.drones) do
      if drone.id == entity_id then
        table.remove(self.drones, i)
        return
      end
    end
  end

  return self
end

return M
