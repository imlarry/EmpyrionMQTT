--[[
  vessel_signals.lua — CV / SV presence and crew state signals

  Reacts to EntityLoaded / EntityUnloaded events from the ESB and emits
  named signals onto the bus. Other scripts (HUD, automation, alerts) can
  subscribe to those signals without knowing anything about entity IDs.

  Verified against: Docs/TestFixtures/vessel_environment.md
  Playfield: Akua (TemperateStarter moon), game "Von Neumann MQTT"

  Signals emitted:
    EDNA/E/Signal.CV_Present/lua/0      "true" / "false"
    EDNA/E/Signal.SV_Present/lua/0      "true" / "false"
    EDNA/E/Signal.Crew_Embarked/lua/0   "true" / "false"

  Blackboard keys written:
    "cv_present"      bool
    "sv_present"      bool
    "crew_embarked"   bool

  To activate: copy to %LocalAppData%\EDNA\scripts\
  To verify:   mosquitto_sub -h localhost -p 1883 -t "EDNA/E/+/+/+" -v

  Note: MoonSharp 2.0.0 JSON API is json.parse / json.serialize
        (not json.decode / json.encode)
]]

-- ── Signal emit (inlined — no require dependency) ─────────────────────────

local function signal_emit(name, value)
  mqtt.publish("EDNA/E/Signal." .. name .. "/lua/0", tostring(value))
  log.info("Signal." .. name .. " = " .. tostring(value))
end

-- ── Entity registry ────────────────────────────────────────────────────────
-- OnEntityUnloaded carries only Id + Name, no Type. We populate this table
-- from each EntityLoaded payload so we can look up Type on unload.
--   entities[id] = { name=string, type=string, faction=string }

local entities = {}

-- ── Presence counters ───────────────────────────────────────────────────────
-- Track how many player-faction CVs/SVs are loaded. Signal emits only on
-- 0→1 (true) and 1→0 (false) transitions so multiple vessels don't spam.

local cv_count = 0
local sv_count = 0

local function update_count(counter_ref, delta, signal_name, bb_key)
  local new_val = counter_ref + delta
  if new_val < 0 then new_val = 0 end
  local was_present = counter_ref > 0
  local now_present = new_val > 0
  if was_present ~= now_present then
    emit_and_store(signal_name, bb_key, now_present)
  end
  return new_val
end

-- ── Type / faction helpers ─────────────────────────────────────────────────

local function is_player_faction(faction)
  return faction ~= nil and faction:find("^Player%.") ~= nil
end

local function is_cv(entity_type)
  return entity_type == "CV"
end

local function is_sv(entity_type)
  return entity_type == "SV"
end

-- ── Emit helper ────────────────────────────────────────────────────────────

local function emit_and_store(signal_name, bb_key, value)
  signal_emit(signal_name, value)
  bb:set(bb_key, value)
end

-- ── Entity lifecycle ───────────────────────────────────────────────────────
-- SubjectId confirmed from capture: Playfield.EntityLoaded
-- Payload includes: Id, Name, Type, Faction, Position, IsLocal, BelongsTo, DockedTo

mqtt.subscribe("+/E/Playfield.EntityLoaded/+/+", function(_, payload)
  local ok, entity = pcall(json.parse, payload)
  if not ok then log.error("EntityLoaded parse failed: " .. tostring(entity)); return end
  if not entity or not entity.Id then return end

  -- Register for unload lookup
  entities[entity.Id] = {
    name    = entity.Name,
    type    = entity.Type,
    faction = entity.Faction,
  }

  if not is_player_faction(entity.Faction) then return end

  if is_cv(entity.Type) then
    log.info("CV loaded: " .. tostring(entity.Name))
    cv_count = update_count(cv_count, 1, "CV_Present", "cv_present")
  end

  if is_sv(entity.Type) then
    log.info("SV loaded: " .. tostring(entity.Name))
    sv_count = update_count(sv_count, 1, "SV_Present", "sv_present")
  end
end)

-- SubjectId confirmed from capture: Playfield.OnEntityUnloaded
-- Payload: Id, Name only — no Type. Look up from registry.

mqtt.subscribe("+/E/Playfield.OnEntityUnloaded/+/+", function(_, payload)
  local ok, entity = pcall(json.parse, payload)
  if not ok then log.error("EntityUnloaded parse failed: " .. tostring(entity)); return end
  if not entity or not entity.Id then return end

  local reg = entities[entity.Id]
  if not reg then return end   -- unloaded before we saw a load (e.g. on session start)

  entities[entity.Id] = nil   -- evict from registry

  if not is_player_faction(reg.faction) then return end

  if is_cv(reg.type) then
    log.info("CV unloaded: " .. tostring(reg.name))
    cv_count = update_count(cv_count, -1, "CV_Present", "cv_present")
  end

  if is_sv(reg.type) then
    log.info("SV unloaded: " .. tostring(reg.name))
    sv_count = update_count(sv_count, -1, "SV_Present", "sv_present")
  end
end)

-- ── Crew / cockpit state ───────────────────────────────────────────────────
-- SV cockpit: GameEvent.EnterSVCockpit fires on entry (no args needed).
-- CV cockpit: GameEvent.DeviceUsed fires on entry; Arg1 == "CaptainChair01".
-- Exit (both): GameEvent.WindowClosed, Arg1 == "SVCVPilotOverlay".
--
-- All three SubjectIds confirmed from capture in vessel_environment.md.

local crew_state = false

local function set_crew(value)
  if value == crew_state then return end
  crew_state = value
  emit_and_store("Crew_Embarked", "crew_embarked", crew_state)
  log.info("Crew_Embarked = " .. tostring(crew_state))
end

-- SV entry — fires as soon as player enters SV cockpit seat
mqtt.subscribe("+/E/GameEvent.EnterSVCockpit/+/+", function(_, _)
  set_crew(true)
end)

-- CV entry — DeviceUsed fires for any device; filter to captain's chair
mqtt.subscribe("+/E/GameEvent.DeviceUsed/+/+", function(_, payload)
  local ok, event = pcall(json.parse, payload)
  if not ok then log.error("DeviceUsed parse failed: " .. tostring(event)); return end
  if not event then return end
  if event.Arg1 == "CaptainChair01" then
    set_crew(true)
  end
end)

-- Cockpit exit (SV and CV both close this overlay window)
mqtt.subscribe("+/E/GameEvent.WindowClosed/+/+", function(_, payload)
  local ok, event = pcall(json.parse, payload)
  if not ok then log.error("WindowClosed parse failed: " .. tostring(event)); return end
  if not event then return end
  if event.Arg1 == "SVCVPilotOverlay" then
    set_crew(false)
  end
end)
