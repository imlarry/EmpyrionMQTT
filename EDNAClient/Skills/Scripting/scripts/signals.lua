--[[
  scripts.signals — Signal publishing helpers

  Signals are named MQTT events on the bus. Any EDNA script or external client
  (e.g. an esp32 display, Home Assistant) can subscribe to them.

  Convention: topic = "EDNA/E/Signal.{Name}/lua/0"

  Usage:
    local Signals = require("scripts.signals")
    Signals.emit("BaseThreat",    true)
    Signals.emit("ThreatCount",   3)
    Signals.emit_json("Squadron", { threat_count = 2, modes = {"engaging","patrolling"} })
]]

local M = {}

-- Publish a scalar signal (bool, number, or string coerced to string).
function M.emit(name, value)
  mqtt.publish("EDNA/E/Signal." .. name .. "/lua/0", tostring(value))
  log.info("Signal." .. name .. " = " .. tostring(value))
end

-- Publish a table signal encoded as a JSON object.
function M.emit_json(name, tbl)
  local payload = json.serialize(tbl)
  mqtt.publish("EDNA/E/Signal." .. name .. "/lua/0", payload)
  log.info("Signal." .. name .. " (json)")
end

return M
