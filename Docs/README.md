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

4. Start the game and load a save. The mod will connect to the broker and begin publishing events. Subscribe with `mosquitto_sub -v -t "EMP/#"` to observe traffic.

---

## Topic Schema

All messages use the `EMP/` prefix with the following base structure:

```
EMP/{participantType}/{connectionId}/{dir}/{scope}/{operation}
```

| Segment | Values |
|---|---|
| `participantType` | `Client`, `Pfs`, `Ds`, `Agent` |
| `connectionId` | 4-char Base-36 ID assigned per connection |
| `dir` | `Req`, `Res`, `Evt`, `Err`, `Log` |
| `scope` | `App`, `Playfield`, `Player`, `Structure` |
| `operation` | `get/{prop}`, `set/{prop}`, `call/{method}`, `{eventName}`, `{cid}` |

Devices within a structure add a sub-scope segment:

```
EMP/{participantType}/{connectionId}/{dir}/Structure/Device/{deviceName}/{operation}
```

A single wildcard subscription covers all scopes and device depths:

```
EMP/+/{connectionId}/Req/#
```

See [TopicSchema.md](TopicSchema.md) for the full schema specification including examples, error handling, event patterns, and design notes.

---

## Broker Security

See [MosquittoSecurityGuide.md](MosquittoSecurityGuide.md) for ACL configuration, TLS setup, connection hardening, and the player invitation workflow for LAN and internet-hosted deployments.

---

## Documentation Index

| Document | Description |
|---|---|
| [TopicSchema.md](TopicSchema.md) | Full MQTT topic schema specification |
| [MosquittoSecurityGuide.md](MosquittoSecurityGuide.md) | Broker security configuration |
| [Analysis/ApiTableOfContents.md](Analysis/ApiTableOfContents.md) | Empyrion mod API surface (DLL reflection) |
| [Analysis/V1ApiObjectModel.md](Analysis/V1ApiObjectModel.md) | V1 ModBase API object model |
| [Plans/topic-restructure-plan.md](Plans/topic-restructure-plan.md) | Completed: EMP/ schema, dir-before-scope layout |
| [Plans/handler-alignment-plan.md](Plans/handler-alignment-plan.md) | Pending: ApplicationHandler and PlayerHandler style alignment |
| [Plans/StartupEventCapture.md](Plans/StartupEventCapture.md) | Pending: startup event queue to prevent dropped events |
