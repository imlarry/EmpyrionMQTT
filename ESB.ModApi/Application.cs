using Eleon.Modding;
using ESBGameMod;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModApi
{
    public class Application
    {
        private readonly ContextData _ctx;

        public Application(ContextData ctx)
        {
            _ctx = ctx;
            _ = _ctx.Messenger.Subscribe("ESB/Test", Test);
            _ = _ctx.Messenger.Subscribe("ESB/Test2", Test2);
            _ = _ctx.Messenger.Subscribe("ESB/#", Wildcard);
        }

        // In this example, you would use Application.CreateAsync(ctx) instead of new Application(ctx)
        public static async Task<Application> CreateAsync(ContextData ctx)
        {
            var app = new Application(ctx);
            await app.ApplicationQueries("ESB/Client/ModApi.Application.#/Q", "");
            return app;
        }

        async Task ApplicationQueries(string topic, string payload)
        {
            // this is intended as the switchboard that routes calls based on topic
            await _ctx.Messenger.SendAsync("ApplicationQueries", "");
        }

        async void Test(string topic, string payload)
        {
            await _ctx.Messenger.SendAsync(topic, "");
        }

        async void Test2(string topic, string payload)
        {
            await _ctx.Messenger.SendAsync(topic, "");
        }

        async void Wildcard(string topic, string payload)
        {
            await _ctx.Messenger.SendAsync(topic, "");
        }

        // Message Entry Points

        /// <summary>
        /// The reply to a GetPathFor message is the path for specific game sub-directories based on the "AppFolder" property 
        /// in the JSON payload. This value must be from the AppFolder enum which includes Root, Content, SaveGame, Mod, ActiveScenario,
        /// Cache, and Dedicated. The use of a /../ parent directory reference and the switch to forward slashes, which do not need 
        /// to be escaped, implies these paths are derived from the System.AppDomain.CurrentDomain.BaseDirectory of the appropriate
        /// Main executable in either the Client, DedicatedServer, or PlayfieldServer directory.
        /// 
        /// Note: These paths contain the game name which include spaces. Subsequent calls using the returned path may require the use
        /// of enclosing quotes to ensure a valid path.
        /// </summary>
        public async void GetPathFor(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                Enum.TryParse(applicationArgs.GetValue("AppFolder").ToString(), true, out AppFolder appFolder);
                var path = _ctx.ModApi.Application.GetPathFor(appFolder);
                JObject json = new JObject(new JProperty("Path", path));
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void GetAllPlayfields(string topic, string payload)
        {
            try
            {
                var playfieldDescr = _ctx.ModApi.Application.GetAllPlayfields();
                var playfieldDict = new List<Dictionary<string, object>>();
                foreach (var playfield in playfieldDescr)
                {
                    var data = new Dictionary<string, object>
                    {
                        { "PlayfieldName", playfield.PlayfieldName },
                        { "PlayfieldType", playfield.PlayfieldType },
                        { "IsInstance", playfield.IsInstance }
                    };
                    playfieldDict.Add(data);
                }
                var json = JsonConvert.SerializeObject(playfieldDict);
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void GetPfServerInfos(string topic, string payload)
        {
            try
            {
                var pfServerInfos = _ctx.ModApi.Application.GetPfServerInfos();
                if (pfServerInfos != null)
                {
                    string json = JsonConvert.SerializeObject(pfServerInfos);
                    await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json);
                }
                else
                {
                    await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), "call returned null");
                }

            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void GetPlayerEntityIds(string topic, string payload)
        {
            try
            {
                var playerEntityIds = _ctx.ModApi.Application.GetPlayerEntityIds();
                string json = JsonConvert.SerializeObject(playerEntityIds);
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void GetPlayerDataFor(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                int? playerEntityId = applicationArgs.GetValue("PlayerEntityId")?.Value<int>() ?? null;
                if (playerEntityId.HasValue)
                {
                    var playerData = _ctx.ModApi.Application.GetPlayerDataFor(playerEntityId.Value);
                    string json = JsonConvert.SerializeObject(playerData);
                    await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json);
                }
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void SendChatMessage(string topic, string payload)
        {
            try
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), "stub");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        void DialogActionHandler(int buttonIdx, string linkId, string inputContent, int playerId, int customValue)
        {
            //_ = _ctx.Messenger.SendAsync("ModApi.Application.ShowDialogBox/X", "Button:" + buttonIdx.ToString());
            _ctx.ModApi.Log("entering ShowDialogBox actionRoutine");
        }
        public async void ShowDialogBox(string topic, string payload)
        {
            try
            {
                _ctx.ModApi.Log("entering ShowDialogBox");
                var playerId = 1040;
                var playerData = _ctx.ModApi.Application.GetPlayerDataFor(playerId);
                string json = JsonConvert.SerializeObject(playerData);
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json);
                string[] bt = { "dog", "cat", "duck" };
                var config = new DialogConfig() // the parens here forces calling the constructor (which probably populates stuff behind the curtain!)
                {
                    TitleText = "TitleText - Your Title Here",
                    BodyText = "BodyText - This is a test of the emergency broadcast system (with buttons)",
                    ButtonTexts = bt
                    //ButtonIdxForEsc = 0,
                    //ButtonIdxForEnter = 1,
                    //CloseOnLinkClick = true,
                    //MaxChars = 30,
                    //Placeholder = "Placeholder",
                    //InitialContent = "InitialContent"
                };
                var displayed = _ctx.ModApi.Application.ShowDialogBox(playerId, config, DialogActionHandler, 0);
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), "Displayed: " + displayed.ToString());
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void GetStructure(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                var entityId = applicationArgs.GetValue("EntityId").Value<int>();
                async void ResultCallback(GlobalStructureInfo globalStructureInfo)
                {
                    await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), globalStructureInfo.name);
                }
                _ctx.ModApi.Application.GetStructure(entityId, ResultCallback);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void GetStructures(string topic, string payload)
        {
            try
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), "stub");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void GetBlockAndItemMapping(string topic, string payload)
        {
            try
            {
                var blockAndItemMapping = _ctx.ModApi.Application.GetBlockAndItemMapping();
                var json = JsonConvert.SerializeObject(blockAndItemMapping);
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        public async void State(string topic, string payload)
        {
            var state = _ctx.ModApi.Application.State;
            JObject json = new JObject(new JProperty("State", state.ToString()));
            await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public async void Mode(string topic, string payload)
        {
            var mode = _ctx.ModApi.Application.Mode;
            JObject json = new JObject(new JProperty("Mode", mode.ToString()));
            await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public async void LocalPlayer(string topic, string payload)
        {
            var localPlayer = _ctx.ModApi.Application.LocalPlayer;
            JObject json = new JObject(new JProperty("Stubbed", "stub"));
            await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public async void GameTicks(string topic, string payload)
        {
            var gameTicks = _ctx.ModApi.Application.GameTicks;
            JObject json = new JObject(new JProperty("GameTicks", gameTicks));
            await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
