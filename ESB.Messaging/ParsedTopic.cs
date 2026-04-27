namespace ESB.Messaging
{
    // ParsedTopic represents a parsed ESB/ schema topic.
    // Standard form (6 segments):    ESB/{type}/{connId}/{scope}/{dir}/{op}
    // Device sub-scope (8 segments): ESB/{type}/{connId}/Structure/Device/{deviceName}/{dir}/{op}
    public class ParsedTopic
    {
        public string ParticipantType { get; set; }  // Client | Pfs | Ds | Agent | Messenger
        public string ConnectionId    { get; set; }  // 4-char base-36 (lowercase)
        public string Scope           { get; set; }  // App | Playfield | Player | Structure
        public string DeviceName      { get; set; }  // non-null only for device sub-scope topics
        public string Dir             { get; set; }  // Req | Res | Evt | Err | Log
        public string Operation       { get; set; }  // get/Prop | set/Prop | call/Method | eventName | cid | info
        public string DispatchKey     { get; set; }  // "{scope}/{dir}/{op}" or "Structure/Device/{name}/{dir}/{op}"
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
