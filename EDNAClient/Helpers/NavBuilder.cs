using System;
using System.Collections.Generic;
using EDNAClient.Workspace;

namespace EDNAClient.Helpers
{
    // Factory methods for common NavNode shapes, eliminating verbose object initializers
    // and the closure-capture boilerplate required before each node lambda.
    internal static class NavBuilder
    {
        // Simple clickable node (e.g. galaxy filter, view shortcut).
        public static NavNode ActionNode(string name, NavNodeType type, Action onSelected)
            => new NavNode { Name = name, NodeType = type, OnSelected = onSelected };

        // Folder node with the standard New/Rename/Delete context menu.
        public static NavNode FolderNode(string name, string path,
            Action onNewFile, Action onNewFolder, Action onRename, Action onDelete)
            => new NavNode
            {
                Name     = name,
                NodeType = NavNodeType.ScriptFolder,
                Tag      = path,
                ContextItems = new List<NavMenuItem>
                {
                    new NavMenuItem { Header = "New Script", Execute = onNewFile },
                    new NavMenuItem { Header = "New Folder", Execute = onNewFolder },
                    NavMenuItem.Separator(),
                    new NavMenuItem { Header = "Rename", Execute = onRename },
                    new NavMenuItem { Header = "Delete", Execute = onDelete },
                },
            };

        // Root folder node: New/New only (no rename/delete on root).
        public static NavNode RootFolderNode(string name, string path,
            Action onNewFile, Action onNewFolder)
            => new NavNode
            {
                Name     = name,
                NodeType = NavNodeType.ScriptFolder,
                Tag      = path,
                ContextItems = new List<NavMenuItem>
                {
                    new NavMenuItem { Header = "New Script", Execute = onNewFile },
                    new NavMenuItem { Header = "New Folder", Execute = onNewFolder },
                },
            };

        // File leaf node with Open/Rename/Delete context menu.
        public static NavNode FileNode(string name, string path,
            Action onOpen, Action onRename, Action onDelete)
            => new NavNode
            {
                Name     = name,
                NodeType = NavNodeType.ScriptFile,
                Tag      = path,
                OnSelected = onOpen,
                ContextItems = new List<NavMenuItem>
                {
                    new NavMenuItem { Header = "Open",   Execute = onOpen },
                    new NavMenuItem { Header = "Rename", Execute = onRename },
                    new NavMenuItem { Header = "Delete", Execute = onDelete },
                },
            };
    }
}
