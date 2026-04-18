using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AvalonDock.Layout;
using EDNAClient.Core;
using EDNAClient.Workspace;
using ESB.Messaging;

namespace EDNAClient.Skills.ScriptEditor
{
    public class ScriptEditorSkill : IEdnaSkill, IGameContextReceiver
    {
        private readonly WorkspaceWindow _workspace;
        private string?  _scriptsDir;
        private NavNode? _rootNode;

        private readonly Dictionary<string, LayoutDocument> _openDocs = new();

        public string Id => "ScriptEditor";

        public ScriptEditorSkill(WorkspaceWindow workspace)
        {
            _workspace = workspace;
        }

        public Task StartAsync(IMessenger messenger) => Task.CompletedTask;

        public void Stop()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_rootNode != null)
                    _workspace.NavViewModel.RemoveRootSection("Scripting");
                _rootNode = null;

                foreach (var contentId in _openDocs.Keys.ToList())
                    _workspace.RemoveDocument(contentId);
                _openDocs.Clear();

                _scriptsDir = null;
            });
        }

        public void SnapToGameWindow() { }

        // ── IGameContextReceiver ──────────────────────────────────────────────

        public void OnGameEnter(string saveGamePath)
        {
            try
            {
                _scriptsDir = Path.Combine(saveGamePath, "Content", "Mods", "ESB", "EDNA", "skills", "scripting");
                EdnaLogger.Log($"ScriptEditor GameEnter: scriptsDir={_scriptsDir}");
                Directory.CreateDirectory(_scriptsDir);
                EdnaLogger.Log("Scripts directory ready");
                Application.Current.Dispatcher.Invoke(BuildNavTree);
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("ScriptEditor GameEnter failed", ex);
            }
        }

        // ── Nav tree ──────────────────────────────────────────────────────────

        private void BuildNavTree()
        {
            if (_scriptsDir == null) return;

            var dir = _scriptsDir;
            EdnaLogger.Log($"Building nav tree from {dir}");
            _rootNode = new NavNode
            {
                Name       = "Scripting",
                NodeType   = NavNodeType.ScriptFolder,
                Tag        = dir,
                IsExpanded = false,
                ContextItems = new List<NavMenuItem>
                {
                    new NavMenuItem { Header = "New Script", Execute = () => CreateFile(dir) },
                    new NavMenuItem { Header = "New Folder", Execute = () => CreateFolder(dir) },
                },
            };

            PopulateChildren(_rootNode, dir);
            _workspace.NavViewModel.AddRootSection(_rootNode);
            EdnaLogger.Log($"Nav tree built ({_rootNode.Children.Count} top-level items)");
        }

        private void PopulateChildren(NavNode parent, string dir)
        {
            parent.Children.Clear();

            foreach (var subDir in Directory.GetDirectories(dir).OrderBy(Path.GetFileName))
            {
                var capturedDir = subDir;
                var name        = Path.GetFileName(subDir);
                var folder      = new NavNode
                {
                    Name     = name,
                    NodeType = NavNodeType.ScriptFolder,
                    Tag      = capturedDir,
                    ContextItems = new List<NavMenuItem>
                    {
                        new NavMenuItem { Header = "New Script", Execute = () => CreateFile(capturedDir) },
                        new NavMenuItem { Header = "New Folder", Execute = () => CreateFolder(capturedDir) },
                        NavMenuItem.Separator(),
                        new NavMenuItem { Header = "Rename",     Execute = () => RenameEntry(capturedDir, isFolder: true) },
                        new NavMenuItem { Header = "Delete",     Execute = () => DeleteFolder(capturedDir) },
                    },
                };
                PopulateChildren(folder, capturedDir);
                parent.Children.Add(folder);
            }

            foreach (var file in Directory.GetFiles(dir, "*.lua").OrderBy(Path.GetFileName))
            {
                var capturedPath = file;
                var name         = Path.GetFileName(file);
                var node         = new NavNode
                {
                    Name     = name,
                    NodeType = NavNodeType.ScriptFile,
                    Tag      = capturedPath,
                };
                node.OnSelected  = () => OpenScript(capturedPath, name);
                node.ContextItems = new List<NavMenuItem>
                {
                    new NavMenuItem { Header = "Open",   Execute = () => OpenScript(capturedPath, name) },
                    new NavMenuItem { Header = "Rename", Execute = () => RenameEntry(capturedPath, isFolder: false) },
                    new NavMenuItem { Header = "Delete", Execute = () => DeleteFile(capturedPath) },
                };
                parent.Children.Add(node);
            }
        }

        private void RebuildTree()
        {
            if (_rootNode == null || _scriptsDir == null) return;
            PopulateChildren(_rootNode, _scriptsDir);
        }

        // ── Open script ───────────────────────────────────────────────────────

        private void OpenScript(string path, string name)
        {
            var contentId = path.ToLowerInvariant();

            if (_openDocs.TryGetValue(contentId, out var existing))
            {
                existing.IsActive = true;
                return;
            }

            var panel = new ScriptEditorPanel(path, dirty =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_openDocs.TryGetValue(contentId, out var doc))
                        doc.Title = dirty ? $"*{name}" : name;
                });
            });

            var newDoc = _workspace.OpenDocument(name, contentId, panel);
            _openDocs[contentId] = newDoc;

            newDoc.Closed += (_, _) => _openDocs.Remove(contentId);
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        private void CreateFile(string dir)
        {
            var dlg = new InputDialog("New Script", "File name (.lua):", "new_script.lua")
            { Owner = _workspace };
            if (dlg.ShowDialog() != true) return;

            var name = dlg.Result.Trim();
            if (!name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) name += ".lua";
            var path = Path.Combine(dir, name);
            if (!File.Exists(path)) File.WriteAllText(path, "");
            RebuildTree();
            OpenScript(path, name);
        }

        private void CreateFolder(string dir)
        {
            var dlg = new InputDialog("New Folder", "Folder name:", "scripts")
            { Owner = _workspace };
            if (dlg.ShowDialog() != true) return;

            var name = dlg.Result.Trim();
            if (string.IsNullOrEmpty(name)) return;
            Directory.CreateDirectory(Path.Combine(dir, name));
            RebuildTree();
        }

        private void RenameEntry(string path, bool isFolder)
        {
            var oldName = Path.GetFileName(path);
            var dlg     = new InputDialog("Rename", "New name:", oldName)
            { Owner = _workspace };
            if (dlg.ShowDialog() != true) return;

            var newName = dlg.Result.Trim();
            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            var newPath = Path.Combine(Path.GetDirectoryName(path)!, newName);
            try
            {
                if (isFolder) Directory.Move(path, newPath);
                else          File.Move(path, newPath);
                RebuildTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show(_workspace, ex.Message, "Rename Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteFile(string path)
        {
            var name = Path.GetFileName(path);
            if (MessageBox.Show(_workspace, $"Delete '{name}'?", "Delete Script",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var contentId = path.ToLowerInvariant();
            _workspace.RemoveDocument(contentId);
            _openDocs.Remove(contentId);

            try { File.Delete(path); }
            catch (Exception ex) { EdnaLogger.Warn($"Delete script '{path}' failed: {ex.Message}"); }
            RebuildTree();
        }

        private void DeleteFolder(string path)
        {
            var name = Path.GetFileName(path);
            if (MessageBox.Show(_workspace, $"Delete folder '{name}' and all its contents?", "Delete Folder",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try { Directory.Delete(path, recursive: true); }
            catch (Exception ex) { EdnaLogger.Warn($"Delete folder '{path}' failed: {ex.Message}"); }
            RebuildTree();
        }
    }
}
