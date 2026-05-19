using ESB.Messaging;

namespace EDNAClient.Core
{
    public class EdnaContext : BaseContextData
    {
        // Set on GameEnter: "Client" for SinglePlayer, "DedicatedServer" for MP.
        public string? AuthoritativeSource { get; set; }

        // Set in EdnaService.StartAsync before any consumer touches it.
        public IMessageBus Bus { get; set; } = null!;

        // LobbyRcId ... pre-game audience derived from MachineId; shared with the bound Client
        // (which lives on the same machine and so derives the same value). The bus's
        // ContextRcId is the live audience; LobbyRcId is the value it returns to between games.
        public string? LobbyRcId { get; set; }

        // GameRcId ... real game audience set on GameEnter (or set manually in offline mode
        // for reviewing saved data). May be set without being the current Bus.ContextRcId.
        public string? GameRcId { get; set; }
    }
}
