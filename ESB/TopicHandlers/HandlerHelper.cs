using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    internal static class HandlerHelper
    {
        internal static Task ReplyAsync(IMessenger messenger, MessageContext ctx, string payload)
            => messenger.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, payload);

        internal static async Task ReplyErrorAsync(IMessenger messenger, MessageContext ctx, string errorJson)
        {
            await messenger.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, errorJson);

            var pt = ctx.ParsedTopic;
            string op = pt.MetaOperation != null ? $"{pt.Operation}.{pt.MetaOperation}" : pt.Operation;
            string errTopic = $"EMP/{pt.ParticipantType}/{pt.ConnectionId}/Err/{pt.Scope}/{op}";
            await messenger.SendAsync(errTopic, errorJson);
        }

        internal static byte[] ToBytes(string s) => Encoding.UTF8.GetBytes(s);

        // -------------------------------------------------------------------------
        // Operation metadata types
        // -------------------------------------------------------------------------

        internal class FieldDef
        {
            internal string Name     { get; }
            internal string Type     { get; }
            internal bool   Required { get; }
            internal string Note     { get; }
            internal FieldDef(string name, string type, bool required = false, string note = null)
            { Name = name; Type = type; Required = required; Note = note; }
        }

        internal class OpDef
        {
            internal string     Summary { get; }
            internal FieldDef[] Input   { get; }
            internal FieldDef[] Output  { get; }
            internal string     Notes   { get; }
            internal OpDef(string summary, FieldDef[] input = null, FieldDef[] output = null, string notes = null)
            {
                Summary = summary;
                Input   = input  ?? new FieldDef[0];
                Output  = output ?? new FieldDef[0];
                Notes   = notes;
            }
        }

        // -------------------------------------------------------------------------
        // Meta-operation dispatch
        // -------------------------------------------------------------------------

        internal static Task ReplyMetaAsync(
            IMessenger messenger, MessageContext ctx, string opName, OpDef def)
        {
            var meta = ctx.ParsedTopic.MetaOperation;
            if (meta == "Describe")
                return ReplyAsync(messenger, ctx, DescribeJson(opName, ctx.ParsedTopic.Scope, def));
            if (meta == "Example" || meta == "Validate")
                return ReplyErrorAsync(messenger, ctx,
                    MessageHelpers.ErrorJson($"Meta-operation '{meta}' not yet implemented"));
            return ReplyErrorAsync(messenger, ctx,
                MessageHelpers.ErrorJson($"Unknown meta-operation '{meta}'. Known: Describe, Example, Validate"));
        }

        internal static string ScopeManifestJson(string scope, Dictionary<string, OpDef> ops)
        {
            var arr = new JArray();
            foreach (var kv in ops)
                arr.Add(new JObject(
                    new JProperty("Operation", kv.Key),
                    new JProperty("Summary",   kv.Value.Summary)));
            return new JObject(
                new JProperty("Scope",      scope),
                new JProperty("Operations", arr)).ToString(Formatting.None);
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private static string DescribeJson(string opName, string scope, OpDef def)
        {
            var obj = new JObject(
                new JProperty("Operation", opName),
                new JProperty("Scope",     scope),
                new JProperty("Summary",   def.Summary));
            obj.Add("Input",  FieldDefsJson(def.Input));
            obj.Add("Output", FieldDefsJson(def.Output));
            if (def.Notes != null) obj.Add("Notes", def.Notes);
            obj.Add("Suffixes", new JArray("Describe", "Example", "Validate"));
            return obj.ToString(Formatting.None);
        }

        private static JArray FieldDefsJson(FieldDef[] fields)
        {
            var arr = new JArray();
            foreach (var f in fields)
            {
                var o = new JObject(
                    new JProperty("Name",     f.Name),
                    new JProperty("Type",     f.Type),
                    new JProperty("Required", f.Required));
                if (f.Note != null) o.Add("Note", f.Note);
                arr.Add(o);
            }
            return arr;
        }
    }
}
