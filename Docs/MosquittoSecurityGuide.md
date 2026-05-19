# Mosquitto Security Guide -- Empyrion MQTT Integration

This guide aligns with the rcId model documented in `Docs/TopicSchema.md` sections 1, 5, and 11: three rcId kinds (Machine, Lobby, Game; Lobby and Game share width/shape on the wire), two standing subscriptions per participant (machine, and a context-evt sub where the context rcId swaps Lobby<->Game on game enter/exit), and a position-1 publisher contract that names the recipient type for machine-targeted req/res/log and the sender's own type for events. Req/res/log always uses MachineId addressing; Lobby and Game rcIds carry `evt` and Announcements (Connect, GameEnter, GameExit, and any other retained announcements).

> The ACL rules in Section 2 describe the intended end-state. The current code uses a single wildcard subscription `ESB/+/{myMachineId}/+/+/+` and puts the caller's own type at position 1 of requests, so a deployment running today's bus needs broader ACL rules than what is documented below until the code follow-on lands.

## Current Config Assessment

Your current `mosquitto.conf` provides a minimal but workable starting point:

| Setting | Status | Notes |
|---|---|---|
| `allow_anonymous false` | ✅ Good | Anonymous connections blocked |
| `password_file` | ✅ Good | Credential auth in place |
| `listener 1883` (plain TCP) | ⚠️ Cleartext | Acceptable for localhost/LAN; **must** be replaced or supplemented for internet |
| No ACL file | ⚠️ Gap | Any authenticated user can pub/sub to any topic |
| No connection limits | ⚠️ Gap | No protection against flooding |
| No MQTT version restriction | ⚠️ Minor | Allows v3.1 and v3.1.1 clients that won't support MQTT 5 features |
| Log types set | ✅ Good | error/warning/notice/information captured |

---

## Deployment Tier Matrix

| Concern | Singleplayer (localhost) | LAN Co-op | Internet Hosted |
|---|---|---|---|
| TLS | Not required | Optional but recommended | **Required** |
| ACL file | Not required | Recommended | **Required** |
| Connection limits | Not required | Recommended | **Required** |
| MQTT v5 only | Optional | Recommended | Recommended |
| Bind to interface | Not required | **Recommended** | **Required** |
| `check_retain_source` | Default (true) | Default | Verify true |

---

## 1. TLS (Internet Hosting)

Replace (or supplement) the plaintext port 1883 with an encrypted listener on 8883. The standard approach uses a self-signed CA for a private game deployment, or a Let's Encrypt certificate if the broker is on a public domain.

```conf
# Disable plaintext listener for internet hosting, or restrict it to loopback only
listener 1883 127.0.0.1
allow_anonymous false
password_file C:\mosquitto\passwd.dat

# Encrypted listener on standard MQTTS port
listener 8883
allow_anonymous false
password_file C:\mosquitto\passwd.dat
certfile  C:\mosquitto\certs\server.crt
keyfile   C:\mosquitto\certs\server.key
# Optional: require clients to present a certificate
# cafile  C:\mosquitto\certs\ca.crt
# require_certificate true
tls_version tlsv1.2
```

Generate a self-signed cert with OpenSSL:

```bash
# CA key and cert
openssl genrsa -out ca.key 4096
openssl req -new -x509 -days 3650 -key ca.key -out ca.crt -subj "/CN=EmpyrionMQTT-CA"

# Server key and CSR
openssl genrsa -out server.key 2048
openssl req -new -key server.key -out server.csr -subj "/CN=<your-server-hostname-or-ip>"

# Sign server cert with CA
openssl x509 -req -days 3650 -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out server.crt
```

> **Client connection string:** `mqtts://<host>:8883` — clients need to trust `ca.crt` to verify the server certificate.

> **TLS 1.3:** If all clients support it, prefer `tls_version tlsv1.3` to eliminate TLS 1.2 entirely.

---

## 2. ACL File

Without an ACL, any authenticated participant can publish to any topic -- including another player's response topic or a server-side command topic. The four standing subscriptions per participant (Section 5 of `Docs/TopicSchema.md`) map cleanly to ACL rules: each grant matches one of the subs on the read side, and each grant on the write side matches one entry of the publisher contract.

