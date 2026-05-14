using System.IO;
using EDNAClient.Core;
using EDNAClient.Helpers;
using EDNAClient.Workspace;
using ESB.Messaging;

namespace EDNAClient.Skills.Scripting.ScriptEditor
{
    public class ScriptEditorSkill : IEdnaSkill, IGameContextReceiver, IDocumentSkill
    {
        private readonly ISkillWorkspace  _workspace;
        private readonly DocumentTracker  _docs = new DocumentTracker();
        private string?  _scriptsDir;
        private NavNode? _rootNode;

        public string Id => "ScriptEditor";

        public ScriptEditorSkill(ISkillWorkspace workspace)
        {
            _workspace = workspace;
        }

        // ── IEdnaSkill ────────────────────────────────────────────────────────

        public Task StartAsync(EdnaContext ctx) => Task.CompletedTask;

        // Full stop: same as OnGameExit since ScriptEditor has no MQTT subscriptions.
        public void Stop() => OnGameExit();

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
                UI.Invoke(BuildNavTree);
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("ScriptEditor GameEnter failed", ex);
            }
        }

        // Closes all open documents and removes the nav section.
        public void OnGameExit()
        {
            UI.Invoke(() =>
            {
                if (_rootNode != null)
                    _workspace.NavViewModel.RemoveRootSection("Scripting");
                _rootNode   = null;
                _scriptsDir = null;
                _docs.CloseAll(_workspace);
            });
        }

        // ── IDocumentSkill ────────────────────────────────────────────────────

        public IReadOnlyList<string> GetOpenDocumentIds() => _docs.GetOpenIds();

        // Re-opens script files from a previous session.
        // contentIds are lowercased file paths (the DocumentTracker key format).
        public void RestoreDocuments(IReadOnlyList<string> contentIds)
        {
            foreach (var contentId in contentIds)
            {
                EdnaLogger.Log($"[ScriptEditor] restoring document '{contentId}'");
                if (File.Exists(contentId))
                    OpenScript(contentId, Path.GetFileName(contentId));
                else
                    EdnaLogger.Warn($"[ScriptEditor] restore: file not found '{contentId}'");
            }
        }

        // ── Nav tree ──────────────────────────────────────────────────────────

        private void BuildNavTree()
        {
            if (_scriptsDir == null) return;

            var dir = _scriptsDir;
            EdnaLogger.Log($"Building nav tree from {dir}");
            _rootNode = NavBuilder.RootFolderNode(
                "Scripting", dir,
                onNewFile:   () => CreateFile(dir),
                onNewFolder: () => CreateFolder(dir));
            _rootNode.IsExpanded = false;

            PopulateChildren(_rootNode, dir);
            _workspace.NavViewModel.AddRootSection(_rootNode);
            EdnaLogger.Log($"Nav tree built ({_rootNode.Children.Count} top-level items)");
        }

        private void PopulateChildren(NavNode parent, string dir)
        {
            parent.Children.Clear();

            foreach (var subDir in Directory.GetDirectories(dir).OrderBy(Path.GetFileName))
            {
                var d    = subDir;
                var name = Path.GetFileName(subDir);
                var folder = NavBuilder.FolderNode(
                    name, d,
                    onNewFile:   () => CreateFile(d),
                    onNewFolder: () => CreateFolder(d),
                    onRename:    () => RenameEntry(d, isFolder: true),
                    onDelete:    () => DeleteFolder(d));
                EdnaLogger.Detail($"[ScriptEditor] folder node: {name}");
                PopulateChildren(folder, d);
                parent.Children.Add(folder);
            }

            foreach (var file in Directory.GetFiles(dir, "*.lua").OrderBy(Path.GetFileName))
            {
                var f    = file;
                var name = Path.GetFileName(file);
                var node = NavBuilder.FileNode(
                    name, f,
                    onOpen:   () => OpenScript(f, name),
                    onRename: () => RenameEntry(f, isFolder: false),
                    onDelete: () => DeleteFile(f));
                EdnaLogger.Detail($"[ScriptEditor] file node: {name}");
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
            if (_docs.TryActivate(contentId)) return;

            var panel = new ScriptEditorPanel(path, dirty =>
                UI.Invoke(() => _docs.UpdateTitle(contentId, dirty ? $"*{name}" : name)));

            _docs.Open(_workspace, name, contentId, panel);

            _workspace.SetDocumentMenuItems(contentId, new[]
            {
                new DocumentMenuAction { Header = "Save",   Execute = () => UI.Invoke(() => panel.Save()) },
                new DocumentMenuAction { Header = "Rename", Execute = () => UI.Invoke(() => RenameEntry(path, isFolder: false)) },
                new DocumentMenuAction { Header = "Delete", Execute = () => UI.Invoke(() => DeleteFile(path)) },
            });
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        private void CreateFile(string dir)
        {
            var dlg = new InputDialog("New Script", "File name (.lua):", "new_script.lua")
            { Owner = _workspace.DialogOwner };
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
            { Owner = _workspace.DialogOwner };
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
            { Owner = _workspace.DialogOwner };
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
                MessageBox.Show(_workspace.DialogOwner, ex.Message, "Rename Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteFile(string path)
        {
            var name = Path.GetFileName(path);
            if (MessageBox.Show(_workspace.DialogOwner, $"Delete '{name}'?", "Delete Script",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _docs.Remove(_workspace, path.ToLowerInvariant());
            try { File.Delete(path); }
            catch (Exception ex) { EdnaLogger.Warn($"Delete script '{path}' failed: {ex.Message}"); }
            RebuildTree();
        }

        private void DeleteFolder(string path)
        {
            var name = Path.GetFileName(path);
            if (MessageBox.Show(_workspace.DialogOwner, $"Delete folder '{name}' and all its contents?", "Delete Folder",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try { Directory.Delete(path, recursive: true); }
            catch (Exception ex) { EdnaLogger.Warn($"Delete folder '{path}' failed: {ex.Message}"); }
            RebuildTree();
        }
    }
}
