using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ESB.Messaging
{
    public class MessageEnvelope
    {
        public string CorrelationId       { get; }
        public string SenderType          { get; }
        public string SenderConnectionId  { get; }
        public string Scope               { get; }
        public string Operation           { get; }
        public string MsgType             { get; }
        public string RawPayload          { get; }

        private JObject _payloadJson;
        private bool _payloadJsonParsed;

        // Lazy-parsed JSON object; null if payload is empty or not valid JSON.
        public JObject PayloadJson
        {
            get
            {
                if (!_payloadJsonParsed)
                {
                    _payloadJsonParsed = true;
                    if (!string.IsNullOrEmpty(RawPayload))
                    {
                        try { _payloadJson = JObject.Parse(RawPayload); }
                        catch { _payloadJson = null; }
                    }
                }
                return _payloadJson;
            }
        }

        public TBody PayloadAs<TBody>()
        {
            if (string.IsNullOrEmpty(RawPayload)) return default(TBody);
            return JsonConvert.DeserializeObject<TBody>(RawPayload);
        }

        // From a fully routed inbound message.
        internal MessageEnvelope(MessageContext ctx)
        {
            var pt = ctx.ParsedTopic;
            CorrelationId      = ctx.CorrelationData != null && ctx.CorrelationData.Length > 0
                                     ? Encoding.UTF8.GetString(ctx.CorrelationData)
                                     : string.Empty;
            SenderType         = pt != null ? pt.ParticipantType : string.Empty;
            SenderConnectionId = pt != null ? pt.ConnectionId    : string.Empty;
            Scope              = pt != null ? pt.Scope           : string.Empty;
            Operation          = pt != null ? pt.Operation       : string.Empty;
            MsgType            = pt != null ? pt.MsgType         : string.Empty;
            RawPayload         = ctx.Payload ?? string.Empty;
        }

        // From a raw response string (RequestAsync path).
        // Sender and CorrelationId are not available in this path.
        internal MessageEnvelope(string rawPayload, string scope, string operation)
        {
            CorrelationId      = string.Empty;
            SenderType         = string.Empty;
            SenderConnectionId = string.Empty;
            Scope              = scope     ?? string.Empty;
            Operation          = operation ?? string.Empty;
            MsgType            = "res";
            RawPayload         = rawPayload ?? string.Empty;
        }

        internal static MessageEnvelope From(MessageContext ctx)
            => new MessageEnvelope(ctx);
    }
}