### Username convention

Encode the participant type and machineId in the Mosquitto username so pattern ACLs can scope each user to its own subtree. With `use_username_as_clientid true` (Section 5 below), the MQTT clientId must equal the username.

| Username form | Example | Notes |
|---|---|---|
| `{type}-{machineId}` | `Ds-abc12`, `Pfs-abc12`, `Client-k3m9p`, `EDNA-k3m9p` | Type matches the participantType string passed to `ConnectAsync`; machineId is the 5-char base-36 value from `bus.token`. In co-op, Ds/Pfs/Client all share the host machineId; their usernames differ only in the type prefix. |

The username is a credential identity, not a topic segment. The bus puts `{type}` at position 1 and `{machineId}` at position 2 of the topic; the ACL ties the username to the specific `(type, machineId)` pair using literal rules per user.

### Read-side grants (match the two subs)

Every participant has two subscriptions: its own type-pinned machine sub, and a context-evt sub whose rcId target swaps between Lobby and Game on game enter/exit. Pattern ACLs do not split a single `%u` into "type" and "machineId" parts, so the cleanest approach is per-user literal rules for the machine sub and a global pattern rule for the wildcard context-evt sub.

```
# ---------------------------------------------------------------
# Globally allowed reads -- the wildcard context-evt subscription
# ---------------------------------------------------------------
# Context-evt fan-out (every participant subscribes its current context rcId, which is either
# its Lobby rcId pre-game or the real Game rcId in-game). Lobby and Game rcIds share the same
# 8-char base-36 shape on the wire; ACLs cannot distinguish them. The broader form below allows
# any 8-char audience; tighten per game if you want gameId compartmentalisation.
topic read ESB/+/+/+/evt/+

# ---------------------------------------------------------------
# Per-user reads -- the type-pinned machine subscription
# ---------------------------------------------------------------
# Example for a Client whose machineId is k3m9p:
user Client-k3m9p
topic read ESB/Client/k3m9p/#       # machine sub: req/res/log addressed to me
```

Req/res/log is always machine-targeted, so the only per-user read grant beyond the global context-evt rule is the participant's own machine subtree.

### Write-side grants (match the publisher contract)

The publisher contract in Section 5 of `Docs/TopicSchema.md` enumerates the four publish cases. Each maps to a write grant:

```
# ---------------------------------------------------------------
# Req/res/log to a specific recipient (machine-targeted).
# A Client may write to any Pfs/Ds/EDNA at any machineId, but should NOT write to
# other Clients' machine subtrees (no rule = no access).
# ---------------------------------------------------------------
user Client-k3m9p
topic write ESB/Pfs/+/+/req/+       # outbound requests to Pfs at any machineId
topic write ESB/Ds/+/+/req/+
topic write ESB/EDNA/+/+/req/+
topic write ESB/Client/k3m9p/+/+/+  # own machine subtree (responses, log)
                                    # %c / %u substitution alternative below

# ---------------------------------------------------------------
# Context-scoped events (position 1 = sender's own type, position 2 = Lobby or Game rcId).
# Only participants who are *in* a game (or lobby) should be able to publish events for it.
# Connect, GameEnter, GameExit, and any Announcements all flow through this rule -- there is
# no separate broadcast tier.
# Per-context dynamic rule (added when the user is granted access to a context):
# topic write ESB/Client/{thatContextRcId}/+/evt/+
# ---------------------------------------------------------------
```

### Pattern-based variant (less verbose)

If you would rather avoid per-user blocks, use Mosquitto's `%u` substitution. This trades precision for brevity: %u is the whole username, so `ESB/{type}/{machineId}/#` becomes `ESB/+/{machineId-from-username}/#` and requires the username to be parseable -- or you accept a per-username rule mapping the username verbatim into the topic:

```
# Each user owns its own ESB/<username>/# subtree if you name the topic to match.
# Not a fit for the {type}/{machineId} schema unless the username equals one of those segments.
# Mostly useful in single-type deployments (e.g. all-Client) where the type is implicit.
pattern readwrite ESB/+/+/+/+/+
```

