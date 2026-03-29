using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace EDNAClient.Scripting.Api
{
    /// <summary>
    /// Replaces MoonSharp's built-in json module (CoreModules.Json) as the 'json' global.
    /// MoonSharp 2.0.0's json.parse fails on negative numbers ("Unexpected token: '-'"),
    /// which breaks every EntityLoaded payload that contains Position coordinates.
    ///
    /// This implementation delegates to Newtonsoft.Json and converts the result to
    /// MoonSharp DynValue/Table objects, giving scripts a fully correct JSON parser.
    ///
    /// Lua API (drop-in replacement for MoonSharp's json module):
    ///   json.parse(str)       -> table, number, string, bool, or json.null
    ///   json.serialize(val)   -> JSON string
    ///   json.isnull(val)      -> bool
    ///   json.null             -> the null sentinel value
    /// </summary>
    [MoonSharpUserData]
    public sealed class LuaJsonApi
    {
        // MoonSharp represents JSON null as a special UserData sentinel.
        // We use a simple marker object that scripts can test with json.isnull().
        private static readonly object NullSentinel = new();

        public DynValue @null => UserData.Create(NullSentinel);

        public bool isnull(DynValue value)
        {
            if (value.IsNil()) return true;
            if (value.Type == DataType.UserData)
                return ReferenceEquals(value.UserData.Object, NullSentinel);
            return false;
        }

        /// <summary>
        /// Parse a JSON string and return a Lua-compatible value.
        /// Throws ScriptRuntimeException on malformed input (pcall-safe).
        /// </summary>
        public DynValue parse(string json)
        {
            JToken token;
            try { token = JToken.Parse(json); }
            catch (Exception ex)
            {
                throw new ScriptRuntimeException($"json.parse: {ex.Message}");
            }
            return JTokenToDynValue(token);
        }

        /// <summary>
        /// Serialize a Lua value to a JSON string.
        /// Tables become objects or arrays; primitives map to their JSON equivalents.
        /// </summary>
        public string serialize(DynValue value)
        {
            var token = DynValueToJToken(value);
            return token.ToString(Newtonsoft.Json.Formatting.None);
        }

        // -- Conversion helpers -------------------------------------------------------

        private DynValue JTokenToDynValue(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                {
                    var table = new Table(null);
                    foreach (var prop in ((JObject)token).Properties())
                        table[prop.Name] = JTokenToDynValue(prop.Value);
                    return DynValue.NewTable(table);
                }
                case JTokenType.Array:
                {
                    var table = new Table(null);
                    int i = 1;
                    foreach (var item in (JArray)token)
                        table[i++] = JTokenToDynValue(item);
                    return DynValue.NewTable(table);
                }
                case JTokenType.String:
                    return DynValue.NewString(token.Value<string>()!);
                case JTokenType.Integer:
                    return DynValue.NewNumber(token.Value<double>());
                case JTokenType.Float:
                    return DynValue.NewNumber(token.Value<double>());
                case JTokenType.Boolean:
                    return DynValue.NewBoolean(token.Value<bool>());
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return UserData.Create(NullSentinel);
                default:
                    return DynValue.NewString(token.ToString());
            }
        }

        private static JToken DynValueToJToken(DynValue value)
        {
            switch (value.Type)
            {
                case DataType.Nil:
                    return JValue.CreateNull();
                case DataType.Boolean:
                    return new JValue(value.Boolean);
                case DataType.Number:
                    return new JValue(value.Number);
                case DataType.String:
                    return new JValue(value.String);
                case DataType.Table:
                {
                    // Detect array: all keys are consecutive integers starting at 1
                    var tbl    = value.Table;
                    var keys   = tbl.Keys.ToList();
                    bool isArr = keys.Count > 0
                              && keys.All(k => k.Type == DataType.Number)
                              && keys.Select(k => (int)k.Number)
                                   .OrderBy(x => x)
                                   .SequenceEqual(Enumerable.Range(1, keys.Count));

                    if (isArr)
                    {
                        var arr = new JArray();
                        for (int i = 1; i <= keys.Count; i++)
                            arr.Add(DynValueToJToken(tbl.Get(i)));
                        return arr;
                    }
                    else
                    {
                        var obj = new JObject();
                        foreach (var k in keys)
                            obj[k.String] = DynValueToJToken(tbl.Get(k));
                        return obj;
                    }
                }
                case DataType.UserData when ReferenceEquals(value.UserData.Object, NullSentinel):
                    return JValue.CreateNull();
                default:
                    return JValue.CreateNull();
            }
        }
    }
}
