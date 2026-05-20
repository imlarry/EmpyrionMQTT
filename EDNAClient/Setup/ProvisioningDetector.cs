using System;
using System.IO;
using System.Text;
using EDNAClient.Configuration;
using EDNAClient.Startup;
using MQTTnet.Adapter;
using MQTTnet.Client;

namespace EDNAClient.Setup
{
    // FUTURE-provisioning detection. Read-only: no files written, no services touched.
    // Builds a human-readable report describing host/broker state and what EsbInstaller /
    // MosquittoInstaller WOULD do once implemented. Logs are human-interpreted, so the
    // narrative form is the point -- not a structured payload.
    //
    // Broker status is derived from the caller's actual bus-connect outcome, not a separate
    // probe -- the connect attempt IS the probe. Local Mosquitto install presence is reported
    // only as a hint, since the broker may legitimately live on another machine.
    public static class ProvisioningDetector
    {
        public static string BuildReport(StartupState rolledUp, MqttConnectionSettings mqtt, Exception? connectError)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EDNA Provisioning Detection -- FUTURE state, no actions taken.");
            sb.AppendLine("This report describes what installers WOULD do once implemented.");
            sb.AppendLine("Overall StartupState: " + rolledUp);
            sb.AppendLine();

            string? empyrion = SteamLocator.GetEmpyrionPath();
            if (empyrion != null)
            {
                sb.AppendLine("[Empyrion] FOUND -- " + empyrion);
                sb.AppendLine("  Why: every downstream path (ESB mod folder, save-game enumeration) resolves from here.");
            }
            else
            {
                sb.AppendLine("[Empyrion] NOT FOUND via Steam HKCU registry");
                sb.AppendLine("  Why: without this anchor the mod cannot be installed and saves cannot be browsed. A future installer would fall back to a manual path prompt.");
            }
            sb.AppendLine();

            if (empyrion != null)
            {
                string esbDir = Path.Combine(empyrion, "Content", "Mods", "ESB");
                bool hasDir   = Directory.Exists(esbDir);
                bool hasYml   = SteamLocator.GetEsbInfoPath() != null;
                if (hasDir && hasYml)
                {
                    sb.AppendLine("[ESB Mod] FOUND -- " + esbDir + " (ESB_Info.yaml present)");
                    sb.AppendLine("  Why: host can already run as an ESB participant. EsbInstaller would only act if the bundled payload version is newer than what is installed.");
                }
                else if (hasDir)
                {
                    sb.AppendLine("[ESB Mod] PARTIAL -- " + esbDir + " exists, ESB_Info.yaml missing");
                    sb.AppendLine("  Why: yaml carries broker host/port and enabled skills. EsbInstaller would write a defaults file without overwriting existing mod payload.");
                }
                else
                {
                    sb.AppendLine("[ESB Mod] NOT FOUND -- " + esbDir);
                    sb.AppendLine("  Why: EsbInstaller would copy the bundled ESB payload here and write a default ESB_Info.yaml.");
                }
            }
            else
            {
                sb.AppendLine("[ESB Mod] SKIPPED -- no Empyrion root to resolve against.");
            }
            sb.AppendLine();

            string endpoint = (mqtt.WithTcpServer ?? "localhost") + ":" + mqtt.Port;
            switch (ClassifyConnectError(connectError))
            {
                case BrokerStatus.Authenticated:
                    sb.AppendLine("[MQTT Broker] REACHABLE + AUTHENTICATED -- " + endpoint);
                    sb.AppendLine("  Why: bus connect succeeded with the current ESB_Info.yaml. The fact that this report was published over MQTT is itself the proof. No installer action needed.");
                    break;
                case BrokerStatus.AuthFailed:
                    sb.AppendLine("[MQTT Broker] REACHABLE but AUTH FAILED -- " + endpoint);
                    sb.AppendLine("  Why: broker responded but credentials in ESB_Info.yaml were rejected. MosquittoInstaller would re-run mosquitto_passwd locally; if the broker lives on another machine the user must reconcile credentials with that host.");
                    break;
                default:
                    sb.AppendLine("[MQTT Broker] UNREACHABLE -- " + endpoint);
                    sb.AppendLine("  Why: bus connect failed before reaching the credential exchange. Could be no broker installed, a broker on another machine that is offline, or wrong host/port in ESB_Info.yaml. Detecting a local install reliably across install methods (MSI, chocolatey, scoop, custom path) is not worth the false-negative noise -- the connect failure logged above is the ground truth.");
                    break;
            }

            return sb.ToString();
        }

        private enum BrokerStatus { Authenticated, AuthFailed, Unreachable }

        // Walks the inner-exception chain looking for an MQTT CONNACK rejection. The CONNACK
        // reason code is the only reliable way to distinguish "broker said no to your creds"
        // from "nothing answered on the wire." Anything else -- socket errors, timeouts,
        // disposed clients -- classifies as Unreachable.
        private static BrokerStatus ClassifyConnectError(Exception? ex)
        {
            if (ex == null) return BrokerStatus.Authenticated;
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is MqttConnectingFailedException mcfe)
                {
                    var rc = mcfe.ResultCode;
                    if (rc == MqttClientConnectResultCode.BadUserNameOrPassword ||
                        rc == MqttClientConnectResultCode.NotAuthorized ||
                        rc == MqttClientConnectResultCode.Banned)
                        return BrokerStatus.AuthFailed;
                    return BrokerStatus.Unreachable;
                }
            }
            return BrokerStatus.Unreachable;
        }
    }
}
