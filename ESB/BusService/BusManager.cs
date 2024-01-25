using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using ESB.Messaging;
using ESB.Common;
using Eleon.Modding;

namespace ESB
{
    public class BusManager
    {
        readonly private ContextData _cntxt;
        readonly private EventManager _eMgr;

        public string ApplicationName { get; private set; }
        public string ESBModPath { get; private set; }

        public BusManager(ContextData context, EventManager eMgr)
        {
            // preserve context, event manager, and BusManager references
            _cntxt = context;
            _eMgr = eMgr;
            _cntxt.BusManager = this;

            // player app returns "client" because modload init occurs in lobby (a "client" mode)
            ApplicationName = _cntxt.ModApi.Application.Mode.ToString();

            // determine root path for ESB mod
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            string location = currentAssembly.Location;
            ESBModPath = Path.GetDirectoryName(location);
        }

        public async Task Init()
        {
            // open and parse yaml config file
            string configPath = Path.Combine(ESBModPath, "ESB_Info.yaml");
            _cntxt.ESBConfig = YamlFileReader.ReadYamlFile<ESBConfig>(configPath);

            // create client and open a channel to broker
            await _cntxt.Messenger.ConnectAsync
                    (_cntxt
                    , ApplicationName
                    , _cntxt.ESBConfig.MQTThost.WithTcpServer
                    , _cntxt.ESBConfig.MQTThost.Username
                    , _cntxt.ESBConfig.MQTThost.Password
                    , _cntxt.ESBConfig.MQTThost.CAFilePath);

            // invoke setup methods
            await InitDataDirectory();

            // subscribe to defined topics and associate topic handlers
            var subscriptionHandler = new SubscriptionHandler(_cntxt);
            await subscriptionHandler.SubscribeAll();

            // enable event handlers
            _eMgr.EnableEventHandlers();
        }

        public async Task Shutdown()
        {
            // shutdown
            _eMgr.DisableEventHandlers();
            await _cntxt.Messenger.DisconnectAsync();
        }

        public async Task InitDataDirectory()
        {
            var dataDir = Path.GetFullPath(Path.Combine(ESBModPath, "Games"));
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
                JObject json = new JObject(
                    new JProperty("DataDirectoryPath", dataDir),
                    new JProperty("Status", "Created"));
                await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.InitDataDir", json.ToString(Newtonsoft.Json.Formatting.None));
            }
            else
            {
                JObject json = new JObject(
                    new JProperty("DataDirectoryPath", dataDir),
                    new JProperty("Status", "AlreadyExists"));
                await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.InitDataDir", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
    }
}