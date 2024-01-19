using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SQLite;
using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;

namespace ESB
{
    public class GameManager
    {
        readonly private ContextData _cntxt;

        public string GameName { get; private set; }
        public string GameIdentifier { get; private set; }
        public string GamePath { get; private set; }

        public GameManager(ContextData context)
        {
            _cntxt = context;
        }

        public async Task Init()
        {
            _cntxt.GameManager = this;
            var json = new JObject(
                new JProperty("Status", "Created"));
            await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.GameManager.Init", json.ToString(Newtonsoft.Json.Formatting.None));
            // set to a "no game active" state
        }
        public void SetGameDirectory()
        {
            var cacheDir = _cntxt.ModApi.Application.GetPathFor(AppFolder.Cache);
            var directories = Directory.GetDirectories(cacheDir);
            GameName = Path.GetFileName(_cntxt.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameIdentifier = Path.GetFileName(directories.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(GameName)));
            GamePath = Path.GetFullPath(Path.Combine(_cntxt.BusManager.ESBModPath, "Data", "Games", GameIdentifier));
            // add SenAsync call to send GameName, GameIdentifier, and GamePath to ESB and switch to async task
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
                await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.GameManager.CreateLocalDatabase", json.ToString(Newtonsoft.Json.Formatting.None));
            }
            else
            {
                json = new JObject(
                    new JProperty("DatabasePath", dbPath),
                    new JProperty("Status", "AlreadyExists"));
                await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.GameManager.CreateLocalDatabase", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
    }
}