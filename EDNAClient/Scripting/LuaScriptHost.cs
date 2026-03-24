using EDNAClient.Scripting.Api;
using ESB.Messaging;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace EDNAClient.Scripting;

/// <summary>
/// Manages Lua scripts loaded from a directory. Provides:
///   - Isolated per-script LuaEngine instances (one engine per .lua file)
///   - Shared LuaBlackboard ('bb') across all engines — reactive key-value coordination
///   - Hot-reload via FileSystemWatcher (save a file → reloads in ~100ms)
///   - Broadcast() — C# fires named events into all scripts
///   - Call()       — C# invokes a named function in a specific script
///   - CreateInstance() — C# calls a Lua factory, wraps the result as a LuaInstance
///
/// Script directory  : {SaveGamePath}/Content/Mods/EDNA/scripts/  (game-specific, hot-reload)
/// Library directory : {AppBase}/lib/                              (shipped with app)
///
/// Execution model:
///   Scripts subscribe to MQTT topics at load time. Decisions happen when events
///   arrive — no C# tick loop. ESB /E/ messages are the clock.
///
/// Blackboard coordination:
///   Scripts that own a topic write to bb on each delivery.
///   Dependent scripts register watchers via bb:watch(key, fn).
///   Watchers fire synchronously on the WPF dispatcher thread when bb:set() is called.
/// </summary>
public sealed class LuaScriptHost : IDisposable
{
    private string _scriptsDirectory = string.Empty;

    private readonly LuaBlackboard                          _blackboard = new();
    private readonly Dictionary<string, LuaEngine>          _engines    = new();
    private readonly Dictionary<string, List<LuaInstance>>  _instances  = new();
    private FileSystemWatcher? _watcher;
    private IMessenger?        _messenger;

    // ── One-time process-level setup ───────────────────────────────────────

