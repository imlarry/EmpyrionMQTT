using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EDNAClient.Scripting;
using EDNAClient.Tray;
using ESB.Messaging;
using EDNAClient.Configuration;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Core
{
    public class EdnaService
    {
        private readonly EdnaContext        _ctx;
        private readonly IEdnaSkill[]       _skills;
        private readonly TrayIconManager    _tray;
        private readonly EdnaSettings       _settings;
        private readonly GameProcessWatcher _watcher;
        private readonly LuaScriptHost      _luaHost;
        private readonly HotkeyManager      _hotkeys;

        private GameWindowEventHook?  _windowHook;
        private HashSet<string>       _blockedByPolicy = new();

        public EdnaService(IEdnaSkill[] skills, TrayIconManager tray, EdnaSettings settings)
        {
            _ctx      = new EdnaContext();
            _skills   = skills;
            _tray     = tray;
            _settings = settings;
            _watcher  = new GameProcessWatcher();
            _luaHost  = new LuaScriptHost();
            _hotkeys  = new HotkeyManager();
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
            _hotkeys.Dispose();
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

                    foreach (var skill in EnabledSkills())
                        if (skill is IHotkeyProvider provider)
                            foreach (var req in provider.GetHotkeyRequests())
                                _hotkeys.Register(req);

                    _tray.UpdateState(IndicatorState.Healthy, gameRunning: true);
                    _tray.OnGameStarted();
                    _tray.ShowBalloon("EDNA Active", "Game detected \u2014 overlay enabled.");

                    EnsureHookInstalled();
                }
                catch (Exception ex)
                {
                    _tray.UpdateState(IndicatorState.Error, gameRunning: true);
                    _tray.ShowBalloon("EDNA Error", ex.Message);
                    System.Diagnostics.Debug.WriteLine($"[EDNA] OnGameStarted failed: {ex}");
                }
            });
        }

        private void OnGameExited()
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                _windowHook?.Dispose();
                _windowHook = null;

                _hotkeys.UnregisterAll();
                _luaHost.Stop();
                foreach (var skill in _skills) skill.Stop();
                _blockedByPolicy.Clear();

                _tray.OnGameExited();

                try { await _ctx.Messenger.DisconnectAsync(); } catch { }

                _tray.UpdateState(IndicatorState.Offline, gameRunning: false);
                _watcher.Start();
            });
        }

        private async Task OnGameEnter(string topic, string payload)
        {
            EnsureHookInstalled();
            SnapWindows();

            // Start Lua host with the save-game-specific scripts directory
            try
            {
                var j            = JObject.Parse(payload);
                var gameMode     = j["GameMode"]?.ToString();
                var saveGamePath = j["SaveGamePath"]?.ToString();

                // Resolve which ESB instance owns authoritative game state.
                // ApplicationName is captured at mod-load (lobby) so the SourceId is always
                // "Client" for the player-side ESB. In SP that instance handles everything;
                // in MP a separate DedicatedServer instance is the authority for V1 calls.
                _ctx.AuthoritativeSource = gameMode == "SinglePlayer"
                    ? topic.Split('/')[0]                          // "Client" in SP
                    : "DedicatedServer";                           // separate Dedi in MP
                if (!string.IsNullOrEmpty(saveGamePath))
                {
                    var ednaScriptsDir = Path.Combine(saveGamePath, "Content", "Mods", "ESB", "EDNA");
                    await _luaHost.StartAsync(_ctx.Messenger, ednaScriptsDir);
                }
            }
            catch { }

            _luaHost.Broadcast("on_game_enter", topic, payload);
        }

        private Task OnGameExit(string topic, string payload)
        {
            _luaHost.Broadcast("on_game_exit", topic, payload);
            _luaHost.Stop();
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