In practice the per-user block style above is what scales, because the schema's *two* identity-bearing segments (type and machineId) do not collapse into a single substitution token.

Add the file to `mosquitto.conf`:

```conf
acl_file C:\mosquitto\acl.conf
```

> **Note on `$SYS` topics:** By default, no user has access to `$SYS/#`. If you want broker stats (connection counts, message throughput), explicitly grant a monitoring user read access to `$SYS/#`.

> **Dynamic ACL updates.** Compartmentalising by gameId requires re-issuing ACL rules when participants join/leave games. Mosquitto reloads `acl_file` on SIGHUP (Linux) or service restart (Windows). For per-connect dynamic ACLs, see `mosquitto-go-auth` or the dynamic security plugin in Section 9 (Future Expansion).

---

## 3. Listener Hardening

### Bind to a Specific Interface

For LAN or internet hosting, bind listeners to the correct interface rather than all interfaces. This prevents the broker from accidentally accepting connections on unexpected network paths.

```conf
# LAN: bind to local network interface only
listener 1883 192.168.1.100

# Internet: bind to public-facing interface; TLS required
listener 8883 0.0.0.0
```

### Restrict to MQTT 5 Only

Your schema is built on MQTT 5.0 (`Response Topic` and `Correlation Data` are protocol-level properties). Allowing 3.1.1 clients means they silently lose these features.

```conf
listener 8883
accept_protocol_versions 5
```

### Connection Limits

Protect against connection flooding. A typical Empyrion game has a bounded player count.

```conf
# Per-listener max (e.g. 32-player server with some headroom for pfs/agents)
max_connections 64

# Global hard cap across all listeners
global_max_connections 128

# Bound inflight messages per client
max_inflight_messages 20
max_queued_messages 200
```

### Packet Size Limit

Game state payloads should be compact. A generous upper bound prevents accidental or malicious large message injection.

```conf
# 64 KB is generous for any emp/ payload; adjust if inventory payloads are larger
max_packet_size 65536
message_size_limit 65536
```

### Keepalive Enforcement

Ensures dead connections are detected and cleaned up in a timely manner.

```conf
# Reject clients requesting keepalive > 120s; forces them to reconnect
max_keepalive 120
```

---

## 4. Retained Message Security

The schema uses retained messages for `Announcements` (Connect presence, per-game reference data). `check_retain_source` is true by default and should be left that way -- it ensures that if a participant's credentials are revoked, their retained Announcement will not be re-delivered to new subscribers.

```conf
# Confirm this is set (it is the default, but worth making explicit)
check_retain_source true
```

Also consider setting a session expiry to clean up stale Announcement entries from participants that disconnected ungracefully:

```conf
persistent_client_expiration 1h
```

---

## 5. MQTT v5 Username-as-ClientID Locking

Prevent a rogue client from using another participant's MQTT clientId (which could interfere with session state). By enforcing username as the client ID, credentials and identity are tied together.

```conf
# Under the listener definition
use_username_as_clientid true
```

This means the MQTT `clientId` used in the connection must equal the `username`. Your participants should be provisioned accordingly (e.g., username `Pfs-abc12` connects with clientId `Pfs-abc12`).

---

## 6. Logging Enhancements

Enhance the current logging for production visibility, especially for internet-hosted servers.

```conf
log_dest file C:\mosquitto\mosquitto.log

# Include subscribe/unsubscribe for ACL debugging; remove in production if noisy
log_type error
log_type warning
log_type notice
log_type information
log_type subscribe
log_type unsubscribe

# Timestamp format for log correlation with game events
log_timestamp true
log_timestamp_format %Y-%m-%dT%H:%M:%S

# Log connect/disconnect events — important for security audit trails
connection_messages true
```

---

## 7. `per_listener_settings` Consideration

If you want different security profiles per listener (e.g., a plaintext loopback listener for local game processes, and a TLS listener for remote players), enable `per_listener_settings`. This isolates `password_file`, `acl_file`, and `allow_anonymous` on a per-listener basis.

