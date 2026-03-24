using EDNAClient.Scripting.Api;
using MoonSharp.Interpreter;

namespace EDNAClient.Scripting;

/// <summary>
/// Wraps a single MoonSharp Script instance with a sandboxed environment.
/// One engine per script file — isolation prevents scripts from clobbering each other.
///
/// Sandbox allows: Basic, Table, String, Math, ErrorHandling, Coroutine, Metatables.
/// Explicitly excluded: IO, OS, LoadMethods, Json — no file access or dynamic code loading.
///
/// Note: CoreModules.Json is excluded because MoonSharp 2.0.0's json.parse fails on
/// negative numbers. LuaJsonApi (backed by Newtonsoft.Json) is injected as 'json' instead.
/// </summary>
public sealed class LuaEngine
{
    private const CoreModules Sandbox =
        CoreModules.Basic          |
        CoreModules.Table          |
        CoreModules.TableIterators |
        CoreModules.String         |
        CoreModules.Math           |
        CoreModules.ErrorHandling  |
        CoreModules.Coroutine      |   // required for coroutine-based behavior trees
        CoreModules.Metatables;        // CoreModules.Json excluded — see LuaJsonApi

    public string Name   { get; }
    public Script Script { get; }

    static LuaEngine()
    {
        UserData.RegisterType<LuaJsonApi>();
    }

    public LuaEngine(string name)
    {
        Name   = name;
        Script = new Script(Sandbox);
        Script.Globals["json"] = UserData.Create(new LuaJsonApi());
    }

    /// <summary>Execute a Lua source string inside this engine.</summary>
    public DynValue Execute(string source, string? chunkName = null)
        => Script.DoString(source, codeFriendlyName: chunkName ?? Name);

    /// <summary>Load and execute a .lua file.</summary>
    public DynValue ExecuteFile(string path)
        => Script.DoFile(path);

    /// <summary>
    /// Invoke a named global function. Returns null if the function is not defined.
    /// Must be called on the thread that owns this engine (WPF dispatcher).
    /// </summary>
    public DynValue? CallFunction(string name, params object[] args)
    {
        var fn = Script.Globals.Get(name);
        return fn.IsNil() ? null : Script.Call(fn, args);
    }

    /// <summary>Invoke a DynValue function reference (e.g. stored from a Lua callback registration).</summary>
    public DynValue CallFunction(DynValue fn, params object[] args)
        => Script.Call(fn, args);

    public void SetGlobal(string name, object value) => Script.Globals[name] = value;
    public DynValue GetGlobal(string name)           => Script.Globals.Get(name);
}
