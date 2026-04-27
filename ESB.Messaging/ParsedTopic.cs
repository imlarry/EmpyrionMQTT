namespace ESB.Messaging
{
    // ParsedTopic represents a parsed EMP/ schema topic.
    // Fixed 6-segment form: EMP/{participantType}/{connectionId}/{dir}/{scope}/{operation}
    public class ParsedTopic
    {
        public string ParticipantType { get; set; }  // Client | Pfs | Ds | Agent | Messenger
        public string ConnectionId    { get; set; }  // 4-char base-36 (lowercase)
        public string Dir             { get; set; }  // Req | Res | Evt | Err | Log
        public string Scope           { get; set; }  // App | Player | Structure | Device | ...
        public string Operation       { get; set; }  // PascalCase operation name (base, no dot-suffix)
        public string MetaOperation   { get; set; }  // non-null when dot-suffix present (e.g. "Describe")
        public string DispatchKey     { get; set; }  // "{scope}/{operation}"
    }

    // MessageContext carries everything a handler needs from an incoming message.
    public class MessageContext
    {
        public ParsedTopic ParsedTopic     { get; set; }
        public string      Payload         { get; set; }
        public string      ResponseTopic   { get; set; }
        public byte[]      CorrelationData { get; set; }
    }
}
