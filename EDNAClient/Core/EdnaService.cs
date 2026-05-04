using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EDNAClient.Skills.Scripting;
using EDNAClient.Tray;
using EDNAClient.Workspace;
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
        private readonly WorkspaceWindow    _workspace;

        private GameWindowEventHook?  _windowHook;

        public EdnaService(IEdnaSkill[] skills, TrayIconManager tray, EdnaSettings settings, WorkspaceWindow workspace)
        {
            _ctx       = new EdnaContext();
            _skills    = skills;
            _tray      = tray;
            _settings  = settings;
            _workspace = workspace;
            _watcher   = new GameProcessWatcher();
            _luaHost   = new LuaScriptHost();
            _hotkeys   = new HotkeyManager();

            // Register skills that support document persistence with the workspace.
            foreach (var skill in _skills.OfType<IDocumentSkill>())
            {
                _workspace.RegisterDocumentSkill(skill);
                EdnaLogger.Log($"[EdnaService] registered IDocumentSkill: {skill.Id}");
            }
        }

        public void Start()
        {
            _watcher.GameStarted += OnGameStarted;
            _watcher.GameExited  += OnGameExited;
            EdnaLogger.Log("Watching for game process");
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
                    EdnaLogger.Log("Game process detected");

                    var esbInfo = WellKnownPaths.LoadEsbInfo();
                    var mqtt    = esbInfo?.MQTThost ?? new MqttConnectionSettings();
                    await _ctx.Messenger.ConnectAsync(_ctx, "EDNA",
                        mqtt.WithTcpServer, mqtt.Port, mqtt.Username, mqtt.Password, mqtt.CAFilePath);
                    EdnaLogger.Log($"MQTT connected to {mqtt.WithTcpServer ?? "localhost"}");

                    WellKnownPaths.SaveInfo(WellKnownPaths.EdnaInfoFile, new EdnaInfo
                    {
                        EnabledSkillIds = _settings.EnabledSkillIds
                    });

                    await _ctx.Messenger.SubscribeEventAsync("ESB/+/+/App/Evt/GameEnter",      OnGameEnter);      // TODO: refacor this approach
                    await _ctx.Messenger.SubscribeEventAsync("ESB/+/+/App/Evt/GameExit",       OnGameExit);       // TODO: refacor this approach
                    await _ctx.Messenger.SubscribeEventAsync("ESB/+/+/Playfield/Evt/Loaded",   OnPlayfieldLoaded); // TODO: refacor this approach
#if DEBUG
                    EdnaLogger.Log("Subscribed: GameEnter, GameExit, PlayfieldLoaded");
#endif

                    foreach (var skill in EnabledSkills())
                    {
                        await skill.StartAsync(_ctx.Messenger);
                        EdnaLogger.Log($"Skill '{skill.Id}' started");
                    }

                    foreach (var skill in EnabledSkills())
                        if (skill is IHotkeyProvider provider)
                            foreach (var req in provider.GetHotkeyRequests())
                                _hotkeys.Register(req);

                    _tray.UpdateState(IndicatorState.Healthy, gameRunning: true);
                    _tray.OnGameStarted();
                    _tray.ShowBalloon("EDNA Active", "Game detected \u2014 overlay enabled.");
                    EdnaLogger.Log("EDNA active");

                    EnsureHookInstalled();
                }
                catch (Exception ex)
                {
                    _tray.UpdateState(IndicatorState.Error, gameRunning: true);
                    _tray.ShowBalloon("EDNA Error", ex.Message);
                    EdnaLogger.Error("OnGameStarted failed", ex);
                }
            });
        }

        private void OnGameExited()
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                EdnaLogger.Log("Game process exited");
                _windowHook?.Dispose();
                _windowHook = null;
                _hotkeys.UnregisterAll();
                _luaHost.Stop();

                // Save workspace state while skills are still alive (nav/docs present),
                // then let skills close their UI, then do full skill teardown.
                CloseGameSession(processExiting: true);

                foreach (var skill in _skills) skill.Stop();

                _tray.OnGameExited();

                try { await _ctx.Messenger.DisconnectAsync(); }
                catch (Exception ex) { EdnaLogger.Warn($"Disconnect failed: {ex.Message}"); }

                _tray.UpdateState(IndicatorState.Offline, gameRunning: false);
                _watcher.Start();
            });
        }

        private Task OnPlayfieldLoaded(string topic, string payload)
        {
            try
            {
                var j      = JObject.Parse(payload);
                var ss     = j["SolarSystemName"]?.ToString() ?? "";
                var pf     = j["Name"]?.ToString() ?? "";
                var coords = j["SolarSystemCoordinates"];
                double x   = coords != null ? (double)(coords["X"] ?? 0) : 0;
                double y   = coords != null ? (double)(coords["Y"] ?? 0) : 0;
                double z   = coords != null ? (double)(coords["Z"] ?? 0) : 0;

                EdnaLogger.Log($"PlayfieldLoaded: system={ss} playfield={pf} coords=({x},{y},{z})");
                _tray.UpdateLocation(ss, pf);

                foreach (var skill in _skills.OfType<IPlayfieldObserver>())
                    skill.OnPlayfieldLoaded(ss, pf, x, y, z);
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("OnPlayfieldLoaded failed", ex);
            }
            return Task.CompletedTask;
        }

        private async Task OnGameEnter(string topic, string payload)
        {
            try
            {
                var j            = JObject.Parse(payload);
                var gameMode     = j["GameMode"]?.ToString();
                var saveGamePath = j["SaveGamePath"]?.ToString();

                EdnaLogger.Log($"GameEnter: mode={gameMode} path={saveGamePath}");

                _ctx.AuthoritativeSource = topic.Split('/')[1]; // TODO: refacor this approach

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    EnsureHookInstalled();
                    SnapWindows();
                    if (!_workspace.IsVisible) _workspace.Show();
                });

                if (!string.IsNullOrEmpty(saveGamePath))
                {
                    var ednaScriptsDir = Path.Combine(saveGamePath, "Content", "Mods", "ESB", "EDNA", "skills", "scripting");
                    await _luaHost.StartAsync(_ctx.Messenger, ednaScriptsDir);
                    EdnaLogger.Log($"Lua host started at {ednaScriptsDir}");

                    foreach (var skill in _skills)
                        if (skill is IGameContextReceiver receiver)
                        {
                            receiver.OnGameEnter(saveGamePath);
                            EdnaLogger.Log($"Notified {skill.Id} of GameEnter");
                        }

                    // Restore documents from the previous session on the UI thread,
                    // after OnGameEnter has set up each skill's per-session state.
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var skill in _skills.OfType<IDocumentSkill>())
                        {
                            var ids = _workspace.GetSavedDocuments(skill.Id);
                            if (ids.Count > 0)
                            {
                                EdnaLogger.Log($"[EdnaService] restoring {ids.Count} document(s) for '{skill.Id}'");
                                skill.RestoreDocuments(ids);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("OnGameEnter failed", ex);
            }

            _luaHost.Broadcast("on_game_enter", topic, payload);
        }

        private Task OnGameExit(string topic, string payload)
        {
            EdnaLogger.Log("GameExit received (lobby)");
            try
            {
                _luaHost.Broadcast("on_game_exit", topic, payload);
                _luaHost.Stop();
                Application.Current.Dispatcher.InvokeAsync(() => CloseGameSession(processExiting: false));
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("OnGameExit failed", ex);
            }
            return Task.CompletedTask;
        }

        // Saves workspace state, calls OnGameExit on skills (closes UI), hides workspace.
        // If processExiting=true, skill.Stop() follows immediately; if false, skills stay
        // running (MQTT subscriptions kept alive) awaiting the next GameEnter.
        private void CloseGameSession(bool processExiting)
        {
            EdnaLogger.Log($"[EdnaService] CloseGameSession processExiting={processExiting}");
            _tray.ClearLocation();
            _workspace.SaveAndHide();

            foreach (var skill in _skills)
                if (skill is IGameContextReceiver receiver)
                    try { receiver.OnGameExit(); }
                    catch (Exception ex) { EdnaLogger.Error($"OnGameExit failed for '{skill.Id}'", ex); }
        }

        private void EnsureHookInstalled()
        {
            if (_windowHook != null) return;
            var hwnd = GameWindowLocator.GetWindowHandle();
            if (hwnd == IntPtr.Zero)
            {
#if DEBUG
                EdnaLogger.Log("EnsureHookInstalled: game window not found");
#endif
                return;
            }
            _windowHook = new GameWindowEventHook(hwnd, () =>
                Application.Current.Dispatcher.Invoke(SnapWindows));
#if DEBUG
            EdnaLogger.Log("Window event hook installed");
#endif
        }

        private void SnapWindows()
        {
            foreach (var skill in _skills) skill.SnapToGameWindow();
        }

        private IEnumerable<IEdnaSkill> EnabledSkills() =>
            _skills.Where(s => _settings.EnabledSkillIds.Contains(s.Id));
    }
}
