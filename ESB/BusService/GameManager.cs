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
        // Buffers retained payloads received before game entry, keyed by "{gameId}:{scope}/{operation}".
        private readonly Dictionary<string, string> _pendingRetained = new Dictionary<string, string>();

        public string GameName { get; private set; }
        public string GameIdentifier { get; private set; }
        public string SaveGamePath { get; private set; }
        public string GameMode { get; private set; }
        public Dictionary<int, string> BlockAndItemMapping { get; private set; }
        public IPlayfield CurrentPlayfield { get; set; }

        public GameManager(ContextData context)
        {
            _ctx = context;
            _ctx.GameManager = this;
        }

        // GameRetainedEventTopic ... builds a game-scoped evt topic using the stable GameIdentifier.
        public string GameRetainedEventTopic(string scope, string operation)
        {
            return "ESB/Client/" + GameIdentifier + "/" + scope + "/evt/" + operation;
        }

        // StorePendingRetained ... called by SubscriptionHandler when a retained message arrives before game entry.
        public void StorePendingRetained(string gameId, string scope, string operation, string payload)
        {
            _pendingRetained[gameId + ":" + scope + "/" + operation] = payload;
        }

        // ConsumePendingRetained ... drains a buffered payload for the current game after GameIdentifier is set.
        public string ConsumePendingRetained(string scope, string operation)
        {
            var key = GameIdentifier + ":" + scope + "/" + operation;
            string payload;
            _pendingRetained.TryGetValue(key, out payload);
            _pendingRetained.Remove(key);
            return payload;
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
            GameName = Path.GetFileName(SaveGamePath);
            GameIdentifier = IdentifierHelper.GenerateIdentifier(GameName, 8);
            GameMode = _ctx.ModApi.Application.Mode.ToString();

            string pending = ConsumePendingRetained("Registry", "BlockAndIdtemMapping");
            if (!string.IsNullOrEmpty(pending))
                ApplyMappingFromJson(pending);

            var json = new JObject(
                new JProperty("Status", "Created"));
            await _ctx.Messenger.SendAsync("App", MessageType.Log, "GameManager", json.ToString(Newtonsoft.Json.Formatting.None));
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
            SaveGamePath = Path.GetFullPath(_ctx.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            GameName = Path.GetFileName(SaveGamePath);
            GameIdentifier = IdentifierHelper.GenerateIdentifier(GameName, 8);
            GameMode = _ctx.ModApi.Application.Mode.ToString();

            string pending = ConsumePendingRetained("Registry", "BlockAndIdtemMapping");
            if (!string.IsNullOrEmpty(pending))
                ApplyMappingFromJson(pending);

            if (GameMode == "Client" && (BlockAndItemMapping == null || BlockAndItemMapping.Count == 0))
            {
                var raw = _ctx.ModApi.Application.GetBlockAndItemMapping();
                if (raw != null && raw.Count > 0)
                {
                    BlockAndItemMapping = new Dictionary<int, string>();
                    foreach (var pair in raw)
                        BlockAndItemMapping[pair.Value] = pair.Key;
                    _ = _ctx.Messenger.PublishRetainedAsync(
                            GameRetainedEventTopic("Registry", "BlockAndIdtemMapping"),
                            BuildMappingJson(),
                            3600u);
                }
            }

            _ctx.ModApi.Log($"IModApi properties: ClientPlayfield={((_ctx.ModApi.ClientPlayfield == null) ? "null" : "set")}, Network={(_ctx.ModApi.Network == null ? "null" : "set")}, GUI={(_ctx.ModApi.GUI == null ? "null" : "set")}, PDA={(_ctx.ModApi.PDA == null ? "null" : "set")}, Scripting={(_ctx.ModApi.Scripting == null ? "null" : "set")}, SoundPlayer={(_ctx.ModApi.SoundPlayer == null ? "null" : "set")}, Application={(_ctx.ModApi.Application == null ? "null" : "set")}");
        }

        private string BuildMappingJson()
        {
            var obj = new JObject();
            foreach (var pair in BlockAndItemMapping)
                obj[pair.Key.ToString()] = pair.Value;
            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}