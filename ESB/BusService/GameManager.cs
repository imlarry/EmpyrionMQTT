using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Models;
using ESB.Messaging;
using System.Collections.Generic;

namespace ESB
{
    public class GameManager : IGameManager
    {
        readonly private ContextData _ctx;

        public string GameName { get; private set; }
        public string GameIdentifier { get; private set; }
        public string GameDataPath { get; private set; }
        public string SaveGamePath { get; private set; }
        public string GameMode { get; private set; }
        public Dictionary<int, string> BlockAndItemMapping { get; private set; }

        public GameManager(ContextData context)
        {
            _ctx = context;
        }

        public async Task Init()
        {
            _ctx.GameManager = this;
            var json = new JObject(
                new JProperty("Status", "Created"));
            await _ctx.Messenger.SendAsync(MessageClass.Information, "ESB.GameManager", json.ToString(Newtonsoft.Json.Formatting.None));
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
            var cacheDir = _ctx.ModApi.Application.GetPathFor(AppFolder.Cache);
            var directories = Directory.GetDirectories(cacheDir);
            SaveGamePath = Path.GetFullPath(_ctx.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameName = Path.GetFileName(SaveGamePath);
            GameIdentifier = GenerateUniqueIdentifier(Path.GetFileName(directories.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(GameName))));
            GameDataPath = Path.GetFullPath(Path.Combine(_ctx.BusManager.ESBModPath, "Games", GameIdentifier));
            GameMode = _ctx.ModApi.Application.Mode.ToString();
            var mapping = _ctx.ModApi.Application.GetBlockAndItemMapping();
            BlockAndItemMapping = new Dictionary<int, string>();
            foreach (var pair in mapping)
            {
                BlockAndItemMapping[pair.Value] = pair.Key;
            }
            _ctx.ModApi.Log($"IModApi properties: ClientPlayfield={((_ctx.ModApi.ClientPlayfield == null) ? "null" : "set")}, Network={(_ctx.ModApi.Network == null ? "null" : "set")}, GUI={(_ctx.ModApi.GUI == null ? "null" : "set")}, PDA={(_ctx.ModApi.PDA == null ? "null" : "set")}, Scripting={(_ctx.ModApi.Scripting == null ? "null" : "set")}, SoundPlayer={(_ctx.ModApi.SoundPlayer == null ? "null" : "set")}, Application={(_ctx.ModApi.Application == null ? "null" : "set")}");
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