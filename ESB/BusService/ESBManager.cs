using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Reflection;
using ESB.Messaging;
using ESB.Common;
using Eleon.Modding;

namespace ESB
{
    public class ESBManager
    {
        readonly private ContextData _cntxt;
        readonly private EventManager _eMgr;

        public string ApplicationName { get; private set; }
        public string GameName { get; private set; }
        public string GameIdentifier { get; private set; }
        public string ESBModPath { get; private set; }
        public string GamePath { get; private set; }

        public ESBManager(ContextData context, EventManager eMgr)
        {
            // preserve context, event manager, and ESBManager references
            _cntxt = context;
            _eMgr = eMgr;
            _cntxt.ESBManager = this;

            // player app returns "client" because modload init occurs in lobby (a "client" mode)
            ApplicationName = _cntxt.ModApi.Application.Mode.ToString();

            // determine game name and identifier
            var cacheDir = _cntxt.ModApi.Application.GetPathFor(AppFolder.Cache);
            var directories = Directory.GetDirectories(cacheDir);
            GameName = Path.GetFileName(_cntxt.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameIdentifier = Path.GetFileName(directories.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(GameName)));

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
            var dataDir = Path.GetFullPath(Path.Combine(ESBModPath, "Data"));
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

        public async Task SetGameDirectory()
        {
            var cacheDir = _cntxt.ModApi.Application.GetPathFor(AppFolder.Cache);
            var directories = Directory.GetDirectories(cacheDir);
            GameName = Path.GetFileName(_cntxt.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameIdentifier = Path.GetFileName(directories.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(GameName)));
            GamePath = Path.GetFullPath(Path.Combine(ESBModPath, "Data", "Games", GameIdentifier));
            JObject json;

            if (GamePath != null)
            {
                if (!Directory.Exists(GamePath))
                {
                    Directory.CreateDirectory(GamePath);
                    json = new JObject(
                        new JProperty("GameDirectoryPath", GamePath),
                        new JProperty("Status", "Created"));
                    await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.InitGameDir", json.ToString(Newtonsoft.Json.Formatting.None));
                }
                else
                {
                    json = new JObject(
                        new JProperty("GameDirectoryPath", GamePath),
                        new JProperty("Status", "AlreadyExists"));
                    await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.InitGameDir", json.ToString(Newtonsoft.Json.Formatting.None));
                }
            }
        }

        public async Task CreateLocalDatabase()
        {
            string dbPath = Path.Combine(GamePath, "local.db");
            JObject json;

            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
                json = new JObject(
                    new JProperty("DatabasePath", dbPath),
                    new JProperty("Status", "Created"));
                await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.CreateLocalDatabase", json.ToString(Newtonsoft.Json.Formatting.None));
            }
            else
            {
                json = new JObject(
                    new JProperty("DatabasePath", dbPath),
                    new JProperty("Status", "AlreadyExists"));
                await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.CreateLocalDatabase", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
    }
}