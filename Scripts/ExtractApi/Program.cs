using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;

// Default DLL directory matches the HintPath in ESB/ESB.csproj
const string DefaultDllDir =
    @"C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Client\Empyrion_Data\Managed";

string dllDir = DefaultDllDir;
string outFile = Path.Combine(
    AppContext.BaseDirectory,
    "..", "..", "..", "..", "..",  // Scripts/ExtractApi -> repo root
    "Docs", "ApiTableOfContents.md");

// Parse args
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--dll-dir" && i + 1 < args.Length)
        dllDir = args[++i];
    else if (args[i] == "--out" && i + 1 < args.Length)
        outFile = args[++i];
}

outFile = Path.GetFullPath(outFile);

string[] targetDlls = ["ModApi.dll", "Mif.dll"];

foreach (var dll in targetDlls)
{
    if (!File.Exists(Path.Combine(dllDir, dll)))
    {
        Console.Error.WriteLine($"ERROR: {dll} not found in {dllDir}");
        Console.Error.WriteLine("Use --dll-dir <path> to specify the Managed directory.");
        return 1;
    }
}

// Load all DLLs from the directory as the resolver scope
var allDlls = Directory.GetFiles(dllDir, "*.dll");
var resolver = new PathAssemblyResolver(allDlls);
using var mlc = new MetadataLoadContext(resolver);

var sb = new StringBuilder();
sb.AppendLine("# Empyrion Modding API - Table of Contents");
sb.AppendLine();
sb.AppendLine($"_Generated from ModApi.dll and Mif.dll via DLL reflection._");
sb.AppendLine($"_Source directory: `{dllDir}`_");
sb.AppendLine($"_Run `dotnet run --project Scripts/ExtractApi` to regenerate._");
sb.AppendLine();

foreach (var dllName in targetDlls)
{
    var dllPath = Path.Combine(dllDir, dllName);
    var asm = mlc.LoadFromAssemblyPath(dllPath);

    Type[] types;
    try { types = asm.GetTypes(); }
    catch (ReflectionTypeLoadException ex)
    {
        types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
    }

    // Only public, non-compiler-generated types
    types = types
        .Where(t => t.IsPublic && !t.Name.StartsWith('<'))
        .OrderBy(t => t.Namespace)
        .ThenBy(t => t.Name)
        .ToArray();

    var byKind = new Dictionary<string, List<Type>>
    {
        ["Interfaces"] = [],
        ["Classes"]    = [],
        ["Structs"]    = [],
        ["Enums"]      = [],
        ["Delegates"]  = [],
    };

    foreach (var t in types)
    {
        if (t.IsInterface)
            byKind["Interfaces"].Add(t);
        else if (t.IsEnum)
            byKind["Enums"].Add(t);
        else if (IsDelegate(t))
            byKind["Delegates"].Add(t);
        else if (t.IsValueType)
            byKind["Structs"].Add(t);
        else
            byKind["Classes"].Add(t);
    }

    sb.AppendLine("---");
    sb.AppendLine();
    sb.AppendLine($"## {dllName}");
    sb.AppendLine();

    foreach (var (kind, list) in byKind)
    {
        if (list.Count == 0) continue;
        sb.AppendLine($"### {dllName} - {kind}");
        sb.AppendLine();

        foreach (var t in list)
        {
            RenderType(sb, t, kind);
        }
    }
}

Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
Console.WriteLine($"Written: {outFile}");
return 0;


// ------------------------------------------------------------------ helpers

static bool IsDelegate(Type t) =>
    t.IsClass && t.BaseType is { } bt &&
    (bt.FullName == "System.MulticastDelegate" || bt.FullName == "System.Delegate");

static string FriendlyName(Type? t)
{
    if (t is null) return "?";
    if (t.IsGenericType)
    {
        var name = t.Name.Split('`')[0];
        var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyName));
        return $"{name}<{args}>";
    }
    return t.Name switch
    {
        "Void"    => "void",
        "Boolean" => "bool",
        "Byte"    => "byte",
        "SByte"   => "sbyte",
        "Int16"   => "short",
        "UInt16"  => "ushort",
        "Int32"   => "int",
        "UInt32"  => "uint",
        "Int64"   => "long",
        "UInt64"  => "ulong",
        "Single"  => "float",
        "Double"  => "double",
        "String"  => "string",
        "Object"  => "object",
        _         => t.Name,
    };
}

static string AccessorLabel(PropertyInfo p)
{
    var g = p.GetMethod?.IsPublic == true;
    var s = p.SetMethod?.IsPublic == true;
    return (g, s) switch
    {
        (true,  true)  => "get; set;",
        (true,  false) => "get;",
        (false, true)  => "set;",
        _              => "-",
    };
}

