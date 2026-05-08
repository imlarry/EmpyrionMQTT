using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AvalonDock.Layout;
using EDNAClient.Workspace;

namespace EDNAClient.Helpers
{
    // Manages the lifecycle of AvalonDock documents opened by a skill:
    // open/reactivate, title updates, close tracking, and bulk close on Stop().
    internal sealed class DocumentTracker
    {
        private readonly Dictionary<string, LayoutDocument> _docs = new();

        // Returns true and activates the tab if the document is already open.
        public bool TryActivate(string contentId)
        {
            if (!_docs.TryGetValue(contentId, out var existing)) return false;
            existing.IsActive = true;
            return true;
        }

        // Opens or reactivates a document. Hooks the Closed event for cleanup.
        // onClosing fires before the tab closes (can cancel). onClosed fires after.
        public void Open(ISkillWorkspace workspace, string title,
            string contentId, UIElement content,
            EventHandler<System.ComponentModel.CancelEventArgs>? onClosing = null,
            Action? onClosed = null)
        {
            var doc = workspace.OpenDocument(title, contentId, content);
            _docs[contentId] = doc;
            if (onClosing != null) doc.Closing += onClosing;
            doc.Closed += (_, _) => { _docs.Remove(contentId); onClosed?.Invoke(); };
        }

        // Updates the tab title for an already-open document (e.g. dirty flag).
        public void UpdateTitle(string contentId, string title)
        {
            if (_docs.TryGetValue(contentId, out var doc))
                doc.Title = title;
        }

        // Removes a specific document from the workspace and the tracker.
        public void Remove(ISkillWorkspace workspace, string contentId)
        {
            workspace.RemoveDocument(contentId);
            _docs.Remove(contentId);
        }

        // Returns the contentIds of all currently-tracked open documents.
        public IReadOnlyList<string> GetOpenIds() => _docs.Keys.ToList();

        // Removes all tracked documents. Call from skill OnGameExit() / Stop().
        public void CloseAll(ISkillWorkspace workspace)
        {
            foreach (var id in _docs.Keys.ToList())
                workspace.RemoveDocument(id);
            _docs.Clear();
        }
    }
}
