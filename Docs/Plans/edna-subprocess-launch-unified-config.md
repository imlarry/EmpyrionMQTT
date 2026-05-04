# Plan: ESB Subprocess Launch + Unified Config + EDNA Startup State Machine

## Context

EDNAClient and an ESB Client process are always a matched pair on the same machine. The
current model requires manual EDNA launch and has no offline capability. This plan:

1. Has BusManager.Init() launch EDNAClient as a subprocess when running as Client, and
   kill it on Shutdown().
2. Absorbs EDNA_Info.yaml into ESB_Info.yaml as an EDNA: block (single config file).
3. Makes EDNA autonomous -- she finds ESB_Info.yaml herself via the Steam registry anchor,
   with no dependency on being launched by ESB.
4. Introduces a startup state machine so EDNA knows what situation she is in and can respond
   appropriately. Wizard flows are stubbed with PLAN.md files for future stages.

---

## New Folder Structure

Changes to the solution tree. Only new folders are listed; existing folders are unchanged.

```
EmpyrionMQTT/
  EDNAClient/
    Configuration/          -- existing, extended (WellKnownPaths, EsbInfo)
    Core/                   -- existing, EdnaService updated
    Startup/                -- NEW: state detection, Steam discovery
      StartupState.cs
      StartupOrchestrator.cs
      SteamLocator.cs
      PLAN.md
    Setup/                  -- NEW: wizard stubs only
      ISetupStep.cs
      EsbInstaller.cs
      MosquittoInstaller.cs
      PLAN.md
    Tray/                   -- existing
    Skills/                 -- existing
    Workspace/              -- existing
  ESB/
    BusService/             -- BusManager.cs updated
    Configuration/          -- ESB_Info.yaml updated only
```

---

## Part 1: BusManager -- Subprocess Launch and Lifecycle

**File:** ESB\BusService\BusManager.cs

### New field

```csharp
private System.Diagnostics.Process _ednaProcess;
```

### LaunchEdnaClient() -- new private method

```csharp
private void LaunchEdnaClient()
{
    string ednaExe = Path.Combine(ESBModPath, "EDNA", "EDNAClient.exe");
    if (!File.Exists(ednaExe))
        return;
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName        = ednaExe,
        UseShellExecute = true
    };
    _ednaProcess = System.Diagnostics.Process.Start(psi);
}
```

EDNAClient has single-instance mutex protection (Local\EDNA_SingleInstance), so double-launch
is safe. Assumed deploy path: ESBModPath\EDNA\EDNAClient.exe (confirm before implementing).

### Init() -- add launch call

After _eMgr.EnableEventHandlers():

```csharp
if (ParticipantType == "Client")
    LaunchEdnaClient();
```

### Shutdown() -- add EDNA kill

```csharp
public async Task Shutdown()
{
    if (_ednaProcess != null && !_ednaProcess.HasExited)
        _ednaProcess.Kill();
    _ednaProcess = null;

    _eMgr.DisableEventHandlers();
    await _ctx.Messenger.DisconnectAsync();
}
```

Use Kill() for immediate teardown. CloseMainWindow() is available if graceful WPF OnExit
is needed later.

---

## Part 2: Unified Config -- ESB_Info.yaml absorbs EDNA_Info.yaml

ESB reads ESB_Info.yaml via YamlFileReader which already sets IgnoreUnmatchedProperties.
The EDNA: block is silently skipped by ESB. No ESB model changes are needed -- the file
is the only coupling point between the two processes.

### ESB_Info.yaml -- add EDNA block

**File:** ESB\Configuration\ESB_Info.yaml

```yaml
MQTThost: { WithTcpServer: "localhost", Port: 1883, Username: "esbuser", Password: "esbpass" }

EDNA:
  EnabledSkillIds:
  - ThreatRadar
  - FloorMap
  - ScriptEditor
  - GalaxyMap
  DetailEnabled: false
```

### EsbInfo.cs -- add EDNA property

**File:** EDNAClient\Configuration\EsbInfo.cs

```csharp
public EdnaInfo EDNA { get; set; } = new EdnaInfo();
```

IgnoreUnmatchedProperties is already set on the EDNAClient deserializer, so EDNA: block
is picked up automatically.

### EdnaService.cs -- read skills from esbInfo.EDNA

**File:** EDNAClient\Core\EdnaService.cs

Replace the separate EDNA_Info.yaml load with:

```csharp
var esbInfo = WellKnownPaths.LoadEsbInfo();
var mqtt    = esbInfo?.MQTThost ?? new MqttConnectionSettings();
_settings   = esbInfo?.EDNA ?? new EdnaInfo();
await _ctx.Messenger.ConnectAsync(_ctx, "EDNA",
    mqtt.WithTcpServer, mqtt.Port, mqtt.Username, mqtt.Password, mqtt.CAFilePath);
```

