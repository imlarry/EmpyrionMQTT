--[[
  signal_example.lua
  ------------------
  Demonstrates the signal logic pattern: react to game events broadcast from C#,
  compute derived state, and publish signals back onto the MQTT bus.

  This script is NOT loaded automatically — copy it to:
    %LocalAppData%\EDNA\scripts\

  Broadcast hooks fired by EdnaService:
    on_game_enter(topic, payload)   -- player entered a playfield (game session active)
    on_game_exit(topic, payload)    -- player exited a playfield

  Additional hooks can be added by calling LuaScriptHost.Broadcast() from any C# skill.
]]

-- ── State ──────────────────────────────────────────────────────────────────

local pilot_mode = false

-- ── Helpers ───────────────────────────────────────────────────────────────

local function publish_signal(name, value)
  -- Signals use the convention: EDNA/E/Signal.<name>/<clientId>/<seq>
  -- Using the short-form topic: SendAsync(MessageClass.Event, "Signal.<name>", payload)
  -- From Lua we must provide a fully-formed topic; use a placeholder client segment.
  mqtt.publish("EDNA/E/Signal." .. name .. "/lua/0", tostring(value))
  log.info("Signal." .. name .. " = " .. tostring(value))
end

-- ── Game lifecycle hooks ──────────────────────────────────────────────────

function on_game_enter(topic, payload)
  log.info("Game entered — signal_example active")
  pilot_mode = false
  publish_signal("Ready", true)
end

function on_game_exit(topic, payload)
  log.info("Game exited — clearing signals")
  publish_signal("Ready", false)
end

-- ── Scan snapshot hook (registered by a C# skill calling Broadcast) ───────
-- To use this, add to ThreatTracker.OnScanSnapshot():
--   _luaHost.Broadcast("on_scan_snapshot", topic, payload);

function on_scan_snapshot(topic, payload)
  local ok, data = pcall(json.parse, payload)
  if not ok or not data then return end

  -- Detect pilot mode transitions
  local is_pilot = data.Player and data.Player.IsPilot == true
  if is_pilot ~= pilot_mode then
    pilot_mode = is_pilot
    publish_signal("PilotMode", pilot_mode)
  end

  -- Count threats within 50 m
  local threat_count = 0
  if data.Entities then
    for _, e in ipairs(data.Entities) do
      if e.Faction and string.find(e.Faction, "Predator") then
        local pos = e.Position
        if pos then
          local dx = (pos.X or 0) - (data.Player.Position.X or 0)
          local dz = (pos.Z or 0) - (data.Player.Position.Z or 0)
          local dist = math.sqrt(dx*dx + dz*dz)
          if dist < 50 then threat_count = threat_count + 1 end
        end
      end
    end
  end

  if threat_count > 0 then
    publish_signal("CloseThreats", threat_count)
  end
end
