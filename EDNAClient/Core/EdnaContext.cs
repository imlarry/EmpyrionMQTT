using ESB.Messaging;

namespace EDNAClient.Core
{
    public class EdnaContext : BaseContextData
    {
        // Set on GameEnter: "Client" for SinglePlayer, "DedicatedServer" for MP.
        public string? AuthoritativeSource { get; set; }

        public IMessageBus Bus { get; set; }
    }
}
