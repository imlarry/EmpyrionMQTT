using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Messaging;
using ESB.Payloads;
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

            await AnnounceConnectAsync(initialContext, initialContext, GameRcId != null);
            _ctx.DisconnectCleanup.Register(initialContext, "Announcements", "Connect");

            var json = new JObject(
                new JProperty("Status",      "Created"),
                new JProperty("ContextRcId", _ctx.Bus.ContextRcId));
            await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "App", "GameManager", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // PrepareEnterGame ... computes GameRcId/GameName/etc. without changing the bus context.
        // The handler calls this first so the GameRcId can be advertised on the LOBBY-scope Connect
        // retain (signalling EDNA to follow) before SwitchContextAsync runs.
        public void PrepareEnterGame()
        {
            SetGameProperties();
        }

        // EnterGame ... Client lifecycle: swap context from Lobby to the real Game rcId.
        // The bus subscribes the new audience before dropping the old one, so no in-process gap.
        // IsTransitioning gates the publisher-side EventQueue across the swap; clearing it after
        // SwitchContextAsync returns lets UpdateHandler drain queued events on the new ContextRcId.
        //
        // The Lobby Connect retain is preserved across the swap as the context manifest: it carries
        // ContextRcId=GameRcId and game-detail fields so a late-joining peer (e.g. EDNA started
        // after a save is loaded) can discover the active game by reading the Lobby retain alone.
        public async Task EnterGame()
        {
            _ctx.IsTransitioning = true;
            try
            {
                if (string.IsNullOrEmpty(GameRcId)) SetGameProperties();
                await _ctx.Bus.SwitchContextAsync(GameRcId);
                await AnnounceConnectAsync(GameRcId, GameRcId, true);
                _ctx.DisconnectCleanup.Register(GameRcId, "Announcements", "Connect");
            }
            finally
            {
                _ctx.IsTransitioning = false;
            }
        }

        // ExitGame ... Client lifecycle: swap context back to Lobby. Game-scope retains
        // (game Connect, BlockAndItemMapping) are cleared; the Lobby Connect retain is updated
        // back to {ContextRcId=Lobby, GameRcId=null} so the manifest reflects the idle state.
        public async Task ExitGame()
        {
            if (string.IsNullOrEmpty(LobbyRcId)) return;
            _ctx.IsTransitioning = true;
            try
            {
                var priorRcId = _ctx.Bus.ContextRcId;
                await _ctx.Bus.SwitchContextAsync(LobbyRcId);
                await _ctx.DisconnectCleanup.ClearScopeAsync(_ctx.Messenger, priorRcId);
                await AnnounceConnectAsync(LobbyRcId, LobbyRcId, false);
                _ctx.DisconnectCleanup.Register(LobbyRcId, "Announcements", "Connect");
            }
            finally
            {
                _ctx.IsTransitioning = false;
            }
        }

        // AnnounceConnectAsync ... publishes the Connect retain on rcIdToPublishOn. ContextRcId in
        // the payload names the audience the publisher is on (or moving to); peers compare this
        // against their own bus.ContextRcId and follow when the values disagree.
        public Task AnnounceConnectAsync(string rcIdToPublishOn, string contextRcId, bool includeGameDetails)
        {
            var payload = new ConnectAnnouncement
            {
                Type        = _ctx.BusManager.ParticipantType,
                MachineId   = _ctx.Bus.MachineId,
                ContextRcId = contextRcId
            };
            if (includeGameDetails)
            {
                payload.GameRcId     = GameRcId;
                payload.GameName     = GameName;
                payload.GameMode     = GameMode;
                payload.SaveGamePath = SaveGamePath;
            }
            return _ctx.Bus.AnnounceAsync(rcIdToPublishOn, "Connect", payload);
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
