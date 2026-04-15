namespace ESB.Messaging
{
    // ParsedTopic
    internal class ParsedTopic
    {
        public string SourceId { get; set; }
        public string MessageClass { get; set; }
        public string SubjectId { get; set; }
        public string ClientId { get; set; }
        public string PubSeqId { get; set; }
    }
}