static string ParamList(MethodBase m) =>
    string.Join(", ", m.GetParameters().Select(p => $"{FriendlyName(p.ParameterType)} {p.Name}"));

static void RenderType(StringBuilder sb, Type t, string kind)
{
    var ns = string.IsNullOrEmpty(t.Namespace) ? "" : $"  _({t.Namespace})_";
    sb.AppendLine($"#### {t.Name}{ns}");
    sb.AppendLine();

    // Inheritance / implements
    var bases = new List<string>();
    if (t.BaseType is { } bt && bt.FullName != "System.Object" && bt.FullName != "System.ValueType" && bt.FullName != "System.Enum" && bt.FullName != "System.MulticastDelegate")
        bases.Add($"extends {FriendlyName(bt)}");
    var ifaces = t.GetInterfaces().Select(i => FriendlyName(i)).ToArray();
    if (ifaces.Length > 0)
        bases.Add($"implements {string.Join(", ", ifaces)}");
    if (bases.Count > 0)
        sb.AppendLine($"_{string.Join(" | ", bases)}_");
    sb.AppendLine();

    // Enum members
    if (t.IsEnum)
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Static)
                      .OrderBy(f => f.Name)
                      .ToArray();
        if (fields.Length > 0)
        {
            sb.AppendLine("| Value | Name |");
            sb.AppendLine("|-------|------|");
            foreach (var f in fields)
            {
                var raw = f.GetRawConstantValue();
                sb.AppendLine($"| {raw} | {f.Name} |");
            }
            sb.AppendLine();
        }
        return;
    }

    // Delegate signature
    if (IsDelegate(t))
    {
        var invoke = t.GetMethod("Invoke");
        if (invoke is not null)
            sb.AppendLine($"`{FriendlyName(invoke.ReturnType)} ({ParamList(invoke)})`");
        sb.AppendLine();
        return;
    }

    // Properties
    var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                 .OrderBy(p => p.Name)
                 .ToArray();
    if (props.Length > 0)
    {
        sb.AppendLine("**Properties**");
        sb.AppendLine();
        sb.AppendLine("| Name | Type | Access |");
        sb.AppendLine("|------|------|--------|");
        foreach (var p in props)
            sb.AppendLine($"| {p.Name} | {FriendlyName(p.PropertyType)} | {AccessorLabel(p)} |");
        sb.AppendLine();
    }

    // Methods (exclude property accessors and object base methods for interfaces)
    var propAccessors = new HashSet<MethodInfo>(
        props.SelectMany(p => new[] { p.GetMethod, p.SetMethod }.Where(m => m != null))!);

    var events = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
    var eventAccessors = new HashSet<MethodInfo>(
        events.SelectMany(e => new[] { e.AddMethod, e.RemoveMethod }.Where(m => m != null))!);

    var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                   .Where(m => !m.IsSpecialName && !propAccessors.Contains(m) && !eventAccessors.Contains(m))
                   .OrderBy(m => m.Name)
                   .ThenBy(m => m.GetParameters().Length)
                   .ToArray();

    if (methods.Length > 0)
    {
        sb.AppendLine("**Methods**");
        sb.AppendLine();
        sb.AppendLine("| Signature |");
        sb.AppendLine("|-----------|");
        foreach (var m in methods)
            sb.AppendLine($"| `{FriendlyName(m.ReturnType)} {m.Name}({ParamList(m)})` |");
        sb.AppendLine();
    }

    // Events
    if (events.Length > 0)
    {
        sb.AppendLine("**Events**");
        sb.AppendLine();
        sb.AppendLine("| Name | Delegate |");
        sb.AppendLine("|------|----------|");
        foreach (var e in events.OrderBy(e => e.Name))
            sb.AppendLine($"| {e.Name} | {FriendlyName(e.EventHandlerType)} |");
        sb.AppendLine();
    }

    // Public fields (structs/classes only)
    if (!t.IsEnum && !IsDelegate(t))
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                      .Where(f => !f.IsSpecialName)
                      .OrderBy(f => f.Name)
                      .ToArray();
        if (fields.Length > 0)
        {
            sb.AppendLine("**Fields**");
            sb.AppendLine();
            sb.AppendLine("| Name | Type |");
            sb.AppendLine("|------|------|");
            foreach (var f in fields)
                sb.AppendLine($"| {f.Name} | {FriendlyName(f.FieldType)} |");
            sb.AppendLine();
        }
    }
}
