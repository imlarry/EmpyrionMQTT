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

        private bool MqttReachable(MqttConnectionSettings? mqtt)
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
