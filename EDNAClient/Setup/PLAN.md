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
