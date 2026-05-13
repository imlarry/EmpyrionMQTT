using System.IO;
using EDNAClient.Core;
using EDNAClient.Helpers;
using EDNAClient.Workspace;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Skills.FloorMap
{
    public class FloorMapSkill : IEdnaSkill, IHotkeyProvider, IGameContextReceiver, IDocumentSkill, IPlayfieldObserver
    {
        private readonly ISkillWorkspace _workspace;

        private FloorMapper? _mapper;
        private NavNode?     _rootNode;
        private string?      _mapsDir;

        private string _solarSystem = "Unknown";
        private string _playfield   = "Unknown";

        private readonly DocumentTracker                      _docs     = new DocumentTracker();
        private readonly Dictionary<string, FloorMapDocument> _openData = new Dictionary<string, FloorMapDocument>();

        public string Id    => "FloorMap";
        public string Title => "Floor Map";

        public FloorMapSkill(ISkillWorkspace workspace)
        {
            _workspace = workspace;
        }

        // ── IEdnaSkill ────────────────────────────────────────────────────────

        public async Task StartAsync(IMessageBus bus)
        {
            _mapper = new FloorMapper();
            await _mapper.StartAsync(bus);
        }

        // Full stop: close UI then tear down MQTT.
        public void Stop()
        {
            OnGameExit();
            _mapper?.Stop();
            _mapper = null;
        }

        public void SnapToGameWindow() { }

        // ── IGameContextReceiver ──────────────────────────────────────────────

        public void OnGameEnter(string saveGamePath)
        {
            try
            {
                _mapsDir = Path.Combine(saveGamePath, "Content", "Mods", "ESB", "EDNA", "skills", "floormap");
                Directory.CreateDirectory(_mapsDir);
                EdnaLogger.Log($"FloorMap mapsDir={_mapsDir}");
                UI.Invoke(BuildNavTree);
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("FloorMapSkill.OnGameEnter failed", ex);
            }
        }

        // Closes all open documents and removes the nav section. Keeps MQTT alive.
        public void OnGameExit()
        {
            UI.Invoke(() =>
            {
                if (_rootNode != null)
                    _workspace.NavViewModel.RemoveRootSection(Title);
                _rootNode = null;

                _openData.Clear();
                _docs.CloseAll(_workspace);
            });
            _mapsDir = null;
        }

        // ── IDocumentSkill ────────────────────────────────────────────────────

        public IReadOnlyList<string> GetOpenDocumentIds() =>
            _docs.GetOpenIds();

        // Searches _mapsDir recursively for each saved document file and re-opens it.
        public void RestoreDocuments(IReadOnlyList<string> contentIds)
        {
            if (_mapsDir == null) return;
            foreach (var id in contentIds)
            {
                EdnaLogger.Log($"[FloorMap] restoring document '{id}'");
                var matches = Directory.GetFiles(_mapsDir, $"{id}.json", SearchOption.AllDirectories);
                if (matches.Length > 0)
                    OpenFromFile(matches[0]);
                else
                    EdnaLogger.Warn($"[FloorMap] restore: no file found for document '{id}'");
            }
        }

        // ── IHotkeyProvider ───────────────────────────────────────────────────

        public IEnumerable<HotkeyRequest> GetHotkeyRequests()
        {
            yield return new HotkeyRequest(
                HotkeyRequest.ModControl | HotkeyRequest.ModShift | HotkeyRequest.NoRepeat,
                0x52,   // VK_R
                OnCaptureHotkey);
        }

        private void OnCaptureHotkey()
        {
            EdnaLogger.Log($"[FloorMap] Ctrl+Shift+R pressed: mapper={_mapper != null} mapsDir={_mapsDir}");
            if (_mapper == null) { EdnaLogger.Warn("[FloorMap] hotkey ignored: mapper not started"); return; }

            var ss = _solarSystem;
            var pf = _playfield;

            var dir = _mapsDir;
            _ = Task.Run(async () =>
            {
                FloorMapDocument? doc = null;
                try
                {
                    doc = await _mapper.RefreshAsync(ss, pf, dir, status =>
                        UI.Invoke(() => UpdateOrShowStatus(status)));
                }
                catch (Exception ex)
                {
                    EdnaLogger.Error("FloorMapSkill capture failed", ex);
                }

                if (doc == null)
                {
                    EdnaLogger.Warn("[FloorMap] RefreshAsync returned null -- no document produced");
                    return;
                }

                EdnaLogger.Log($"[FloorMap] capture succeeded: docId={doc.DocumentId} Y={doc.Y}");
                UI.Invoke(() => ApplyCapturedDocument(doc));
            });
        }

        private void UpdateOrShowStatus(string status)
        {
            foreach (var d in _openData.Values)
                d.StatusText = status;
        }

        private void ApplyCapturedDocument(FloorMapDocument incoming)
        {
            var docId = incoming.DocumentId;

            if (_openData.TryGetValue(docId, out var existing))
            {
                existing.UpdateFrom(incoming);
                _docs.TryActivate(docId);
                if (_mapsDir != null)
                {
                    var path = MapFilePath(incoming);
                    if (File.Exists(path)) incoming.Save(path);
                }
                return;
            }

            bool fileExists = false;
            if (_mapsDir != null)
            {
                var path = MapFilePath(incoming);
                if (File.Exists(path))
                {
                    incoming.Save(path);
                    fileExists = true;
                }
            }

            OpenDocumentTab(incoming, dirty: !fileExists);

            if (fileExists) RebuildNavTree();
        }

        // ── MQTT event handler ────────────────────────────────────────────────

        public void OnPlayfieldLoaded(string solarSystem, string playfield, double x, double y, double z)
        {
            if (!string.IsNullOrEmpty(solarSystem)) _solarSystem = solarSystem;
            if (!string.IsNullOrEmpty(playfield))   _playfield   = playfield;
            EdnaLogger.Log($"[FloorMap] location updated: solarSystem={_solarSystem} playfield={_playfield}");
        }

        // ── Document lifecycle ────────────────────────────────────────────────

        private void OpenDocumentTab(FloorMapDocument doc, bool dirty)
        {
            var docId = doc.DocumentId;
            _openData[docId] = doc;
            _docs.Open(_workspace,
                title:     dirty ? $"*{doc.ShortTitle}" : doc.ShortTitle,
                contentId: docId,
                content:   new FloorMapPanel(doc),
                onClosing: dirty ? (EventHandler<System.ComponentModel.CancelEventArgs>)((_, e) => OnDirtyDocumentClosing(doc, e)) : null,
                onClosed:  () => _openData.Remove(docId));

            _workspace.SetDocumentMenuItems(docId, new[]
            {
                new DocumentMenuAction { Header = "Save",   Execute = () => UI.Invoke(() => SaveDocument(docId)) },
                new DocumentMenuAction { Header = "Delete", Execute = () => UI.Invoke(() => DeleteDocument(docId)) },
            });
        }

        private void SaveDocument(string docId)
        {
            if (_mapsDir == null || !_openData.TryGetValue(docId, out var doc)) return;
            var path = MapFilePath(doc);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            doc.Save(path);
            _docs.UpdateTitle(docId, doc.ShortTitle);
            RebuildNavTree();
        }

        private void DeleteDocument(string docId)
        {
            if (_mapsDir == null || !_openData.TryGetValue(docId, out var doc)) return;
            DeleteMapFile(MapFilePath(doc));
        }

        private void OnDirtyDocumentClosing(FloorMapDocument doc, System.ComponentModel.CancelEventArgs e)
        {
            if (_mapsDir == null) return;

            var result = MessageBox.Show(_workspace.DialogOwner,
                $"Save floor map for entity {doc.EntityId} Y={doc.Y}?",
                "Save Floor Map",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                var path = MapFilePath(doc);
                try
                {
                    doc.Save(path);
                    RebuildNavTree();
                }
                catch (Exception ex)
                {
                    EdnaLogger.Warn($"FloorMap save failed: {ex.Message}");
                }
            }
        }

        private void OpenFromFile(string path)
        {
            var doc = FloorMapDocument.Load(path);
            if (doc == null) return;

            var docId = doc.DocumentId;
            if (_docs.TryActivate(docId)) return;
            OpenDocumentTab(doc, dirty: false);
        }

        // ── Nav tree ──────────────────────────────────────────────────────────

        private void BuildNavTree()
        {
            if (_mapsDir == null) return;

            _rootNode = new NavNode
            {
                Name       = Title,
                NodeType   = NavNodeType.MapRoot,
                IsExpanded = true,
            };

            PopulateNavFromDisk(_rootNode, _mapsDir);
            _workspace.NavViewModel.AddRootSection(_rootNode);
        }

        private void RebuildNavTree()
        {
            if (_rootNode == null || _mapsDir == null) return;
            _rootNode.Children.Clear();
            PopulateNavFromDisk(_rootNode, _mapsDir);
        }

        private void PopulateNavFromDisk(NavNode root, string mapsDir)
        {
            if (!Directory.Exists(mapsDir)) return;

            foreach (var ssDir in Directory.GetDirectories(mapsDir).OrderBy(Path.GetFileName))
            {
                var ssName = Path.GetFileName(ssDir);
                var ssNode = new NavNode { Name = ssName, NodeType = NavNodeType.MapSolarSystem };

                foreach (var pfDir in Directory.GetDirectories(ssDir).OrderBy(Path.GetFileName))
                {
                    var pfName = Path.GetFileName(pfDir);
                    var pfNode = new NavNode { Name = pfName, NodeType = NavNodeType.MapPlayfield };

                    foreach (var file in Directory.GetFiles(pfDir, "*.json").OrderBy(Path.GetFileName))
                    {
                        var f       = file;
                        var entId   = Path.GetFileNameWithoutExtension(file);
                        var leaf = new NavNode
                        {
                            Name       = $"Entity {entId}",
                            NodeType   = NavNodeType.MapEntity,
                            Tag        = f,
                            OnSelected = () => UI.Invoke(() => OpenFromFile(f)),
                        };
                        leaf.ContextItems = new List<NavMenuItem>
                        {
                            new NavMenuItem { Header = "Open",   Execute = () => UI.Invoke(() => OpenFromFile(f)) },
                            new NavMenuItem { Header = "Delete", Execute = () => UI.Invoke(() => DeleteMapFile(f)) },
                        };
                        pfNode.Children.Add(leaf);
                    }

                    if (pfNode.Children.Count > 0)
                        ssNode.Children.Add(pfNode);
                }

                if (ssNode.Children.Count > 0)
                    root.Children.Add(ssNode);
            }
        }

        private void DeleteMapFile(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (MessageBox.Show(_workspace.DialogOwner, $"Delete map '{name}'?", "Delete Floor Map",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var docId = name;
            _docs.Remove(_workspace, docId);
            _openData.Remove(docId);

            try { File.Delete(path); }
            catch (Exception ex) { EdnaLogger.Warn($"FloorMap delete '{path}' failed: {ex.Message}"); }

            RebuildNavTree();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string MapFilePath(FloorMapDocument doc)
        {
            var safe = (string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_mapsDir!,
                safe(doc.SolarSystem),
                safe(doc.Playfield),
                $"{doc.EntityId}.json");
        }
    }
}