    static LuaScriptHost()
    {
        UserData.RegisterType<LuaMqttApi>();
        UserData.RegisterType<LuaLogApi>();
        UserData.RegisterType<LuaBbApi>();
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public Task StartAsync(IMessenger messenger, string scriptsDirectory)
    {
        _messenger         = messenger;
        _scriptsDirectory  = scriptsDirectory;

        // Configure require() paths for all Script instances:
        //   1. scripts/ — game-specific scripts and user-provided overrides of library modules
        //   2. {AppBase}/lib/ — library modules shipped with EDNA (edna.bt, edna.drone, …)
        // MoonSharp replaces '.' with '/' in module names before matching '?'.
        var loader = new FileSystemScriptLoader
        {
            ModulePaths = new[]
            {
                Path.Combine(_scriptsDirectory, "?.lua"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "?.lua"),
            }
        };
        Script.DefaultOptions.ScriptLoader = loader;

        _blackboard.OnWatcherError = (key, script, detail) =>
            PublishInfo("LuaScriptHost.Error",
                $"{{\"Script\":{JsonConvert.SerializeObject(script)},\"Error\":\"WatcherError\",\"Key\":{JsonConvert.SerializeObject(key)},\"Detail\":{JsonConvert.SerializeObject(detail)}}}");
        Directory.CreateDirectory(_scriptsDirectory);
        LoadAll();
        StartWatcher();
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;

        // Invalidate C#-held instances
        foreach (var list in _instances.Values)
            foreach (var inst in list) inst.Invalidate();
        _instances.Clear();

        // Clear blackboard watchers then reset data — session is over
        foreach (var engine in _engines.Values)
            _blackboard.ClearWatchers(engine);
        _blackboard.Reset();

        foreach (var engine in _engines.Values)
            Log($"Stopped: {engine.Name}");
        _engines.Clear();
    }

    public void Dispose() => Stop();

    // ── C# → Lua dispatch ─────────────────────────────────────────────────

    /// <summary>
    /// Fire a named global function in every loaded script that defines it.
    /// Used for lifecycle events (on_game_enter, on_game_exit) that originate in C#
    /// rather than on the MQTT bus.
    /// Must be called on the WPF dispatcher thread.
    /// </summary>
    public void Broadcast(string functionName, params object[] args)
    {
#if DEBUG
        PublishInfo("LuaScriptHost.Broadcast", $"{{\"Function\":\"{functionName}\",\"Scripts\":{JsonConvert.SerializeObject(_engines.Keys)}}}");
#endif
        foreach (var engine in _engines.Values)
        {
            try   { engine.CallFunction(functionName, args); }
            catch (ScriptRuntimeException ex)
            {
                Log($"Broadcast '{functionName}' error in '{engine.Name}': {ex.DecoratedMessage}");
                PublishInfo("LuaScriptHost.Error", $"{{\"Script\":\"{engine.Name}\",\"Error\":\"BroadcastError\",\"Function\":\"{functionName}\",\"Detail\":{JsonConvert.SerializeObject(ex.DecoratedMessage)}}}");
            }
        }
    }

    /// <summary>
    /// Call a named global function in a specific script.
    /// Returns null if the script is not loaded or the function is not defined.
    /// Must be called on the WPF dispatcher thread.
    /// </summary>
    public DynValue? Call(string scriptName, string functionName, params object[] args)
    {
        if (!_engines.TryGetValue(scriptName, out var engine)) return null;
        try   { return engine.CallFunction(functionName, args); }
        catch (ScriptRuntimeException ex)
        {
            Log($"Call '{scriptName}.{functionName}' error: {ex.DecoratedMessage}");
            PublishInfo("LuaScriptHost.Error",
                $"{{\"Script\":{JsonConvert.SerializeObject(scriptName)},\"Error\":\"CallError\",\"Function\":{JsonConvert.SerializeObject(functionName)},\"Detail\":{JsonConvert.SerializeObject(ex.DecoratedMessage)}}}");
            return null;
        }
    }

    /// <summary>
    /// Call a Lua factory function in a specific script and wrap the returned table
    /// as a LuaInstance. Returns null if the script is not loaded, the factory is
    /// missing, or the factory does not return a table.
    ///
    /// Instances are automatically invalidated when their script hot-reloads.
    /// Check instance.IsValid before calling; recreate if false.
    ///
    /// Must be called on the WPF dispatcher thread.
    /// </summary>
    public LuaInstance? CreateInstance(
        string scriptName, string factoryName, string instanceId, params object[] args)
    {
        if (!_engines.TryGetValue(scriptName, out var engine)) return null;

        DynValue? result;
        try   { result = engine.CallFunction(factoryName, args); }
        catch (ScriptRuntimeException ex)
        {
            Log($"CreateInstance '{scriptName}.{factoryName}' error: {ex.DecoratedMessage}");
            return null;
        }

        if (result == null || result.Type != DataType.Table)
        {
            Log($"CreateInstance '{scriptName}.{factoryName}' did not return a table");
            return null;
        }

        var instance = new LuaInstance(scriptName, instanceId, engine, result);

        if (!_instances.TryGetValue(scriptName, out var list))
            _instances[scriptName] = list = new();
        list.Add(instance);

        return instance;
    }

    // ── File loading ───────────────────────────────────────────────────────

    private void LoadAll()
    {
        foreach (var path in Directory.GetFiles(_scriptsDirectory, "*.lua"))
            LoadScript(path);
    }

    private void LoadScript(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        if (_engines.TryGetValue(name, out var old))
        {
            // Invalidate C#-held instances from this script
            if (_instances.TryGetValue(name, out var stale))
            {
                foreach (var inst in stale) inst.Invalidate();
                _instances.Remove(name);
            }

            // Purge blackboard watchers registered by the old engine
            _blackboard.ClearWatchers(old);

            _engines.Remove(name);
            Log($"Reloading: {name}");
        }

        var engine = new LuaEngine(name);

        if (_messenger != null)
        {
            engine.SetGlobal("mqtt", UserData.Create(new LuaMqttApi(_messenger, engine)));
            engine.SetGlobal("log",  UserData.Create(new LuaLogApi(name, _messenger)));
            engine.SetGlobal("bb",   UserData.Create(new LuaBbApi(_blackboard, engine)));
        }

        try
        {
            engine.ExecuteFile(path);
            _engines[name] = engine;
            Log($"Loaded: {name}");
#if DEBUG
            PublishInfo("LuaScriptHost.Loaded", $"{{\"Script\":\"{name}\"}}");
#endif
        }
        catch (SyntaxErrorException ex)
        {
            Log($"Syntax error in '{name}': {ex.DecoratedMessage}");
            PublishInfo("LuaScriptHost.Error", $"{{\"Script\":\"{name}\",\"Error\":\"SyntaxError\",\"Detail\":{JsonConvert.SerializeObject(ex.DecoratedMessage)}}}");
        }
        catch (ScriptRuntimeException ex)
        {
            Log($"Runtime error in '{name}': {ex.DecoratedMessage}");
            PublishInfo("LuaScriptHost.Error", $"{{\"Script\":\"{name}\",\"Error\":\"RuntimeError\",\"Detail\":{JsonConvert.SerializeObject(ex.DecoratedMessage)}}}");
        }
    }

    private void UnloadScript(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        if (_engines.TryGetValue(name, out var engine))
        {
            if (_instances.TryGetValue(name, out var stale))
            {
                foreach (var inst in stale) inst.Invalidate();
                _instances.Remove(name);
            }
            _blackboard.ClearWatchers(engine);
            _engines.Remove(name);
            Log($"Unloaded: {name}");
        }
    }

    // ── Hot-reload watcher ─────────────────────────────────────────────────

    private void StartWatcher()
    {
        _watcher = new FileSystemWatcher(_scriptsDirectory, "*.lua")
        {
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents   = true,
        };

        _watcher.Created += (_, e) => ReloadOnDispatcher(e.FullPath);
        _watcher.Changed += (_, e) => ReloadOnDispatcher(e.FullPath);
        _watcher.Deleted += (_, e) => Application.Current.Dispatcher.Invoke(() => UnloadScript(e.FullPath));
        _watcher.Renamed += (_, e) => Application.Current.Dispatcher.Invoke(() =>
        {
            UnloadScript(e.OldFullPath);
            LoadScript(e.FullPath);
        });
    }

    private void ReloadOnDispatcher(string path)
    {
        System.Threading.Thread.Sleep(80);
        Application.Current.Dispatcher.Invoke(() => LoadScript(path));
    }

    // ── Logging ────────────────────────────────────────────────────────────

    private static void Log(string msg) =>
        System.Diagnostics.Debug.WriteLine($"[LuaScriptHost] {msg}");

    private void PublishInfo(string subjectId, string payload)
    {
        if (_messenger == null) return;
        _ = _messenger.SendAsync(ESB.Messaging.MessageClass.Information, subjectId, payload);
    }
}
