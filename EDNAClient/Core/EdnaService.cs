using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EDNAClient.Skills.Scripting;
using EDNAClient.Startup;
using EDNAClient.Setup;
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
        private readonly LuaScriptHost      _luaHost;
        private readonly HotkeyManager      _hotkeys;
        private readonly WorkspaceWindow    _workspace;

        private EdnaInfo             _settings = new EdnaInfo();
        private GameWindowEventHook? _windowHook;

        public EdnaService(IEdnaSkill[] skills, TrayIconManager tray, WorkspaceWindow workspace)
        {
            _ctx       = new EdnaContext();
            _skills    = skills;
            _tray      = tray;
            _workspace = workspace;
            _luaHost   = new LuaScriptHost();
            _hotkeys   = new HotkeyManager();

            foreach (var skill in _skills.OfType<IDocumentSkill>())
            {
                _workspace.RegisterDocumentSkill(skill);
                EdnaLogger.Log($"[EdnaService] registered IDocumentSkill: {skill.Id}");
            }
        }

        public async Task StartAsync()
        {
            var startupState = await Task.Run(() => new StartupOrchestrator().Detect());

            var esbInfo = WellKnownPaths.LoadEsbInfo();
            var mqtt    = esbInfo?.MQTThost ?? new MqttConnectionSettings();
            _settings   = esbInfo?.EDNA ?? new EdnaInfo();

            Exception? connectError = null;
            try
            {
                _ctx.Bus = new BusBuilder()
                    .WithMessenger(_ctx.Messenger)
                    .WithParticipantType("EDNA")
                    .WithConnection(mqtt.WithTcpServer, mqtt.Port)
                    .WithCredentials(mqtt.Username, mqtt.Password)
                    .WithCertificate(mqtt.CAFilePath)
                    .Build();

                await _ctx.Bus.ConnectAsync();
                EdnaLogger.Log($"MQTT connected to {mqtt.WithTcpServer ?? "localhost"}");

                // EDNA shares its MachineId with the bound Client (same machine, same bus.token),
                // so the lobby rcId derives to the same value on both sides.
                _ctx.LobbyRcId = RoutingContextId.Lobby(_ctx.Bus.MachineId).Id;
                await _ctx.Bus.SwitchContextAsync(_ctx.LobbyRcId);

                WellKnownPaths.SaveEdnaSettings(_settings);

                _ctx.Bus.OnEvent("Announcements", "Connect", OnClientConnect);
                _ctx.Bus.OnEvent("App",           "GameEnter", OnGameEnter);
                _ctx.Bus.OnEvent("App",           "GameExit",  OnGameExit);
                _ctx.Bus.OnEvent("Playfield",     "Loaded",    OnPlayfieldLoaded);

                foreach (var skill in EnabledSkills())
                {
                    await skill.StartAsync(_ctx);
                    EdnaLogger.Log($"Skill '{skill.Id}' started");
                }

                foreach (var skill in EnabledSkills())
                    if (skill is IHotkeyProvider provider)
                        foreach (var req in provider.GetHotkeyRequests())
                            _hotkeys.Register(req);

                _tray.SetConnected();
                EdnaLogger.Log("EDNA connected and ready");

                // Lobby/offline level-0 navbar -- visible until a game is entered.
                await Application.Current.Dispatcher.InvokeAsync(AddSavesNavSection);
            }
            catch (Exception ex)
            {
                connectError = ex;
                _tray.SetMqttDown();
                EdnaLogger.Error("MQTT connect failed", ex);
            }

            // FUTURE-provisioning report. The bus-connect outcome above IS the broker probe --
            // no separate test is run. Local log always; MQTT publish only when bus came up
            // (in which case the broker section is necessarily REACHABLE + AUTHENTICATED).
            var provisioningReport = ProvisioningDetector.BuildReport(startupState, mqtt, connectError);
            EdnaLogger.Log(provisioningReport);
            if (connectError == null)
            {
                try
                {
                    await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "Provisioning", "Detection", provisioningReport);
                }
                catch (Exception ex)
                {
                    EdnaLogger.Warn("Provisioning report publish failed: " + ex.Message);
                }
            }
        }

        public async Task StopAsync()
        {
            _windowHook?.Dispose();
            _windowHook = null;
            _luaHost.Stop();
            foreach (var skill in _skills) skill.Stop();
            _hotkeys.Dispose();
            if (_ctx.Bus != null)
                await _ctx.Bus.DisconnectAsync();
        }

        // OnClientConnect ... EDNA follows the Client's retained context manifest. Connect carries
        // a ContextRcId field naming where the Client is (or is about to be); when that disagrees
        // with EDNA's current bus context, EDNA swaps. This is what unblocks game-entry: the Client
        // republishes Connect on the lobby with ContextRcId=GameRcId BEFORE its own SwitchContextAsync,
        // and EDNA reacts here, before GameEnter is published on the game audience.
        private async Task OnClientConnect(MessageEnvelope env)
        {
            try
            {
                if (env.SenderType != "Client") return;
                var j = env.PayloadJson;
                if (j == null) return;
                var target = (string?)j["ContextRcId"];
                if (string.IsNullOrEmpty(target)) return;
                if (target == _ctx.Bus.ContextRcId) return;
                EdnaLogger.Log($"Connect manifest follow: {_ctx.Bus.ContextRcId} -> {target}");
                await _ctx.Bus.SwitchContextAsync(target);
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("OnClientConnect failed", ex);
            }
        }

        private Task OnPlayfieldLoaded(MessageEnvelope env)
        {
            try
            {
                var j      = env.PayloadJson ?? new JObject();
                var ss     = (string?)j["SolarSystemName"] ?? "";
                var pf     = (string?)j["Name"] ?? "";
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

        private async Task OnGameEnter(MessageEnvelope env)
        {
            try
            {
                var j            = env.PayloadJson ?? new JObject();
                var gameMode     = (string?)j["GameMode"];
                var saveGamePath = (string?)j["SaveGamePath"];
                var gameRcId     = (string?)j["GameRcId"];

                EdnaLogger.Log($"GameEnter: mode={gameMode} path={saveGamePath} rcId={gameRcId}");

                _ctx.AuthoritativeSource = env.SenderType;
                if (!string.IsNullOrEmpty(gameRcId))
                    _ctx.GameRcId = gameRcId;
                // Context switch happens in OnClientConnect ahead of this event; no swap needed here.

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    EnsureHookInstalled();
                    SnapWindows();
                    if (!_workspace.IsVisible) _workspace.Show();
                    _workspace.CaptureNavExpansion();
                    _workspace.NavViewModel.RemoveRootSection(SavesNavBuilder.RootSectionName);
                    _workspace.SetNavTitle(SaveNameFromPath(saveGamePath));
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

                _tray.SetInGame();
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("OnGameEnter failed", ex);
            }

            _luaHost.Broadcast("on_game_enter", env.SenderType, env.RawPayload);
        }

        private async Task OnGameExit(MessageEnvelope env)
        {
            EdnaLogger.Log("GameExit received (lobby)");
            try
            {
                _luaHost.Broadcast("on_game_exit", env.SenderType, env.RawPayload);
                _luaHost.Stop();
                _ctx.GameRcId = null;
                // Context switch happens in OnClientConnect ahead of this event; no swap needed here.
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _windowHook?.Dispose();
                    _windowHook = null;
                    _hotkeys.UnregisterAll();
                    CloseGameSession();
                    AddSavesNavSection();
                    _workspace.SetNavTitle(null);
                    _tray.SetConnected();
                });
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("OnGameExit failed", ex);
            }
        }

        private void CloseGameSession()
        {
            _tray.ClearLocation();
            _workspace.SaveState();

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

        // Adds the lobby/offline level-0 "Games" section to the navbar. No-op when
        // Empyrion install or Saves directory cannot be resolved. Called on lobby
        // entry and on game exit. Must run on the UI dispatcher (ObservableCollection).
        private void AddSavesNavSection()
        {
            var node = SavesNavBuilder.Build();
            if (node == null) return;
            _workspace.NavViewModel.AddRootSection(node);
        }

        // Derives a display-friendly save name from the saveGamePath supplied on GameEnter.
        // Returns null when the path is empty or unparseable -- caller treats null as
        // "fall back to default title".
        private static string? SaveNameFromPath(string? saveGamePath)
        {
            if (string.IsNullOrEmpty(saveGamePath)) return null;
            try { return Path.GetFileName(saveGamePath.TrimEnd('\\', '/')); }
            catch { return null; }
        }
    }
}
