using ESB.Messaging;

namespace EDNAClient.Core
{
    public class EdnaContext : BaseContextData
    {
        // Set on GameEnter: "Client" for SinglePlayer, "DedicatedServer" for MP.
        public string? AuthoritativeSource { get; set; }

        // Set in EdnaService.StartAsync before any consumer touches it.
        public IMessageBus Bus { get; set; } = null!;

        // ContextRcId ... current audience for evt subscriptions and event publishes.
        // Always set after EdnaService startup: Lobby pre-game, real Game while in a game,
        // and (optionally) a Game rcId loaded for offline review of saved state.
        public string? ContextRcId { get; set; }

        // LobbyRcId ... pre-game audience derived from MachineId; shared with the bound Client
        // (which lives on the same machine and so derives the same value).
        public string? LobbyRcId { get; set; }

        // GameRcId ... real game audience set on GameEnter (or set manually in offline mode
        // for reviewing saved data). May be set without being the current ContextRcId.
        public string? GameRcId { get; set; }
    }
}
