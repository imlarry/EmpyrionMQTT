namespace ESB.Messaging
{
    public interface IParsedTopic
    {
        string SourceId { get; set; }
        string MessageClass { get; set; }
        string SubjectId { get; set; }
        string ClientId { get; set; }
        string PubSeqId { get; set; }
    }
}
