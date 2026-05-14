using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Messaging;
using System.Collections.Generic;

namespace ESB
{
    public class GameManager
    {
        readonly private ContextData _ctx;

        public string GameName { get; private set; }
        public string GameRcId { get; private set; }              // RoutingContextId.Game(SaveGamePath, machineId)
        public string CurrentPlayfieldRcId { get; set; }          // RoutingContextId.Playfield(...); set by PlayfieldLoadedHandler
        public string SaveGamePath { get; private set; }
        public string GameMode { get; private set; }
        public Dictionary<int, string> BlockAndItemMapping { get; private set; }
        public IPlayfield CurrentPlayfield { get; set; }

        public GameManager(ContextData context)
        {
            _ctx = context;
            _ctx.GameManager = this;
        }

        // ApplyMappingFromJson ... deserializes an ID->Name JSON object into BlockAndItemMapping.
        public void ApplyMappingFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var obj = JObject.Parse(json);
            var map = new Dictionary<int, string>();
            foreach (var prop in obj.Properties())
            {
                int id;
                if (int.TryParse(prop.Name, out id))
                    map[id] = (string)prop.Value;
            }
            if (map.Count > 0) BlockAndItemMapping = map;
        }

        public async Task Init()
        {
            SaveGamePath = Path.GetFullPath(_ctx.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameName     = Path.GetFileName(SaveGamePath);
            GameRcId     = RoutingContextId.Game(SaveGamePath, _ctx.Bus.MachineId).Id;
            GameMode     = _ctx.ModApi.Application.Mode.ToString();

            var json = new JObject(
                new JProperty("Status", "Created"));
            await _ctx.Messenger.SendAsync(_ctx.Bus.MachineId, "App", MessageType.Log, "GameManager", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public void StateChanged(bool hasEntered)
        {
            if (hasEntered)
            {
                SetGameProperties();
            }
        }

        private void SetGameProperties()
        {
            SaveGamePath = Path.GetFullPath(_ctx.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameName     = Path.GetFileName(SaveGamePath);
            GameRcId     = RoutingContextId.Game(SaveGamePath, _ctx.Bus.MachineId).Id;
            GameMode     = _ctx.ModApi.Application.Mode.ToString();

            if (_ctx.BusManager.ParticipantType == "Client" && (BlockAndItemMapping == null || BlockAndItemMapping.Count == 0))
            {
                var raw = _ctx.ModApi.Application.GetBlockAndItemMapping();
                if (raw != null && raw.Count > 0)
                {
                    BlockAndItemMapping = new Dictionary<int, string>();
                    foreach (var pair in raw)
                        BlockAndItemMapping[pair.Value] = pair.Key;
                    _ = _ctx.Bus.AnnounceAsync(GameRcId, "BlockAndItemMapping", BlockAndItemMapping, 3600u);
                }
            }

            _ctx.ModApi.Log($"IModApi properties: ClientPlayfield={((_ctx.ModApi.ClientPlayfield == null) ? "null" : "set")}, Network={(_ctx.ModApi.Network == null ? "null" : "set")}, GUI={(_ctx.ModApi.GUI == null ? "null" : "set")}, PDA={(_ctx.ModApi.PDA == null ? "null" : "set")}, Scripting={(_ctx.ModApi.Scripting == null ? "null" : "set")}, SoundPlayer={(_ctx.ModApi.SoundPlayer == null ? "null" : "set")}, Application={(_ctx.ModApi.Application == null ? "null" : "set")}");
        }
    }
}
