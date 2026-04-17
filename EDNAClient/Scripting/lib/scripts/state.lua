--[[
  scripts.state — Base self-updating state object

  Provides a lightweight foundation for MQTT-driven state objects.
  Handles topic subscription, JSON decoding, and field storage.
  Domain-specific state objects (e.g. scripts.drone) build on this.

  Usage:
    local State = require("scripts.state")
    local s = State.create()

    -- Bind a topic: fn(self, decoded_payload, raw_topic) updates self's fields
    s:bind("+/E/SomeTopic/+/+", function(self, data)
      self:set("x", data.Position.X)
      self:set("y", data.Position.Y)
    end)

    -- Read fields
    local x = s:get("x")   -- nil until first message arrives

  All updates happen on the WPF dispatcher thread (Lua's execution thread),
  so there are no race conditions between bind callbacks and field reads.
]]

local M = {}

function M.create()
  local self = { _data = {} }

  -- Subscribe to topicFilter. On each delivery, decode the JSON payload and
  -- call fn(self, data, topic). fn is responsible for calling self:set(...).
  -- Returns self for chaining.
  function self:bind(topicFilter, fn)
    mqtt.subscribe(topicFilter, function(topic, payload)
      local ok, data = pcall(json.parse, payload)
      if ok and data then fn(self, data, topic) end
    end)
    return self
  end

  function self:get(key)        return self._data[key] end
  function self:set(key, value) self._data[key] = value end
  function self:has(key)        return self._data[key] ~= nil end

  -- Merge a table of key→value pairs into _data in one call
  function self:merge(tbl)
    for k, v in pairs(tbl) do self._data[k] = v end
  end

  return self
end

return M