Update the save call site from SaveInfo(EdnaInfoFile, ...) to SaveEdnaSettings(_settings).

### WellKnownPaths.cs -- add SaveEdnaSettings (non-destructive)

**File:** EDNAClient\Configuration\WellKnownPaths.cs

```csharp
public static void SaveEdnaSettings(EdnaInfo edna)
{
    string path = LocateEsbInfoFile();
    if (path == null) return;
    // Load raw so ESB metadata fields (Name, Author, etc.) are preserved
    var deserializer = new DeserializerBuilder().Build();
    var raw = File.Exists(path)
        ? deserializer.Deserialize<Dictionary<object, object>>(File.ReadAllText(path))
          ?? new Dictionary<object, object>()
        : new Dictionary<object, object>();
    raw["EDNA"] = new Dictionary<object, object>
    {
        { "EnabledSkillIds", new List<string>(edna.EnabledSkillIds) },
        { "DetailEnabled",   edna.DetailEnabled }
    };
    var serializer = new SerializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .Build();
    File.WriteAllText(path, serializer.Serialize(raw));
}
```

### Delete EDNA_Info.yaml

**File:** EDNAClient\Configuration\EDNA_Info.yaml -- remove from project and disk.

---

## Part 3: EDNA Autonomous Config Discovery

EDNA finds ESB_Info.yaml without being told where it is.

### SteamLocator.cs -- new class

**File:** EDNAClient\Startup\SteamLocator.cs

```csharp
using Microsoft.Win32;
using System.IO;

namespace EDNAClient.Startup
{
    public static class SteamLocator
    {
        public static string GetSteamPath()
        {
            string path = (string)Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            return path?.Replace('/', '\\');
        }

        public static string GetEmpyrionPath()
        {
            string steam = GetSteamPath();
            if (steam == null) return null;
            string path = Path.Combine(steam, "steamapps", "common",
                "Empyrion - Galactic Survival");
            return Directory.Exists(path) ? path : null;
        }

        public static string GetEsbInfoPath()
        {
            string emp = GetEmpyrionPath();
            if (emp == null) return null;
            string path = Path.Combine(emp, "Content", "Mods", "ESB", "ESB_Info.yaml");
            return File.Exists(path) ? path : null;
        }
    }
}
```

### WellKnownPaths.cs -- update LocateEsbInfoFile()

Replace the hardcoded relative path with a two-step lookup:

```csharp
public static string LocateEsbInfoFile()
{
    // Try 1: deployed alongside mod (ESBModPath\EDNA\EDNAClient.exe -> ..\ESB_Info.yaml)
    string relative = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ESB_Info.yaml"));
    if (File.Exists(relative)) return relative;

    // Try 2: Steam registry anchor
    return SteamLocator.GetEsbInfoPath();
}
```

---

## Part 4: EDNA Startup State Machine

### StartupState.cs -- new enum

**File:** EDNAClient\Startup\StartupState.cs

```csharp
namespace EDNAClient.Startup
{
    public enum StartupState
    {
        NoEmpyrion,
        NoEsb,
        NoMqtt,
        Ready
    }
}
```

### StartupOrchestrator.cs -- new class

**File:** EDNAClient\Startup\StartupOrchestrator.cs

```csharp
using System.Net.Sockets;
using EDNAClient.Configuration;

namespace EDNAClient.Startup
{
    public class StartupOrchestrator
    {
        public StartupState Detect()
        {
            if (SteamLocator.GetEmpyrionPath() == null)
                return StartupState.NoEmpyrion;

            if (WellKnownPaths.LocateEsbInfoFile() == null)
                return StartupState.NoEsb;

            var info = WellKnownPaths.LoadEsbInfo();
            if (!MqttReachable(info?.MQTThost))
                return StartupState.NoMqtt;

            return StartupState.Ready;
        }

        private bool MqttReachable(MqttConnectionSettings mqtt)
        {
            if (mqtt == null) return false;
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect(mqtt.WithTcpServer ?? "localhost", mqtt.Port);
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
```

### App.xaml.cs -- route on startup state

After single-instance mutex check, before EdnaService initialization:

```csharp
var orchestrator = new StartupOrchestrator();
_startupState = orchestrator.Detect();

if (_startupState != StartupState.Ready)
{
    // TrayIconManager still created so user sees EDNA in tray with status tooltip
    // Setup wizards invoked here in future stages -- see EDNAClient/Setup/PLAN.md
}
```

TrayIconManager should reflect the state in its tooltip (e.g. "EDNA - No MQTT connection").
Skills and MQTT connect are only initiated when state == Ready.

### EDNAClient\Startup\PLAN.md -- stub plan

