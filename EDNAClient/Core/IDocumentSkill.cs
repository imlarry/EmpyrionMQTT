using System.Collections.Generic;

namespace EDNAClient.Core
{
    // Opt-in interface for skills that open document tabs.
    // The workspace queries these on hide to persist open document IDs,
    // and EdnaService calls RestoreDocuments after each OnGameEnter.
    public interface IDocumentSkill
    {
        string Id { get; }

        // Returns the contentId of every currently-open document tab (docked or floating).
        IReadOnlyList<string> GetOpenDocumentIds();

        // Re-opens documents from a previous session. Called after OnGameEnter so that
        // skill-scoped state (e.g. _mapsDir) is already initialized.
        void RestoreDocuments(IReadOnlyList<string> contentIds);
    }
}
