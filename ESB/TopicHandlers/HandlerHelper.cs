using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    internal static class HandlerHelper
    {
        internal static Task ReplyAsync(IMessenger messenger, MessageContext ctx, string payload)
        {
            string replyTopic = ResolveReplyTopic(ctx);
            return messenger.ReplyAsync(replyTopic, ctx.CorrelationData, payload);
        }

        internal static Task ReplyErrorAsync(IMessenger messenger, MessageContext ctx, string errorJson)
        {
            string replyTopic = ResolveReplyTopic(ctx);
            return messenger.ReplyAsync(replyTopic, ctx.CorrelationData, errorJson);
        }

        private static string ResolveReplyTopic(MessageContext ctx)
        {
            if (ctx.ResponseTopic != null)
                return ctx.ResponseTopic;
            var pt = ctx.ParsedTopic;
            string op = pt.MetaOperation != null ? $"{pt.Operation}.{pt.MetaOperation}" : pt.Operation;
            return $"ESB/{pt.ParticipantType}/{pt.ConnectionId}/{pt.Scope}/res/{op}";
        }

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
        // Property-bag projection helpers (used by GetProperties handlers)
        // -------------------------------------------------------------------------

        internal static bool TryParsePropertyNames(
            JArray requested,
            IEnumerable<string> validNames,
            out HashSet<string> names,
            out List<string> invalid)
        {
            names = new HashSet<string>();
            invalid = null;
            var validSet = new HashSet<string>(validNames);
            foreach (var token in requested)
            {
                var name = token.Value<string>();
                if (validSet.Contains(name)) names.Add(name);
                else (invalid ?? (invalid = new List<string>())).Add(name);
            }
            return invalid == null;
        }

        internal static JObject BuildPropertyObject<T>(
            T source,
            Dictionary<string, Func<T, JToken>> getters,
            HashSet<string> filter)
        {
            var obj = new JObject();
            foreach (var kv in getters)
            {
                if (filter != null && !filter.Contains(kv.Key)) continue;
                try   { obj[kv.Key] = kv.Value(source); }
                catch { obj[kv.Key] = JValue.CreateNull(); }
            }
            return obj;
        }

        // -------------------------------------------------------------------------
        // Shared serialization helpers (used across multiple handlers)
        // -------------------------------------------------------------------------

        internal static JObject BuildStructureJson(GlobalStructureInfo s) =>
            new JObject(
                new JProperty("Id",             s.id),
                new JProperty("Name",           s.name),
                new JProperty("FactionId",      s.factionId),
                new JProperty("FactionGroup",   s.factionGroup),
                new JProperty("ClassNr",        s.classNr),
                new JProperty("CoreType",       s.coreType),
                new JProperty("Type",           s.type),
                new JProperty("PlayfieldName",  s.PlayfieldName),
                new JProperty("Pos",            MessageHelpers.Vec(new Vector3(s.pos.x, s.pos.y, s.pos.z))),
                new JProperty("Rot",            MessageHelpers.Vec(new Vector3(s.rot.x, s.rot.y, s.rot.z))),
                new JProperty("LastVisitedUtc", s.lastVisitedUTC),
                new JProperty("Powered",        s.powered),
                new JProperty("DockedShips",    s.dockedShips != null ? (JToken)new JArray(s.dockedShips) : JValue.CreateNull()));

        internal static JObject ItemStackJson(ItemStack s) =>
            new JObject(
                new JProperty("Id",      s.id),
                new JProperty("Count",   s.count),
                new JProperty("SlotIdx", s.slotIdx),
                new JProperty("Ammo",    s.ammo),
                new JProperty("Decay",   s.decay));

        internal static JArray ItemStacksJson(List<ItemStack> stacks)
        {
            var arr = new JArray();
            if (stacks == null) return arr;
            foreach (var s in stacks)
                arr.Add(ItemStackJson(s));
            return arr;
        }

        internal static ItemStack ParseItemStack(JToken t)
        {
            var stack     = new ItemStack(t["Id"].Value<int>(), t["Count"].Value<int>());
            stack.slotIdx = t["SlotIdx"]?.Value<byte>() ?? 0;
            stack.ammo    = t["Ammo"]?.Value<int>()    ?? 0;
            stack.decay   = t["Decay"]?.Value<int>()   ?? 0;
            return stack;
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