```markdown
# Startup State Machine -- Future Stages

## States
- NoEmpyrion: Empyrion not found via Steam registry. Show advisory message.
- NoEsb: ESB mod not installed. Offer to run EsbInstaller (see Setup/PLAN.md).
- NoMqtt: ESB_Info.yaml found but broker unreachable. Offer to run MosquittoInstaller.
- Ready: Full connect, normal operation.

## Future work
- Connect state detection to TrayIconManager icon variants (gray/amber/green)
- Drive Setup wizards from tray context menu based on current state
- Re-check state periodically so tray self-heals when ESB or broker comes online
```

---

## Part 5: Setup Stubs

### ISetupStep.cs -- stub interface

**File:** EDNAClient\Setup\ISetupStep.cs

```csharp
using System.Threading.Tasks;

namespace EDNAClient.Setup
{
    public interface ISetupStep
    {
        string DisplayName { get; }
        Task RunAsync();
    }
}
```

### EsbInstaller.cs -- stub

**File:** EDNAClient\Setup\EsbInstaller.cs

```csharp
using System;
using System.Threading.Tasks;

namespace EDNAClient.Setup
{
    public class EsbInstaller : ISetupStep
    {
        public string DisplayName => "Install ESB Mod";

        public Task RunAsync()
        {
            throw new NotImplementedException("ESB installer not yet implemented. See Setup/PLAN.md.");
        }
    }
}
```

### MosquittoInstaller.cs -- stub

**File:** EDNAClient\Setup\MosquittoInstaller.cs

```csharp
using System;
using System.Threading.Tasks;

namespace EDNAClient.Setup
{
    public class MosquittoInstaller : ISetupStep
    {
        public string DisplayName => "Set Up MQTT Broker";

        public Task RunAsync()
        {
            throw new NotImplementedException("Mosquitto installer not yet implemented. See Setup/PLAN.md.");
        }
    }
}
```

### EDNAClient\Setup\PLAN.md -- stub plan

```markdown
# Setup Wizards -- Future Stages

## EsbInstaller
Copies ESB mod payload from a known sibling folder in the release tree into
Empyrion\Content\Mods\ESB\. Creates ESB_Info.yaml with defaults if absent.
Compares installed version against bundled payload version for upgrade detection.
Payload location: relative to EDNAClient.exe, e.g. ..\Payload\ESB\

## MosquittoInstaller
Steps:
1. Detect if mosquitto is already installed (check PATH or well-known install dirs).
2. If absent, download mosquitto installer from mosquitto.org and run it.
3. Write mosquitto.conf (listener 1883, allow_anonymous false).
4. Run mosquitto_passwd to create credentials matching ESB_Info.yaml.
5. Register mosquitto as a Windows service (requires elevation).
6. Start the service and verify connectivity.
LAN-only scope: username/password auth only, no TLS for initial setup.
```

---

## Files Changed Summary

| File | Change |
|------|--------|
| ESB\BusService\BusManager.cs | _ednaProcess field, LaunchEdnaClient(), Shutdown() kill |
| ESB\Configuration\ESB_Info.yaml | Add EDNA: block |
| EDNAClient\Configuration\EsbInfo.cs | Add EdnaInfo EDNA property |
| EDNAClient\Configuration\WellKnownPaths.cs | LocateEsbInfoFile() with Steam fallback, SaveEdnaSettings() |
| EDNAClient\Core\EdnaService.cs | Read from esbInfo.EDNA, update save call |
| EDNAClient\App.xaml.cs | StartupOrchestrator call, state-based routing |
| EDNAClient\Configuration\EDNA_Info.yaml | Delete |
| EDNAClient\Startup\StartupState.cs | New enum |
| EDNAClient\Startup\StartupOrchestrator.cs | New state machine |
| EDNAClient\Startup\SteamLocator.cs | New Steam registry discovery |
| EDNAClient\Startup\PLAN.md | New stub plan |
| EDNAClient\Setup\ISetupStep.cs | New stub interface |
| EDNAClient\Setup\EsbInstaller.cs | New stub |
| EDNAClient\Setup\MosquittoInstaller.cs | New stub |
| EDNAClient\Setup\PLAN.md | New stub plan |

ESB.Messaging\ is untouched (frozen).

---

## Verification

1. Build both projects -- no errors.
2. Start game client -- BusManager.Init fires, EDNAClient.exe launches, tray icon appears.
3. Confirm EDNA reads EnabledSkillIds from ESB_Info.yaml EDNA block.
4. Toggle a skill -- confirm ESB_Info.yaml is updated with EDNA block preserved and ESB
   metadata (Name, Author, etc.) preserved.
5. Close game -- Shutdown() fires, EDNA process is killed.
6. Launch EDNAClient.exe directly (no game running):
   a. With ESB_Info.yaml present and MQTT up: state == Ready, full connect.
   b. With MQTT down: state == NoMqtt, tray shows advisory, no crash.
   c. With no ESB_Info.yaml: state == NoEsb, tray shows advisory.
   d. With Empyrion not installed: state == NoEmpyrion, tray shows advisory.
