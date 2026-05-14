using ESB.Messaging;

namespace EDNAClient.Core
{
    public class EdnaContext : BaseContextData
    {
        // Set on GameEnter: "Client" for SinglePlayer, "DedicatedServer" for MP.
        public string? AuthoritativeSource { get; set; }

        public IMessageBus Bus { get; set; }

        // Routing context IDs learned from inbound events; used as the target
        // rcId for in-game requests (Player/Structure/etc.). Null when not in a game.
        public string? GameRcId { get; set; }
        public string? CurrentPlayfieldRcId { get; set; }
    }
}
