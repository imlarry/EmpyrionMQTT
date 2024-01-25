using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using ESB.Database;

namespace ESB
{
    public class GameManager
    {
        readonly private ContextData _cntxt;

        public string GameName { get; private set; }
        public string GameIdentifier { get; private set; }
        public string GameDataPath { get; private set; }
        public string GameMode { get; private set; }

        public GameManager(ContextData context)
        {
            _cntxt = context;
        }

        public async Task Init()
        {
            _cntxt.GameManager = this;
            var json = new JObject(
                new JProperty("Status", "Created"));
            await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.GameManager", json.ToString(Newtonsoft.Json.Formatting.None));
            // set to a "no game active" state
        }
        public async Task StateChanged(bool hasEntered)
        {
            if (hasEntered)
            {
                // set to a "game active" state (I AM THE POWER!)
                SetGameProperties();
                await OpenLocalDatabase();
            }
            else
            {
                // set to a "no game active" state (LET THE POWER RETURN!)
            }
        }
        private void SetGameProperties()
        {
            var cacheDir = _cntxt.ModApi.Application.GetPathFor(AppFolder.Cache);
            var directories = Directory.GetDirectories(cacheDir);
            GameName = Path.GetFileName(_cntxt.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameIdentifier = GenerateUniqueIdentifier(Path.GetFileName(directories.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(GameName))));
            GameDataPath = Path.GetFullPath(Path.Combine(_cntxt.BusManager.ESBModPath, "Games", GameIdentifier));
            GameMode = _cntxt.ModApi.Application.Mode.ToString();
        }

        private string GenerateUniqueIdentifier(string identifier)
        {
            var parts = identifier.Split('_');
            if (parts.Length > 1)
            {
                int number = int.Parse(parts[parts.Length - 1]);
                string hexValue = number.ToString("X");
                return $"{parts[0]}@{hexValue}";
            }
            else
            {
                return parts[0];
            }
        }

        private async Task OpenLocalDatabase()
        {
            string dbPath = Path.Combine(GameDataPath, "local.db");
            JObject json;

            // create directory for game data if it doesn't exist
            if (!Directory.Exists(GameDataPath))
            {
                Directory.CreateDirectory(GameDataPath);
            }

            // create local database if it doesn't exist and populate it with schema
            if (!File.Exists(dbPath))
            {
                var dbAccess = new DbAccess($"Data Source={dbPath};Version=3;", false);
                dbAccess.CreateDatabaseFile(GameDataPath, "local.db");
                string sqlCommands = File.ReadAllText(Path.Combine(_cntxt.BusManager.ESBModPath, "LocalSchema.sql.txt"));
                dbAccess.ExecuteCommand(sqlCommands);
                json = new JObject(
                    new JProperty("DatabasePath", dbPath),
                    new JProperty("Status", "Created"));
            }
            else
            {
                json = new JObject(
                    new JProperty("DatabasePath", dbPath),
                    new JProperty("Status", "AlreadyExists"));
            }

            await _cntxt.Messenger.SendAsync(MessageClass.Information, "ESB.GameManager.CreateLocalDatabase", json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}