```conf
# Must appear before any listener definitions
per_listener_settings true

# Local listener for pfs/ds processes on the same machine (no TLS needed)
listener 1883 127.0.0.1
allow_anonymous false
password_file C:\mosquitto\passwd-local.dat
acl_file      C:\mosquitto\acl-local.conf

# External listener for remote clients
listener 8883
allow_anonymous false
password_file C:\mosquitto\passwd-remote.dat
acl_file      C:\mosquitto\acl-remote.conf
certfile  C:\mosquitto\certs\server.crt
keyfile   C:\mosquitto\certs\server.key
tls_version tlsv1.2
accept_protocol_versions 5
max_connections 64
```

This lets you keep `ds` and `pfs` credentials off the external-facing listener entirely.

---

## 8. `$SYS` Topic Exposure

By default `$SYS/#` topics broadcast broker statistics (client counts, message rates, uptime). On a public-facing broker this leaks operational information. Either disable them or restrict them explicitly.

```conf
# Disable $SYS entirely
sys_interval 0

# Or keep them but ensure no ACL rule grants external clients read access to $SYS/#
```

---

## Quick Reference: Config Additions by Tier

### Singleplayer (no change required beyond current)
Your current config is sufficient. Optionally add an ACL for defense-in-depth.

### LAN Co-op — additions to current config
```conf
listener 1883 <LAN IP>   # bind to LAN interface only
allow_anonymous false
password_file C:\mosquitto\passwd.dat
acl_file      C:\mosquitto\acl.conf
accept_protocol_versions 5
max_connections 64
connection_messages true
check_retain_source true
```

---

## 9. Player Invitation System

### Concept Overview

The self-signed CA approach enables a lightweight invitation workflow: the host controls a CA and credential store, and onboarding a new player is a scripted operation that ends with an email containing everything they need to connect. Revoking access is equally scripted — no cert revocation required since clients authenticate via password, not client certificates.

```
┌─────────────────────────────────────────────────────────────┐
│                     HOST MACHINE                            │
│                                                             │
│  ┌──────────────┐    ┌────────────────────────────────┐    │
│  │  CA Store    │    │  Provisioning Script           │    │
│  │  ca.crt      │    │  - generate username/password  │    │
│  │  ca.key ──── │───▶│  - append to passwd.dat        │    │
│  └──────────────┘    │  - append to acl.conf          │    │
│                      │  - package invite bundle        │    │
│                      │  - send email                   │    │
│                      │  - SIGHUP mosquitto             │    │
│                      └──────────────┬───────────────── ┘   │
│                                     │                       │
│  ┌──────────────────────────────────▼──────────────────┐   │
│  │  Mosquitto                                          │   │
│  │  passwd.dat  ◀── hot reload on SIGHUP               │   │
│  │  acl.conf    ◀── hot reload on SIGHUP               │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                    invite bundle (email)
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     PLAYER MACHINE                          │
│                                                             │
│  bundle contains:                                           │
│  - ca.crt          (trust anchor, safe to distribute)       │
│  - username        (their MQTT clientId)                    │
│  - password        (one-time or permanent)                  │
│  - host + port     (broker endpoint)                        │
│  - install notes   (where to put the files)                 │
└─────────────────────────────────────────────────────────────┘
```

### Future Expansion Points

The manual script is intentionally the simplest viable implementation. Each point below is an independent upgrade that can be added later without redesigning the core flow.

**Short-term refinements**
- Replace plain email attachment with a short-lived signed download link (e.g. pre-signed S3 URL, or a small self-hosted token endpoint) so credentials aren't sitting in email inboxes
- Add a `revoke-player.ps1` counterpart to the provisioning script — removes ACL entry, removes password entry, sends SIGHUP
- Store provisioned usernames in a simple JSON registry so the host can list who has access

**Medium-term**
- Generate a per-player one-time password that the client mod exchanges on first connect for a session credential — prevents static passwords sitting in config files
- Add a small web UI (local to the host machine) for the host to manage invites, view active connections via `$SYS` topics, and revoke players with a button click

**Longer-term / production scale**
- Replace `passwd.dat` + `acl.conf` with `mosquitto-go-auth` plugin backed by a database — enables dynamic credential and ACL management without file reloads
- Issue short-lived JWTs as credentials — players re-authenticate through a game lobby or auth endpoint; the broker validates the JWT signature rather than a stored hash
- CI/CD pipeline for broker config — changes to ACL rules go through version control and deploy automatically

