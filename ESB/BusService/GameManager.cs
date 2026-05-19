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

        public string GameName     { get; private set; }
        public string GameRcId     { get; private set; }   // RoutingContextId.Game(SaveGamePath, machineId); null on Client until EnterGame
        public string LobbyRcId    { get; private set; }   // RoutingContextId.Lobby(machineId); null for Pfs/Ds
        public string SaveGamePath { get; private set; }
        public string GameMode     { get; private set; }
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
            GameMode = _ctx.ModApi.Application.Mode.ToString();

            var participantType = _ctx.BusManager.ParticipantType;
            string initialContext;
            if (participantType == "Pfs" || participantType == "Ds")
            {
                // Service processes serve one specific game; no lobby phase.
                SetGameProperties();
                initialContext = GameRcId;
            }
            else
            {
                // Client (and any user-defined in-game participant): pre-game until EnterGame.
                LobbyRcId      = RoutingContextId.Lobby(_ctx.Bus.MachineId).Id;
                initialContext = LobbyRcId;
            }

            await _ctx.Bus.SwitchContextAsync(initialContext);

            await _ctx.Bus.AnnounceAsync(_ctx.Bus.ContextRcId, "Connect", new { Type = _ctx.BusManager.ParticipantType });
            _ctx.DisconnectCleanup.Register(_ctx.Bus.ContextRcId, "Announcements", "Connect");

            var json = new JObject(
                new JProperty("Status",      "Created"),
                new JProperty("ContextRcId", _ctx.Bus.ContextRcId));
            await _ctx.Messenger.SendAsync(_ctx.Bus.MachineId, "App", MessageType.Log, "GameManager", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // EnterGame ... Client lifecycle: swap context from Lobby to the real Game rcId.
        // The bus subscribes the new audience before dropping the old one, so no in-process gap.
        // IsTransitioning gates the publisher-side EventQueue across the swap; clearing it after
        // SwitchContextAsync returns lets UpdateHandler drain queued events on the new ContextRcId.
        public async Task EnterGame()
        {
            _ctx.IsTransitioning = true;
            try
            {
                var priorRcId = _ctx.Bus.ContextRcId;
                SetGameProperties();
                await _ctx.Bus.SwitchContextAsync(GameRcId);
                await _ctx.DisconnectCleanup.ClearScopeAsync(_ctx.Messenger, priorRcId);
                await _ctx.Bus.AnnounceAsync(GameRcId, "Connect", new { Type = _ctx.BusManager.ParticipantType });
                _ctx.DisconnectCleanup.Register(GameRcId, "Announcements", "Connect");
            }
            finally
            {
                _ctx.IsTransitioning = false;
            }
        }

        // ExitGame ... Client lifecycle: swap context back to Lobby.
        public async Task ExitGame()
        {
            if (string.IsNullOrEmpty(LobbyRcId)) return;
            _ctx.IsTransitioning = true;
            try
            {
                var priorRcId = _ctx.Bus.ContextRcId;
                await _ctx.Bus.SwitchContextAsync(LobbyRcId);
                await _ctx.DisconnectCleanup.ClearScopeAsync(_ctx.Messenger, priorRcId);
                await _ctx.Bus.AnnounceAsync(LobbyRcId, "Connect", new { Type = _ctx.BusManager.ParticipantType });
                _ctx.DisconnectCleanup.Register(LobbyRcId, "Announcements", "Connect");
            }
            finally
            {
                _ctx.IsTransitioning = false;
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
                    _ = _ctx.Bus.AnnounceAsync(GameRcId, "BlockAndItemMapping", BlockAndItemMapping, 86400u);
                    _ctx.DisconnectCleanup.Register(GameRcId, "Announcements", "BlockAndItemMapping");
                }
            }

            _ctx.ModApi.Log($"IModApi properties: ClientPlayfield={((_ctx.ModApi.ClientPlayfield == null) ? "null" : "set")}, Network={(_ctx.ModApi.Network == null ? "null" : "set")}, GUI={(_ctx.ModApi.GUI == null ? "null" : "set")}, PDA={(_ctx.ModApi.PDA == null ? "null" : "set")}, Scripting={(_ctx.ModApi.Scripting == null ? "null" : "set")}, SoundPlayer={(_ctx.ModApi.SoundPlayer == null ? "null" : "set")}, Application={(_ctx.ModApi.Application == null ? "null" : "set")}");
        }
    }
}
