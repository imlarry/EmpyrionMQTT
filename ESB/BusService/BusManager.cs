using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Eleon.Modding;
using ESB.Configuration;
using ESB.EventHandlers;
using ESB.Helpers;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class BusManager
    {
        private readonly ContextData _ctx;
        private readonly EventManager _eMgr;

        public string ParticipantType  { get; private set; }
        public string ESBModPath       { get; private set; }

        public BusManager(ContextData context, EventManager eMgr)
        {
            _ctx = context;
            _eMgr = eMgr;
            _ctx.BusManager = this;

            switch (_ctx.ModApi.Application.Mode)
            {
                case ApplicationMode.Client:          ParticipantType = "Client"; break;
                case ApplicationMode.DedicatedServer: ParticipantType = "Ds";     break;
                case ApplicationMode.PlayfieldServer: ParticipantType = "Pfs";    break;
                default: ParticipantType = _ctx.ModApi.Application.Mode.ToString(); break;
            }

            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            ESBModPath = Path.GetDirectoryName(currentAssembly.Location);
        }

        public async Task Init()
        {
            string configPath = Path.Combine(ESBModPath, "ESB_Info.yaml");
            _ctx.ESBConfig = YamlFileReader.ReadYamlFile<ESBConfig>(configPath);

            await _ctx.Messenger.ConnectAsync
                    (_ctx
                    , ParticipantType
                    , _ctx.ESBConfig.MQTThost.WithTcpServer
                    , _ctx.ESBConfig.MQTThost.Port
                    , _ctx.ESBConfig.MQTThost.Username
                    , _ctx.ESBConfig.MQTThost.Password
                    , _ctx.ESBConfig.MQTThost.CAFilePath);

            await PublishRegistryEntryAsync();

            var subscriptionHandler = new SubscriptionHandler(_ctx);
            await subscriptionHandler.SubscribeAll();

            _ctx.IsReady = true;

            if (ParticipantType == "Client")
            {
                LaunchEdnaClient();
            }
        }

        public async Task Shutdown()
        {
            _eMgr.DisableEventHandlers();
            // await ClearRegistryEntryAsync(); // a will can clear it if the client disconnects unexpectedly. Retained messages with empty payload are discarded by the broker.
            await _ctx.Messenger.DisconnectAsync();
        }

        private void LaunchEdnaClient()
        {
            string ednaExe = Path.Combine(ESBModPath, "EDNA", "EDNA.exe");
            _ctx.ModApi.Log($"EDNA launch: looking for '{ednaExe}'");
            if (!File.Exists(ednaExe))
            {
                _ctx.ModApi.Log("EDNA launch: exe not found -- skipping");
                return;
            }
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = ednaExe,
                    UseShellExecute = true
                };
                var p = System.Diagnostics.Process.Start(psi);
                _ctx.ModApi.Log($"EDNA launch: started detached PID {p?.Id}");
            }
            catch (System.Exception ex)
            {
                _ctx.ModApi.Log($"EDNA launch failed: {ex.Message}");
            }
        }

        private async Task PublishRegistryEntryAsync()
        {
            var json = new JObject(new JProperty("type", ParticipantType));
            await _ctx.Messenger.PublishRetainedAsync("Registry", MessageType.Evt, "Connect", json.ToString(Formatting.None));
        }

        private async Task ClearRegistryEntryAsync()
        {
            // empty retained payload instructs the broker to discard the retained message
            await _ctx.Messenger.PublishRetainedAsync("Registry", MessageType.Evt, "Connect", "");
        }
    }
}