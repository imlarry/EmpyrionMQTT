namespace ESB.Messaging
{
    public class MessageEnvelope<T> : MessageEnvelope
    {
        public T Body { get; }

        internal MessageEnvelope(MessageContext ctx) : base(ctx)
        {
            Body = PayloadAs<T>();
        }

        internal MessageEnvelope(string rawPayload, string scope, string operation)
            : base(rawPayload, scope, operation)
        {
            Body = PayloadAs<T>();
        }

        internal static new MessageEnvelope<T> From(MessageContext ctx)
            => new MessageEnvelope<T>(ctx);
    }
}
