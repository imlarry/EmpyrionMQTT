# Topic Schema Restructure: Move {dir} Before {scope} [SUPERSEDED]

> This plan was NOT adopted. The canonical format is scope-first:
> `ESB/{participantType}/{connectionId}/{scope}/{msgType}/{operation}`
> See TopicSchema.md for the current definition.

## Problem

The current schema places `{scope}` before `{dir}`, which forces two MQTT broker subscriptions at startup. The Device sub-scope inserts `Structure/Device/{name}` between `{connId}` and `{dir}`, so a single wildcard cannot cover both standard and device topics:

```
ESB/+/{connId}/+/Req/#                    -- misses 8-segment device topics
ESB/+/{connId}/Structure/Device/+/Req/#   -- required second subscription
```

The `RegisteredHandlers` log entry is identical in both Subscribed log messages because `AvailableTopics()` returns the full handler registry regardless of which filter was just subscribed -- this makes it look like duplication, but the two subscriptions are genuinely needed under the current layout.

## Proposed Schema

Move `{dir}` to position 4 (before `{scope}`), making direction the fixed pivot. Scope and any sub-scope hierarchy follow as a variable-depth suffix.

**Standard (6 segments):**
```
ESB/{participantType}/{connectionId}/{dir}/{scope}/{operation}
```

**Device sub-scope (8 segments):**
```
ESB/{participantType}/{connectionId}/{dir}/Structure/Device/{deviceName}/{operation}
```

**Event:**
```
ESB/{participantType}/{connectionId}/Evt/{scope}/{eventName}
```

**Response / Error:**
```
ESB/{participantType}/{connectionId}/Res/{scope}/{cid}
ESB/{participantType}/{connectionId}/Err/{scope}/{cid}
```

**Log:**
```
ESB/{participantType}/{connectionId}/Log/App/{level}
```

**Registry (no change):**
```
ESB/Registry/{connectionId}
```

## Subscription Improvement

One subscription now covers all scopes and all sub-scope depths:

```
ESB/+/{connId}/Req/#
```

Additional wildcard patterns that become natural:

```
ESB/+/{connId}/Evt/#           -- all events from this participant
ESB/Pfs/+/Evt/Player/#         -- all player events across all Pfs
ESB/+/+/Req/Structure/#        -- all structure requests from any participant
ESB/+/+/Log/#                  -- all log messages
```

## Tradeoffs

- Full breaking change: every topic string in ESB, EDNAClient, schema doc, and external tooling (MQTT Explorer filters, Mosquitto ACL rules) must be updated in one pass.
- `MqttRequester` uses a legacy `Client/Q/...` schema entirely separate from ESB/ -- not affected.
- The `Dir` segment at position 4 is a better structural pivot than `Scope`: it has a small closed vocabulary (Req / Res / Evt / Err / Log) and is the natural "kind of traffic" discriminator for both subscriber filtering and handler dispatch.

## Dispatch Key Convention After Change

Format: `{dir}/{scope}/{op}` for standard; `{dir}/Structure/Device/*/{op}` for device wildcard.

```
Req/App/get/GameTicks
Req/Structure/get/Info
Req/Structure/Device/*/get/Lcd
Evt/App/ChatMessageSent
Evt/Playfield/EntityLoaded
```

## Files to Change

### ESB.Messaging

**ParsedTopic.cs**
- Update property-order comments to match new segment order.
- `DispatchKey` format changes to `{dir}/{scope}/{op}`.

**Messenger.cs**
- `ParseTopic()`: device detection changes from `p[3]=="Structure" && p[4]=="Device"` to `p[4]=="Structure" && p[5]=="Device"`. Standard path: `p[3]` is now `Dir`, `p[4]` is `Scope`. DispatchKey for standard: `$"{p[3]}/{p[4]}/{op}"`. DispatchKey for device: `$"{p[3]}/Structure/Device/{p[5]}/{op}"`.
- `ProcessMessageAsync`: wildcard fallback key changes to `$"{pt.Dir}/Structure/Device/*/{pt.Operation}"`.
- `LogAsync`: topic becomes `ESB/Messenger/{clientId}/Log/App/{operation}`.
- Will topic: `ESB/Registry/{clientId}` (prefix only; no structural change).

### ESB (server)

**BusService/SubscriptionHandler.cs**
- Replace two `SubscribeBrokerAsync` calls with one: `$"ESB/+/{connId}/Req/#"`.

**BusService/BusManager.cs**
- `ESB/Registry/` -> `ESB/Registry/` in `PublishRegistryEntryAsync` and `ClearRegistryEntryAsync`.

**EventHandlers/HandlerBase.cs**
- `EmitEventAsync`: `$"ESB/{type}/{connId}/{scope}/Evt/{eventName}"` -> `$"ESB/{type}/{connId}/Evt/{scope}/{eventName}"`.
- `Execute` error topic: `$"ESB/{type}/{connId}/App/Err/{...}"` -> `$"ESB/{type}/{connId}/Err/App/{...}"`.

**TopicHandlers/HandlerHelper.cs**
- `ReplyErrorAsync` error topic for standard case: swap `{scope}/Err` -> `Err/{scope}`.
- Device case: `Structure/Device/{name}/Err/{op}` -> `Err/Structure/Device/{name}/{op}`.

**TopicHandlers/ApplicationHandler.cs**
- All `RegisterHandler` dispatch keys: `App/Req/get/X` -> `Req/App/get/X`.
- Any hardcoded publish topics (e.g. DialogResponse event): reorder segments.

**TopicHandlers/PlayerHandler.cs**
- All dispatch keys: `Player/Req/get/X` -> `Req/Player/get/X`.

**TopicHandlers/StructureHandler.cs**
- Standard dispatch keys: `Structure/Req/get/Info` -> `Req/Structure/get/Info`.
- Device dispatch keys: `Structure/Device/*/Req/get/Lcd` -> `Req/Structure/Device/*/get/Lcd`.

### EDNAClient

**Skills/Scripting/Api/LuaMqttApi.cs**
- Hardcoded log topics: `ESB/{participantType}/{connId}/App/Log/LuaMqttApi.*` -> `ESB/{participantType}/{connId}/Log/App/LuaMqttApi.*`.

**Skills/Scripting/Api/LuaLogApi.cs**
- Check for any hardcoded topic strings; apply same reorder.

**Helpers/MqttRequester.cs**
- Uses legacy `Client/Q/...` schema. Not affected.

### Docs

**Docs/EsbMqttSchema.md**
- Update all topic format tables and examples to new segment order.
- Rename prefix `ESB/` -> `ESB/` in all examples (the format spec already uses `ESB/`; examples were inconsistent).
