namespace EDNAClient.Core
{
    public interface IGameContextReceiver
    {
        // Called when the player enters an in-game session (has a save game path).
        // Skills set up their save-game-scoped state here (directories, nav tree, etc.).
        void OnGameEnter(string saveGamePath);

        // Called when the game returns to lobby (or is about to stop). Skills close
        // their UI (documents, nav section) but keep MQTT subscriptions alive.
        // Default is a no-op for skills that have no per-session UI.
        void OnGameExit() { }
    }
}
