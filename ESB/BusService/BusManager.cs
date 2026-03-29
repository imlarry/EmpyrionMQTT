using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using ESB.Configuration;
using ESB.Models;
using ESB.Utilities;

namespace ESB
{
    public class BusManager : IBusManager
    {
        private readonly ContextData _ctx;
        private readonly IEventManager _eMgr;

        public string ApplicationName { get; private set; }
        public string ESBModPath { get; private set; }

        public BusManager(ContextData context, IEventManager eMgr)
        {
            // preserve context, event manager, and BusManager references
            _ctx = context;
            _eMgr = eMgr;
            _ctx.BusManager = this;

            // player app returns "client" because modload init occurs in lobby (a "client" mode)
            ApplicationName = _ctx.ModApi.Application.Mode.ToString();

            // determine root path for ESB mod
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            string location = currentAssembly.Location;
            ESBModPath = Path.GetDirectoryName(location);
        }

        public async Task Init()
        {
            // open and parse yaml config file
            string configPath = Path.Combine(ESBModPath, "ESB_Info.yaml");
            _ctx.ESBConfig = YamlFileReader.ReadYamlFile<ESBConfig>(configPath);

            // create client and open a channel to broker
            await _ctx.Messenger.ConnectAsync
                    (_ctx
                    , ApplicationName
                    , _ctx.ESBConfig.MQTThost.WithTcpServer
                    , _ctx.ESBConfig.MQTThost.Port
                    , _ctx.ESBConfig.MQTThost.Username
                    , _ctx.ESBConfig.MQTThost.Password
                    , _ctx.ESBConfig.MQTThost.CAFilePath);

            // subscribe to defined topics and associate topic handlers
            var subscriptionHandler = new SubscriptionHandler(_ctx);
            await subscriptionHandler.SubscribeAll();

            // enable event handlers
            _eMgr.EnableEventHandlers();
        }

        public async Task Shutdown()
        {
            // shutdown
            _eMgr.DisableEventHandlers();
            await _ctx.Messenger.DisconnectAsync();
        }

    }
}