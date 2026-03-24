using MoonSharp.Interpreter;
using System.Diagnostics;

namespace EDNAClient.Scripting;

/// <summary>
/// A Lua object instance created by calling a factory function in a loaded script.
/// Wraps the table returned by the factory; lets C# call methods on it without
/// knowing the script's internal structure.
///
/// Primary use: C# discovers a new game entity at runtime (e.g. a drone entity
/// reported by the ESB) and needs to create a corresponding Lua instance dynamically.
/// For entities known at script-load time, create instances directly in the Lua script body.
///
/// Lifecycle:
///   Instances are invalidated when their owning script hot-reloads (IsValid → false).
///   Check IsValid before each Call(); recreate via LuaScriptHost.CreateInstance() if false.
///
/// Threading: all Call() invocations must be on the WPF dispatcher thread.
/// </summary>
public sealed class LuaInstance
{
    private LuaEngine? _engine;

    public string   ScriptName { get; }
    public string   InstanceId { get; }

    /// <summary>
    /// The raw Lua table returned by the factory function.
    /// Can be passed back into Lua — e.g. to register the instance with a coordinator:
    ///   _luaHost.Call("base_defense", "register_drone", instance.Self);
    /// </summary>
    public DynValue Self    { get; }

    /// <summary>False after the owning script has been hot-reloaded.</summary>
    public bool     IsValid => _engine != null;

    internal LuaInstance(string scriptName, string instanceId, LuaEngine engine, DynValue self)
    {
        ScriptName = scriptName;
        InstanceId = instanceId;
        Self       = self;
        _engine    = engine;
    }

    /// <summary>
    /// Call a named method on the Lua table.
    /// Returns null if the instance is invalid or the method is not defined.
    /// </summary>
    public DynValue? Call(string method, params object[] args)
    {
        if (_engine == null) return null;

        var fn = Self.Table?.Get(method) ?? DynValue.Nil;
        if (fn.IsNil()) return null;

        try   { return _engine.CallFunction(fn, args); }
        catch (ScriptRuntimeException ex)
        {
            Debug.WriteLine(
                $"[LuaInstance:{ScriptName}/{InstanceId}] '{method}' error: {ex.DecoratedMessage}");
            return null;
        }
    }

    internal void Invalidate() => _engine = null;
}
