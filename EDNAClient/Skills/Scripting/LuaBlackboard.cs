using EDNAClient.Core;
using MoonSharp.Interpreter;

namespace EDNAClient.Skills.Scripting;

/// <summary>
/// Shared in-process key-value store with reactive watchers.
/// One instance lives in LuaScriptHost and is shared across all loaded engines.
///
/// Ownership convention: each key is written by exactly one script (the one that
/// holds the MQTT subscription for the authoritative data source). All other scripts
/// treat it as read-only and react via bb:watch().
///
/// Threading: all access must be on the WPF dispatcher thread (same thread as Lua).
/// </summary>
internal sealed class LuaBlackboard
{
    private readonly Dictionary<string, DynValue> _data = new();

    // key → list of (owning engine, lua function to call)
    private readonly Dictionary<string, List<(LuaEngine Engine, DynValue Fn)>> _watchers = new();

    // Optional error sink — set by LuaScriptHost to route watcher errors to MQTT.
    internal Action<string, string, string>? OnWatcherError;  // (key, scriptName, decoratedMessage)

    public DynValue Get(string key)
        => _data.TryGetValue(key, out var v) ? v : DynValue.Nil;

    /// <summary>Write a value and synchronously fire all watchers for this key.</summary>
    public void Set(string key, DynValue value)
    {
        _data[key] = value;
        FireWatchers(key, value);
    }

    public bool Has(string key) => _data.ContainsKey(key);

    public void Clear(string key) => _data.Remove(key);

    /// <summary>Clear all data. Called on game session end.</summary>
    public void Reset() => _data.Clear();

    internal void AddWatcher(string key, LuaEngine engine, DynValue fn)
    {
        if (!_watchers.TryGetValue(key, out var list))
            _watchers[key] = list = new();
        list.Add((engine, fn));
    }

    /// <summary>Remove all watchers registered by a specific engine. Called on hot-reload.</summary>
    internal void ClearWatchers(LuaEngine engine)
    {
        foreach (var list in _watchers.Values)
            list.RemoveAll(w => ReferenceEquals(w.Engine, engine));
    }

    private void FireWatchers(string key, DynValue value)
    {
        if (!_watchers.TryGetValue(key, out var list) || list.Count == 0) return;

        // Snapshot before iterating — a watcher callback may itself call Set(),
        // which would re-enter FireWatchers for a different key (safe), or for
        // this same key (cycle — user's responsibility to avoid).
        var snapshot = list.ToArray();
        foreach (var (engine, fn) in snapshot)
        {
            try   { engine.CallFunction(fn, value); }
            catch (ScriptRuntimeException ex)
            {
                EdnaLogger.Error($"[Blackboard] Watcher error for key '{key}': {ex.DecoratedMessage}");
                OnWatcherError?.Invoke(key, engine.Name, ex.DecoratedMessage);
            }
        }
    }
}

/// <summary>
/// Per-engine Lua-facing wrapper for LuaBlackboard. Exposed as the 'bb' global.
/// Tagging each watcher with its engine allows the host to purge stale watchers on hot-reload.
///
/// Lua API:
///   bb:get(key)           → value or nil
///   bb:set(key, value)    → write; fires watchers synchronously
///   bb:has(key)           → bool
///   bb:clear(key)         → remove key
///   bb:watch(key, fn)     → fn(value) called whenever key is set
///
/// Ownership convention:
///   Each key should be written by exactly one script. Watcher callbacks
///   should treat the received value as read-only — writing to the same key
///   inside a watcher creates a recursive trigger cycle.
/// </summary>
[MoonSharpUserData]
public sealed class LuaBbApi
{
    private readonly LuaBlackboard _bb;
    private readonly LuaEngine     _engine;

    internal LuaBbApi(LuaBlackboard bb, LuaEngine engine)
    {
        _bb     = bb;
        _engine = engine;
    }

    public DynValue get(string key)           => _bb.Get(key);
    public void     set(string key, DynValue value) => _bb.Set(key, value);
    public bool     has(string key)           => _bb.Has(key);
    public void     clear(string key)         => _bb.Clear(key);

    /// <summary>
    /// Register a watcher. fn(value) is called synchronously whenever bb:set(key, ...) fires.
    /// Watchers are automatically removed when the owning script hot-reloads.
    /// </summary>
    public void watch(string key, DynValue fn) => _bb.AddWatcher(key, _engine, fn);
}
