namespace ESB.Messaging
{
    // BaseContextData .. the basis for context data structure which minimally contains an instance of Messenger
    public abstract class BaseContextData : IBaseContextData
    {
        public Messenger Messenger { get; set; } = new Messenger();
    }

}
