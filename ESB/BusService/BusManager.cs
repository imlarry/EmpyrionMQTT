using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Eleon.Modding;
using ESB.Configuration;
using ESB.EventHandlers;
using ESB.Helpers;
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

            _eMgr.EnableEventHandlers();
        }

        public async Task Shutdown()
        {
            _eMgr.DisableEventHandlers();
            // await ClearRegistryEntryAsync(); // this clears an entry, a will can clear it if the client disconnects unexpectedly. Retained messages with empty payload are discarded by the broker.
            await _ctx.Messenger.DisconnectAsync();
        }

        private async Task PublishRegistryEntryAsync()
        {
            string connId = _ctx.Messenger.ClientId();
            var json = new JObject(new JProperty("type", ParticipantType));
            await _ctx.Messenger.PublishRetainedAsync($"ESB/Registry/{connId}", json.ToString(Formatting.None));
        }

        private async Task ClearRegistryEntryAsync()
        {
            string connId = _ctx.Messenger.ClientId();
            // empty retained payload instructs the broker to discard the retained message
            await _ctx.Messenger.PublishRetainedAsync($"ESB/Registry/{connId}", "");
        }
    }
}