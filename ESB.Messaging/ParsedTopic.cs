using System.Collections.Generic;

namespace ESB.Messaging
{
    // ParsedTopic represents a parsed ESB/ schema topic.
    // Fixed 6-segment form: ESB/{participantType}/{connectionId}/{scope}/{msgType}/{operation}
    public class ParsedTopic
    {
        public string ParticipantType { get; set; }  // as passed to ConnectAsync
        public string ConnectionId    { get; set; }  // 4-char base-36 lowercase
        public string Scope           { get; set; }  // App | Player | Structure | Device | Registry | ...
        public string MsgType         { get; set; }  // evt | req | res | log (lowercase)
        public string Operation       { get; set; }  // operation name (base, no dot-suffix)
        public string MetaOperation   { get; set; }  // non-null when dot-suffix present (e.g. "Describe")
        public string DispatchKey     { get; set; }  // "{scope}/{msgType}/{operation}"
    }

    // MessageContext carries everything a handler needs from an incoming message.
    public class MessageContext
    {
        public ParsedTopic                          ParsedTopic     { get; set; }
        public string                               Payload         { get; set; }
        public string                               ResponseTopic   { get; set; }
        public byte[]                               CorrelationData { get; set; }
        public List<KeyValuePair<string, string>>   UserProperties  { get; set; }
    }
}
