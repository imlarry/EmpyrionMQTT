using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EDNAClient.Scripting;
using EDNAClient.Skills.StatusPill;
using EDNAClient.Tray;
using ESB.Messaging;
using ESB.Messaging.Configuration;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Core
{
    public class EdnaService
    {
        private readonly EdnaContext        _ctx;
        private readonly IEdnaSkill[]       _skills;
        private readonly StatusPillSkill    _pill;
        private readonly TrayIconManager    _tray;
        private readonly EdnaSettings       _settings;
        private readonly GameProcessWatcher _watcher;
        private readonly LuaScriptHost      _luaHost;

        private GameWindowEventHook?  _windowHook;
        private HashSet<string>       _blockedByPolicy = new();

        public EdnaService(IEdnaSkill[] skills, StatusPillSkill pill,
                           TrayIconManager tray, EdnaSettings settings)
        {
            _ctx      = new EdnaContext();
            _skills   = skills;
            _pill     = pill;
            _tray     = tray;
            _settings = settings;
            _watcher  = new GameProcessWatcher();
            _luaHost  = new LuaScriptHost();
        }

        public void Start()
        {
            _watcher.GameStarted += OnGameStarted;
            _watcher.GameExited  += OnGameExited;
            _watcher.Start();
        }

        public async Task StopAsync()
        {
            _windowHook?.Dispose();
            _windowHook = null;
            _luaHost.Stop();
            foreach (var skill in _skills) skill.Stop();
            _watcher.Dispose();
            await _ctx.Messenger.DisconnectAsync();
        }

        private void OnGameStarted()
        {
            Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var esbInfo = WellKnownPaths.LoadEsbInfo();
                    var mqtt    = esbInfo?.MQTThost ?? new MqttConnectionSettings();
                    await _ctx.Messenger.ConnectAsync(_ctx, "EDNA",
                        mqtt.WithTcpServer, mqtt.Port, mqtt.Username, mqtt.Password, mqtt.CAFilePath);

                    // Write EDNA_Info.yaml with current settings so other clients can discover our config
                    WellKnownPaths.SaveInfo(WellKnownPaths.EdnaInfoFile, new EdnaInfo
                    {
                        EnabledSkillIds = _settings.EnabledSkillIds
                    });

                    await _ctx.Messenger.SubscribeEventAsync("+/E/Application.GameEnter/+/+", OnGameEnter);
                    await _ctx.Messenger.SubscribeEventAsync("+/E/Application.GameExit/+/+",  OnGameExit);
                    await _ctx.Messenger.SubscribeEventAsync("+/E/Application.EdnaPolicy/+/+", OnPolicyReceived);

                    foreach (var skill in EnabledSkills())
                        await skill.StartAsync(_ctx.Messenger);

                    _pill.UpdateIndicator(IndicatorState.Healthy);
                    _tray.UpdateState(IndicatorState.Healthy, gameRunning: true);
                    _tray.OnGameStarted(_pill.Window);
                    _tray.ShowBalloon("EDNA Active", "Game detected \u2014 HUD overlay enabled.");

                    EnsureHookInstalled();
                }
                catch
                {
                    _pill.UpdateIndicator(IndicatorState.Error);
                    _tray.UpdateState(IndicatorState.Error, gameRunning: true);
                    _tray.ShowBalloon("EDNA Error", "Could not connect to MQTT broker.");
                }
            });
        }

        private void OnGameExited()
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                _windowHook?.Dispose();
                _windowHook = null;

                _luaHost.Stop();
                foreach (var skill in _skills) skill.Stop();
                _blockedByPolicy.Clear();

                _tray.OnGameExited();

                try { await _ctx.Messenger.DisconnectAsync(); } catch { }

                _pill.UpdateIndicator(IndicatorState.Offline);

                Application.Current.Shutdown();
            });
        }

        private async Task OnGameEnter(string topic, string payload)
        {
            EnsureHookInstalled();
            SnapWindows();

            // Start Lua host with the game-specific scripts directory
            try
            {
                var j            = JObject.Parse(payload);
                var gameMode     = j["GameMode"]?.ToString();
                var gameDataPath = j["GameDataPath"]?.ToString();

                // Resolve which ESB instance owns authoritative game state.
                // ApplicationName is captured at mod-load (lobby) so the SourceId is always
                // "Client" for the player-side ESB. In SP that instance handles everything;
                // in MP a separate DedicatedServer instance is the authority for V1 calls.
                _ctx.AuthoritativeSource = gameMode == "SinglePlayer"
                    ? topic.Split('/')[0]                          // "Client" in SP
                    : "DedicatedServer";                           // separate Dedi in MP
                if (!string.IsNullOrEmpty(gameDataPath))
                {
                    var ednaScriptsDir = Path.Combine(gameDataPath, "EDNA", "scripts");
                    await _luaHost.StartAsync(_ctx.Messenger, ednaScriptsDir);
                }
            }
            catch { }

            _luaHost.Broadcast("on_game_enter", topic, payload);
        }

        private Task OnGameExit(string topic, string payload)
        {
            _luaHost.Broadcast("on_game_exit", topic, payload);
            return Task.CompletedTask;
        }

        private Task OnPolicyReceived(string topic, string payload)
        {
            try
            {
                var j       = JObject.Parse(payload);
                var allowed = j["AllowedSkills"];

                if (allowed == null || allowed.Type == JTokenType.Null)
                {
                    // No restriction — clear any existing policy block
                    _blockedByPolicy.Clear();
                    return Task.CompletedTask;
                }

                var allowedIds = allowed.ToObject<HashSet<string>>() ?? new HashSet<string>();
                _blockedByPolicy = _skills.Select(s => s.Id).Except(allowedIds).ToHashSet();

                foreach (var skill in _skills.Where(s => _blockedByPolicy.Contains(s.Id)))
                    skill.Stop();
            }
            catch { }
            return Task.CompletedTask;
        }

        private void EnsureHookInstalled()
        {
            if (_windowHook != null) return;
            var hwnd = GameWindowLocator.GetWindowHandle();
            if (hwnd == IntPtr.Zero) return;
            _windowHook = new GameWindowEventHook(hwnd, () =>
                Application.Current.Dispatcher.Invoke(SnapWindows));
        }

        private void SnapWindows()
        {
            foreach (var skill in _skills) skill.SnapToGameWindow();
        }

        private IEnumerable<IEdnaSkill> EnabledSkills() =>
            _skills.Where(s => _settings.EnabledSkillIds.Contains(s.Id)
                            && !_blockedByPolicy.Contains(s.Id));
    }
}
