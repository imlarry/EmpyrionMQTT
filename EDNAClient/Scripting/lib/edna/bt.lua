--[[
  edna.bt — Behavior Tree node factories

  Nodes are plain functions: (ctx) → "success" | "failure" | "running"
  ctx is the object being evaluated (e.g. a Drone instance).
  No classes, no metatables — just composable closures.

  Usage:
    local BT = require("edna.bt")

    local tree = BT.selector(
      BT.sequence(BT.when("fuel_low"),   BT.do_("return_to_base")),
      BT.sequence(BT.when("has_target"), BT.do_("engage")),
      BT.do_("patrol")
    )

    local result = tree(drone)   -- "success" | "failure" | "running"
]]

local M = {}

-- ── Composites ────────────────────────────────────────────────────────────

-- sequence: evaluate children left-to-right.
-- Returns "failure"/"running" immediately on the first non-success.
-- Returns "success" only if all children succeed.
function M.sequence(...)
  local children = {...}
  return function(ctx)
    for _, child in ipairs(children) do
      local r = child(ctx)
      if r ~= "success" then return r end
    end
    return "success"
  end
end

-- selector: evaluate children left-to-right.
-- Returns "success"/"running" immediately on the first non-failure.
-- Returns "failure" only if all children fail.
function M.selector(...)
  local children = {...}
  return function(ctx)
    for _, child in ipairs(children) do
      local r = child(ctx)
      if r ~= "failure" then return r end
    end
    return "failure"
  end
end

-- ── Leaves ────────────────────────────────────────────────────────────────

-- condition: wraps a predicate; returns "success" if true, "failure" if false.
function M.condition(fn)
  return function(ctx)
    return fn(ctx) and "success" or "failure"
  end
end

-- action: wraps an action function; returns whatever the function returns.
-- The function should return "success", "failure", or "running".
function M.action(fn)
  return function(ctx)
    return fn(ctx)
  end
end

-- ── Decorators ────────────────────────────────────────────────────────────

-- invert: flips "success" ↔ "failure"; passes "running" through unchanged.
function M.invert(child)
  return function(ctx)
    local r = child(ctx)
    if     r == "success" then return "failure"
    elseif r == "failure" then return "success"
    else                       return r end
  end
end

-- always_succeed: returns "success" even if the child fails.
-- "running" passes through so long-running children still suspend the tree.
function M.always_succeed(child)
  return function(ctx)
    local r = child(ctx)
    return r == "running" and "running" or "success"
  end
end

-- ── Method-name shorthands ────────────────────────────────────────────────

-- BT.when("method_name")
-- Shorthand condition that calls ctx:method_name() and expects a bool.
-- Lets the tree read as near-natural language:
--   BT.when("fuel_low")  →  condition(function(ctx) return ctx:fuel_low() end)
function M.when(method_name)
  return function(ctx)
    return ctx[method_name](ctx) and "success" or "failure"
  end
end

-- BT.do_("method_name")
-- Shorthand action that calls ctx:method_name() and returns its result.
--   BT.do_("patrol")  →  action(function(ctx) return ctx:patrol() end)
function M.do_(method_name)
  return function(ctx)
    return ctx[method_name](ctx)
  end
end

return M
