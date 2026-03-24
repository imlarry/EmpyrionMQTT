--[[
  behavior_tree_example.lua
  -------------------------
  Demonstrates a coroutine-based behavior tree for drone logic.

  MoonSharp supports Lua coroutines, making it natural to write behavior trees
  as suspendable sequences. C# ticks the tree by calling:
    _luaHost.Call("behavior_tree_example", "tick", droneStateTable)

  The drone state table is a plain Lua table built by C# from game data:
    { id=42, x=100, y=80, z=200, fuel=0.7, target_x=150, target_z=250 }

  Nodes return: "success", "failure", or "running"
]]

-- ── Node primitives ────────────────────────────────────────────────────────

-- sequence: run children left-to-right; fail on first failure
local function sequence(...)
  local children = {...}
  return function(state)
    for _, child in ipairs(children) do
      local result = child(state)
      if result ~= "success" then return result end
    end
    return "success"
  end
end

-- selector: run children left-to-right; succeed on first success
local function selector(...)
  local children = {...}
  return function(state)
    for _, child in ipairs(children) do
      local result = child(state)
      if result ~= "failure" then return result end
    end
    return "failure"
  end
end

-- condition: returns success/failure based on a predicate
local function condition(predicate)
  return function(state)
    return predicate(state) and "success" or "failure"
  end
end

-- action: runs a function, returns its result
local function action(fn)
  return function(state)
    return fn(state)
  end
end

-- ── Drone actions ──────────────────────────────────────────────────────────

local function move_to_target(state)
  local dx = state.target_x - state.x
  local dz = state.target_z - state.z
  local dist = math.sqrt(dx*dx + dz*dz)

  if dist < 5 then
    log.info("Drone " .. state.id .. " reached target")
    return "success"
  end

  -- Publish a move command onto the bus
  local cmd = string.format('{"DroneId":%d,"TargetX":%.1f,"TargetZ":%.1f}',
    state.id, state.target_x, state.target_z)
  mqtt.request("EDNA/Q/Drone.Move/*/0", cmd)
  log.info("Drone " .. state.id .. " moving, dist=" .. string.format("%.1f", dist))
  return "running"
end

local function return_to_base(state)
  local cmd = string.format('{"DroneId":%d,"ReturnToBase":true}', state.id)
  mqtt.request("EDNA/Q/Drone.Move/*/0", cmd)
  log.info("Drone " .. state.id .. " returning to base (low fuel)")
  return "running"
end

-- ── Tree definition ────────────────────────────────────────────────────────

local tree = selector(
  -- Priority 1: return to base if fuel is low
  sequence(
    condition(function(s) return s.fuel < 0.2 end),
    action(return_to_base)
  ),
  -- Priority 2: move toward assigned target
  action(move_to_target)
)

-- ── Public tick function (called by C# per update cycle) ──────────────────

function tick(state)
  local result = tree(state)
  -- result is "success" | "failure" | "running"
  return result
end
