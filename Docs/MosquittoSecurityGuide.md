# Mosquitto Security Guide — Empyrion MQTT Integration

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

The `EMP/` topic schema creates a clear access pattern. Without an ACL, any authenticated participant can publish to any topic — including another player's `Res` topic or the `Ds` command topics.

### Request/Response Access Model

The schema has an inherent tension: requesters publish to the *responder's* `req` topic. This means no simple "only write to your own subtree" rule applies. The practical ACL strategy is role-based:

- **`ds` / `pfs`** — server-side processes; need broad write access
- **`client`** — player sessions; should only publish requests and their own registry entry
- **`agent`** — autonomous processes; scoped to their declared purpose

### Recommended ACL Strategy

Map each participant type to a **username prefix** in your `passwd.dat`:

| Username | Role |
|---|---|
| `ds-main` | DedicatedServer process |
| `pfs-akua`, `pfs-omicron` | PlayfieldServer per playfield |
| `client-<playerId>` | Each connecting player |
| `agent-edna`, `agent-*` | Named agents |

Then in your `acl_file`:

```
# ---------------------------------------------------------------
# DedicatedServer — full bus access
# ---------------------------------------------------------------
user ds-main
topic readwrite EMP/#
topic readwrite EMP/Registry/#

# ---------------------------------------------------------------
# PlayfieldServer — full access to own connection subtree;
# can read requests directed to any Pfs; can write responses anywhere
# ---------------------------------------------------------------
user pfs-akua
topic readwrite EMP/Pfs/conn-pfs-akua/#
topic read      EMP/Registry/#
topic write     EMP/Registry/conn-pfs-akua
# Responses go to requester's Res topic -- Pfs must be able to write there
topic write     EMP/Client/+/#
topic write     EMP/Agent/+/#
topic write     EMP/Ds/+/#

# ---------------------------------------------------------------
# Clients -- can send requests to Pfs/Ds; own their Res/Err/Log/Evt topics
# Use %u to scope each client to their own connectionId subtree
# ---------------------------------------------------------------
# Pattern ACLs apply to all users -- scope clients to their own connection namespace
pattern readwrite EMP/Client/%u/#
# Clients need to write their registry entry and read all
pattern write EMP/Registry/%u

# Clients can send requests to server-side participants
# Schema: EMP/{type}/{connId}/Req/{scope}/{op}
topic write EMP/Pfs/+/Req/#
topic write EMP/Ds/+/Req/#
# Clients can read the registry to discover participants
topic read  EMP/Registry/#
# Clients should NOT be able to write to other clients' topics
# (no rule = no access)

# ---------------------------------------------------------------
# Agents -- scoped to their own subtree; can read/write broadly for automation
# ---------------------------------------------------------------
user agent-edna
topic readwrite EMP/Agent/conn-edna-01/#
topic read      EMP/Pfs/+/#
topic read      EMP/Ds/+/#
topic read      EMP/Client/+/#
topic write     EMP/Pfs/+/Req/#
topic write     EMP/Ds/+/Req/#
topic write     EMP/Registry/conn-edna-01
topic read      EMP/Registry/#
```

Add to `mosquitto.conf`:

```conf
acl_file C:\mosquitto\acl.conf
```

> **Note on `$SYS` topics:** By default, no user has access to `$SYS/#`. If you want broker stats (connection counts, message throughput), explicitly grant a monitoring user read access to `$SYS/#`.

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

The schema uses retained messages for the `EMP/Registry/#` subtree. `check_retain_source` is true by default and should be left that way — it ensures that if a participant's credentials are revoked, their retained registry entry will not be re-delivered to new subscribers.

```conf
# Confirm this is set (it is the default, but worth making explicit)
check_retain_source true
```

Also consider setting a session expiry to clean up stale registry entries from participants that disconnected ungracefully:

```conf
persistent_client_expiration 1h
```

---

## 5. MQTT v5 Username-as-ClientID Locking

Prevent a rogue client from using another participant's `connectionId` as their MQTT client ID (which could interfere with session state). By enforcing username as the client ID, credentials and identity are tied together.

```conf
# Under the listener definition
use_username_as_clientid true
```

This means the MQTT `clientId` used in the connection must equal the `username`. Your participants should be provisioned accordingly (e.g., username `conn-pfs-akua` connects with clientId `conn-pfs-akua`).

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
│  - username        (their connectionId / clientId)          │
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

Create an empty passwd.dat and seed it with a `ds` account for the DedicatedServer process:

```powershell
# Create passwd.dat with first user (will prompt for password)
mosquitto_passwd -c C:\empyrion-mqtt\credentials\passwd.dat ds-main
```

Create `C:\empyrion-mqtt\credentials\acl.conf` with the server-side entries from Section 2 of this guide. Leave the client section empty for now — the provisioning script will append to it.

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
    [Parameter(Mandatory)] [string] $PlayerId,      # e.g. "7"
    [Parameter(Mandatory)] [string] $PlayerEmail,
    [string] $BrokerHost = "localhost",
    [string] $BrokerPort = "8883"
)

$username    = "client-$PlayerId"
$connId      = "conn-client-$PlayerId"
$passwdFile  = "C:\empyrion-mqtt\credentials\passwd.dat"
$aclFile     = "C:\empyrion-mqtt\credentials\acl.conf"
$caCert      = "C:\empyrion-mqtt\certs\ca.crt"
$inviteDir   = "C:\empyrion-mqtt\invites\$username"

# 1. Generate password
$password = [System.Web.Security.Membership]::GeneratePassword(24, 4)

# 2. Add to passwd.dat
echo "$password" | mosquitto_passwd -b $passwdFile $username $password

# 3. Append ACL entry
$aclEntry = @"

# Player $PlayerId -- provisioned $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
user $username
topic readwrite EMP/Client/$connId/#
topic write     EMP/Registry/$connId
topic read      EMP/Registry/#
topic write     EMP/Pfs/+/Req/#
topic write     EMP/Ds/+/Req/#
"@
Add-Content -Path $aclFile -Value $aclEntry

# 4. Package invite bundle
New-Item -ItemType Directory -Force -Path $inviteDir | Out-Null
Copy-Item $caCert "$inviteDir\ca.crt"
@{
    host     = $BrokerHost
    port     = $BrokerPort
    username = $username
    password = $password
    connId   = $connId
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
# Should connect and receive messages
mosquitto_sub -h localhost -p 8883 --cafile C:\empyrion-mqtt\certs\ca.crt `
  -u ds-main -P <password> -t "EMP/#" -v

# Should be rejected (wrong user for this topic -- ACL test)
mosquitto_pub -h localhost -p 8883 --cafile C:\empyrion-mqtt\certs\ca.crt `
  -u client-7 -P <password> -t "EMP/Client/conn-client-99/Evt/App/test" -m "test"
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
