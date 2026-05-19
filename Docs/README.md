# EmpyrionMQTT

A mod and companion application for **Empyrion - Galactic Survival** (Eleon Game Studios) that bridges the game's mod APIs to an MQTT message bus, enabling external services to read game state and issue commands in real time.

> Copyright &copy; 2023 L.Goodhind
> Licensed under the MIT License. See the LICENSE file in the repository root.

---

## What It Does

When deployed in the game's Mods folder, **ESB** (the game-side mod) connects to a local Mosquitto broker, registers event handlers, and publishes game events as structured MQTT messages. It also subscribes to request topics and dispatches inbound commands to the appropriate game API calls, returning responses via MQTT 5.0 correlation.

**EDNA** (Empyrion Data Network Assistant) is a companion WPF tray application that subscribes to the bus, displays a HUD overlay, and hosts a Lua scripting engine for automation.

External clients can be written in any language that supports MQTT. The game mod itself runs inside Unity/Mono on .NET 4.8; client-side code is unrestricted.

---

## Architecture

| Component | Target | Role |
|---|---|---|
| `ESB` | .NET 4.8 / game mod | Publishes events, handles inbound requests, runs inside Empyrion |
| `ESB.Messaging` | .NET 4.8 | Shared MQTT transport layer (Messenger, ParsedTopic, IMessenger) |
| `EDNA` | .NET 8 / WPF | Companion tray app; HUD overlay; Lua scripting host |
| `ESBTests` | .NET 4.8 | Integration and unit tests |

---

## Getting Started

1. Install [Mosquitto](https://mosquitto.org/) and verify it is working:

   ```
   mosquitto_sub -v -t "#"
   mosquitto_pub -t "Hello" -m "HelloWorld"
   ```

   The message should appear in the subscriber window. If not, fix the broker before proceeding.

2. Build the solution. Copy `ESB.dll`, `ESB.Messaging.dll`, and any required dependency DLLs to:

   ```
   {EmpyrionRoot}\Content\Mods\ESB\
   ```

3. Place a configured `ESB_Info.yaml` in the same directory. Minimum required fields:

   ```yaml
   MQTThost:
     WithTcpServer: localhost
     Port: 1883
   ```

4. Start the game and load a save. The mod will connect to the broker and begin publishing events. Subscribe with `mosquitto_sub -v -t "ESB/#"` to observe traffic.

---

## Topic Schema

All messages use the `ESB/` prefix and a fixed 6-segment structure:

```
ESB/{participantType}/{routingContextId}/{scope}/{msgType}/{operation}
```

`{participantType}` carries the **recipient type** for machine-targeted traffic (so subscribers can pin their own type at position 1 and only receive what is addressed to them), and the **sender's own type** for context-scoped events (subscribers wildcard position 1 on the context-evt sub so fan-out works).

`{routingContextId}` is a base-36 audience selector chosen at publish time, not a per-participant identity. Three kinds exist: **Machine** (5-char), **Lobby** (8-char, pre-game), and **Game** (8-char, in-game). Lobby and Game share width/shape and differ only by in-process `Kind`. Since one player = one Client = one machineId, the `ESB/Client/{machineId}/...` pattern is also the player-addressing pattern.

**Addressing rule:** `req`/`res`/`log` always target a specific recipient's MachineId. Events, retained Announcements (including Connect), and lifecycle hints (GameEnter, GameExit) publish to the participant's current **context rcId** -- Lobby for Client/EDNA before game entry, Game once in a game; Pfs/Ds are always Game.

`{msgType}` is one of `req`, `res`, `evt`, `log` (always lowercase). See [TopicSchema.md](TopicSchema.md) for the full specification: section 1 (topic format), section 5 (request/response and subscription patterns), section 11 (routing contexts).

---

## Broker Security

See [MosquittoSecurityGuide.md](MosquittoSecurityGuide.md) for ACL configuration, TLS setup, connection hardening, and the player invitation workflow for LAN and internet-hosted deployments.

---

## Documentation Index

| Document | Description |
|---|---|
| [TopicSchema.md](TopicSchema.md) | Full MQTT topic schema specification (canonical source for topic format and the `RoutingContextKind` taxonomy) |
| [Bus/README.md](Bus/README.md) | `IMessageBus` developer guide: setup, handler registration, publishing, requests, announcements |
| [Bus/message-bus-overview.md](Bus/message-bus-overview.md) | Bus model overview: envelope, dispatch, audience subscriptions |
| [EDNA/SkillsOverview.md](EDNA/SkillsOverview.md) | EDNA skills: subscriptions and bus traffic per skill |
| [OpenIssues.md](OpenIssues.md) | Active work items and deferred follow-ons |
| [MosquittoSecurityGuide.md](MosquittoSecurityGuide.md) | Broker security configuration (ACL section pending rework for the rcId model) |
| [Analysis/ApiTableOfContents.md](Analysis/ApiTableOfContents.md) | Empyrion mod API surface (DLL reflection) |
| [Analysis/V1ApiObjectModel.md](Analysis/V1ApiObjectModel.md) | V1 ModBase API object model |
