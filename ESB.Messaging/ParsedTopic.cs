namespace ESB.Messaging
{
    internal class ParsedTopic
    {
        public string SourceId { get; set; }
        public string MessageClass { get; set; }
        public string SubjectId { get; set; }
        public string ClientId { get; set; }
        public string PubSeqId { get; set; }
    }

    // EmpMessageContext carries everything an emp/ handler needs from an incoming request.
    public class EmpMessageContext
    {
        public EmpParsedTopic ParsedTopic     { get; set; }
        public string         Payload         { get; set; }
        public string         ResponseTopic   { get; set; }
        public byte[]         CorrelationData { get; set; }
    }

    // EmpParsedTopic represents a parsed EMP/ schema topic.
    // Standard form (6 segments):    EMP/{type}/{connId}/{scope}/{dir}/{op}
    // Device sub-scope (8 segments): EMP/{type}/{connId}/Structure/Device/{deviceName}/{dir}/{op}
    public class EmpParsedTopic
    {
        public string ParticipantType { get; set; }  // Client | Pfs | Ds | Agent
        public string ConnectionId    { get; set; }  // 4-char base-36 (lowercase)
        public string Scope           { get; set; }  // App | Playfield | Player | Structure
        public string DeviceName      { get; set; }  // non-null only for device sub-scope topics
        public string Dir             { get; set; }  // Req | Res | Evt | Err | Log
        public string Operation       { get; set; }  // get/Prop | set/Prop | call/Method | eventName | cid | info
        public string DispatchKey     { get; set; }  // handler lookup key: "{scope}/{dir}/{op}" or "Structure/Device/{deviceName}/{dir}/{op}"
    }
}
