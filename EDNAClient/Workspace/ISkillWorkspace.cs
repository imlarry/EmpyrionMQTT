using System.Windows;
using AvalonDock.Layout;

namespace EDNAClient.Workspace
{
    public interface ISkillWorkspace
    {
        NavigationViewModel NavViewModel { get; }

        // Parent window for dialogs (MessageBox.Show, InputDialog.Owner, etc.).
        Window DialogOwner { get; }

        LayoutDocument OpenDocument(string title, string contentId, UIElement content);
        void RemoveDocument(string contentId);
        IReadOnlyList<string> GetSavedDocuments(string skillId);

        // Registers context menu items shown at the top of the tab right-click menu
        // and in the layout picker dropdown for the given document.
        void SetDocumentMenuItems(string contentId, IEnumerable<DocumentMenuAction> items);
    }
}