### Dev Setup Instructions

These steps get the minimum infrastructure running on your local machine so you can develop and test the invitation flow end to end. Everything here is intentionally simple — the goal is working infrastructure, not production hardening.

#### Prerequisites

- Mosquitto 2.1.2 installed (`C:\mosquitto\`)
- OpenSSL available on PATH (ships with Git for Windows)
- PowerShell 5.1+

#### Step 1 — Directory Layout

Create the following structure. Keep it separate from the Mosquitto install directory so it can be version-controlled independently.

```
C:\empyrion-mqtt\
  certs\
  credentials\
    passwd.dat        ← generated by mosquitto_passwd
    acl.conf          ← hand-edited or generated
  scripts\
    New-PlayerInvite.ps1
    Revoke-Player.ps1
  invites\            ← staging area for outgoing bundles
```

#### Step 2 — Generate the CA and Server Certificate

Run once. Keep `ca.key` private — it is the root of trust for your entire deployment.

```powershell
cd C:\empyrion-mqtt\certs

# CA
openssl genrsa -out ca.key 4096
openssl req -new -x509 -days 3650 -key ca.key -out ca.crt `
  -subj "/CN=EmpyrionMQTT-CA"

# Server key and CSR (replace localhost with your LAN IP or hostname for LAN/internet use)
openssl genrsa -out server.key 2048
openssl req -new -key server.key -out server.csr `
  -subj "/CN=localhost"

# Sign server cert
openssl x509 -req -days 3650 -in server.csr `
  -CA ca.crt -CAkey ca.key -CAcreateserial -out server.crt
```

#### Step 3 — Create the Initial passwd.dat and ACL

Create an empty passwd.dat and seed it with a Ds account for the DedicatedServer process. Use the host's machineId (5-char base-36 from `%ProgramData%\EmpyrionESB\bus.token`) as the suffix so the username matches the topic schema:

```powershell
# Create passwd.dat with first user (will prompt for password)
mosquitto_passwd -c C:\empyrion-mqtt\credentials\passwd.dat Ds-abc12
```

Create `C:\empyrion-mqtt\credentials\acl.conf` with the server-side entries from Section 2 of this guide. Leave the client section empty for now -- the provisioning script will append to it.

#### Step 4 — Dev mosquitto.conf

Point Mosquitto at the dev infrastructure. This config is for local development only — plaintext loopback plus a TLS listener for testing the invite flow.

```conf
# C:\mosquitto\mosquitto-dev.conf

per_listener_settings true

# Loopback plaintext for local server processes
listener 1883 127.0.0.1
allow_anonymous false
password_file C:\empyrion-mqtt\credentials\passwd.dat
acl_file      C:\empyrion-mqtt\credentials\acl.conf

# TLS listener for testing client connections
listener 8883
allow_anonymous false
password_file C:\empyrion-mqtt\credentials\passwd.dat
acl_file      C:\empyrion-mqtt\credentials\acl.conf
certfile  C:\empyrion-mqtt\certs\server.crt
keyfile   C:\empyrion-mqtt\certs\server.key
tls_version tlsv1.2
accept_protocol_versions 5
use_username_as_clientid true
max_connections 32

# Logging
log_dest file C:\mosquitto\mosquitto-dev.log
log_type all
log_timestamp true
log_timestamp_format %Y-%m-%dT%H:%M:%S
connection_messages true
```

Start Mosquitto with this config:

```powershell
mosquitto -c C:\mosquitto\mosquitto-dev.conf
```

#### Step 5 — Provisioning Script (Skeleton)

`C:\empyrion-mqtt\scripts\New-PlayerInvite.ps1` — fills in the invite bundle. Refine as needed.

```powershell
param(
    [Parameter(Mandatory)] [string] $MachineId,     # 5-char base-36 from the player's bus.token
    [Parameter(Mandatory)] [string] $PlayerEmail,
    [string] $BrokerHost = "localhost",
    [string] $BrokerPort = "8883"
)

$username    = "Client-$MachineId"
$passwdFile  = "C:\empyrion-mqtt\credentials\passwd.dat"
$aclFile     = "C:\empyrion-mqtt\credentials\acl.conf"
$caCert      = "C:\empyrion-mqtt\certs\ca.crt"
$inviteDir   = "C:\empyrion-mqtt\invites\$username"

# 1. Generate password
$password = [System.Web.Security.Membership]::GeneratePassword(24, 4)

# 2. Add to passwd.dat
echo "$password" | mosquitto_passwd -b $passwdFile $username $password

# 3. Append ACL entry. Mirrors the two-subscription model from Section 2:
#    - read: own machine subtree, context-evt wildcard
#    - write: req to Pfs/Ds/EDNA at any rcId; own machine subtree; context-scoped evts
$aclEntry = @"

# Client $MachineId -- provisioned $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
user $username
topic read      ESB/Client/$MachineId/#
topic read      ESB/Client/+/#
topic read      ESB/+/+/+/evt/+
topic write     ESB/Client/$MachineId/#
topic write     ESB/Pfs/+/+/req/+
topic write     ESB/Ds/+/+/req/+
topic write     ESB/EDNA/+/+/req/+
topic write     ESB/Client/+/+/evt/+
"@
Add-Content -Path $aclFile -Value $aclEntry

# 4. Package invite bundle
New-Item -ItemType Directory -Force -Path $inviteDir | Out-Null
Copy-Item $caCert "$inviteDir\ca.crt"
@{
    host      = $BrokerHost
    port      = $BrokerPort
    username  = $username
    password  = $password
    machineId = $MachineId
} | ConvertTo-Json | Set-Content "$inviteDir\connection.json"

# 5. Reload Mosquitto (Windows service)
# For dev: find the mosquitto PID and signal it, or just restart
# In production on Linux: kill -HUP $(cat /var/run/mosquitto.pid)
Write-Host "TODO: reload mosquitto (SIGHUP or service restart)"
Write-Host "Invite bundle staged at: $inviteDir"
Write-Host "Email $PlayerEmail with contents of $inviteDir"

# 6. TODO: Send email
```

> On Windows, Mosquitto does not support `SIGHUP`. For dev, restart the service manually or use the `$CONTROL/broker/` API if enabled. On Linux this becomes `kill -HUP`.

#### Step 6 — Verify the Setup

Use `mosquitto_sub` and `mosquitto_pub` to confirm TLS and ACL are working:

```powershell
# Should connect and receive messages (Ds has broad read access; see Section 2)
mosquitto_sub -h localhost -p 8883 --cafile C:\empyrion-mqtt\certs\ca.crt `
  -u Ds-abc12 -P <password> -t "ESB/#" -v

# Should be rejected (ACL test: Client-k3m9p tries to write to a different Client's machine subtree)
mosquitto_pub -h localhost -p 8883 --cafile C:\empyrion-mqtt\certs\ca.crt `
  -u Client-k3m9p -P <password> -t "ESB/Client/xyzab/App/evt/test" -m "test"
```

---

### Internet Hosted — replace current config
```conf
per_listener_settings true

listener 1883 127.0.0.1   # loopback only for local server processes
allow_anonymous false
password_file C:\mosquitto\passwd-local.dat
acl_file      C:\mosquitto\acl-local.conf

listener 8883              # public TLS listener for remote players/agents
allow_anonymous false
password_file C:\mosquitto\passwd-remote.dat
acl_file      C:\mosquitto\acl-remote.conf
certfile  C:\mosquitto\certs\server.crt
keyfile   C:\mosquitto\certs\server.key
tls_version tlsv1.2
accept_protocol_versions 5
use_username_as_clientid true
max_connections 64
max_inflight_messages 20
max_queued_messages 200
max_packet_size 65536
max_keepalive 120

# Global
log_dest file C:\mosquitto\mosquitto.log
log_type error
log_type warning
log_type notice
log_type information
log_type subscribe
log_type unsubscribe
log_timestamp true
log_timestamp_format %Y-%m-%dT%H:%M:%S
connection_messages true
check_retain_source true
sys_interval 0
persistent_client_expiration 1h
```
