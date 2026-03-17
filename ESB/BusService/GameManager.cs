using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using System.Collections.Generic;

namespace ESB
{
    public class GameManager
    {
        readonly private ContextData _cntxt;

        public string GameName { get; private set; }
        public string GameIdentifier { get; private set; }
        public string GameDataPath { get; private set; }
        public string SaveGamePath { get; private set; }
        public string GameMode { get; private set; }
        public Dictionary<int, string> BlockAndItemMapping { get; private set; }

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
                SetGameProperties();
            }
            else
            {
            }
            await Task.CompletedTask;
        }
        private void SetGameProperties()
        {
            var cacheDir = _cntxt.ModApi.Application.GetPathFor(AppFolder.Cache);
            var directories = Directory.GetDirectories(cacheDir);
            SaveGamePath = Path.GetFullPath(_cntxt.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameName = Path.GetFileName(SaveGamePath);
            GameIdentifier = GenerateUniqueIdentifier(Path.GetFileName(directories.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(GameName))));
            GameDataPath = Path.GetFullPath(Path.Combine(_cntxt.BusManager.ESBModPath, "Games", GameIdentifier));
            GameMode = _cntxt.ModApi.Application.Mode.ToString();
            var mapping = _cntxt.ModApi.Application.GetBlockAndItemMapping();
            BlockAndItemMapping = new Dictionary<int, string>();
            foreach (var pair in mapping)
            {
                BlockAndItemMapping[pair.Value] = pair.Key;
            }
            _cntxt.ModApi.Log($"IModApi properties: ClientPlayfield={((_cntxt.ModApi.ClientPlayfield == null) ? "null" : "set")}, Network={(_cntxt.ModApi.Network == null ? "null" : "set")}, GUI={(_cntxt.ModApi.GUI == null ? "null" : "set")}, PDA={(_cntxt.ModApi.PDA == null ? "null" : "set")}, Scripting={(_cntxt.ModApi.Scripting == null ? "null" : "set")}, SoundPlayer={(_cntxt.ModApi.SoundPlayer == null ? "null" : "set")}, Application={(_cntxt.ModApi.Application == null ? "null" : "set")}");
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
    }
}