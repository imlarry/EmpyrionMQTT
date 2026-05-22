using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EDNAClient.Core;
using EDNAClient.Helpers;
using EDNAClient.Workspace;

namespace EDNAClient.Skills.Tomography
{
    public class TomographySkill : IEdnaSkill, IGameContextReceiver, IDocumentSkill, IPlayfieldObserver
    {
        private readonly ISkillWorkspace _workspace;

        private TomographyScanner? _scanner;
        private NavNode?           _rootNode;
        private string?            _scansDir;

        private string _solarSystem = "Unknown";
        private string _playfield   = "Unknown";

        private readonly DocumentTracker                        _docs     = new DocumentTracker();
        private readonly Dictionary<string, TomographyDocument> _openData = new Dictionary<string, TomographyDocument>();

        public string Id    => "Tomography";
        public string Title => "Tomography";

        public TomographySkill(ISkillWorkspace workspace)
        {
            _workspace = workspace;
        }

        // ── IEdnaSkill ────────────────────────────────────────────────────────

        public async Task StartAsync(EdnaContext ctx)
        {
            _scanner = new TomographyScanner();
            await _scanner.StartAsync(ctx);
        }

        public void Stop()
        {
            OnGameExit();
            _scanner?.Stop();
            _scanner = null;
        }

        public void SnapToGameWindow() { }

        // ── IGameContextReceiver ──────────────────────────────────────────────

        public void OnGameEnter(string saveGamePath)
        {
            try
            {
                _scansDir = Path.Combine(saveGamePath, "Content", "Mods", "ESB", "EDNA", "skills", "tomography");
                Directory.CreateDirectory(_scansDir);
                EdnaLogger.Log($"Tomography scansDir={_scansDir}");
                UI.Invoke(BuildNavTree);
            }
            catch (Exception ex)
            {
                EdnaLogger.Error("TomographySkill.OnGameEnter failed", ex);
            }
        }

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
            _scansDir = null;
        }

        // ── IDocumentSkill ────────────────────────────────────────────────────

        public IReadOnlyList<string> GetOpenDocumentIds() => _docs.GetOpenIds();

        public void RestoreDocuments(IReadOnlyList<string> contentIds)
        {
            if (_scansDir == null) return;
            foreach (var id in contentIds)
            {
                EdnaLogger.Log($"[Tomography] restoring document '{id}'");
                // Persisted file name uses the entity id (matches FloorMap); document id has 'tomo-' prefix.
                var entitySegment = id.StartsWith("tomo-") ? id.Substring(5) : id;
                var matches = Directory.GetFiles(_scansDir, $"{entitySegment}.json", SearchOption.AllDirectories);
                if (matches.Length > 0)
                    OpenFromFile(matches[0]);
                else
                    EdnaLogger.Warn($"[Tomography] restore: no file found for document '{id}'");
            }
        }

        // ── Scan trigger ──────────────────────────────────────────────────────

        // Invoked from the Tomography nav root context menu ("Scan Current Structure").
        // Hotkeys were attempted but Empyrion's raw-input capture swallows global Win32
        // hotkeys before EDNA sees them, so this is the only reliable trigger.
        private void StartScan()
        {
            EdnaLogger.Log($"[Tomography] StartScan invoked: scanner={_scanner != null} scansDir={_scansDir} ss={_solarSystem} pf={_playfield}");
            if (_scanner == null) { EdnaLogger.Warn("[Tomography] scan ignored: scanner not started"); return; }

            var ss = _solarSystem;
            var pf = _playfield;

            _ = Task.Run(async () =>
            {
                EdnaLogger.Log("[Tomography] background scan task started");
                TomographyDocument? doc = null;
                try
                {
                    doc = await _scanner.ScanAsync(ss, pf, status =>
                    {
                        EdnaLogger.Log($"[Tomography] status: {status}");
                        UI.Invoke(() => UpdateOrShowStatus(status));
                    });
                }
                catch (Exception ex)
                {
                    EdnaLogger.Error("TomographySkill capture failed", ex);
                }

                if (doc == null)
                {
                    EdnaLogger.Warn("[Tomography] ScanAsync returned null -- no document produced");
                    return;
                }

                EdnaLogger.Log($"[Tomography] capture succeeded: docId={doc.DocumentId} tris={doc.Indices.Length / 3}");
                UI.Invoke(() => ApplyCapturedDocument(doc));
            });
        }

        private void UpdateOrShowStatus(string status)
        {
            foreach (var d in _openData.Values)
                d.StatusText = status;
        }

        private void ApplyCapturedDocument(TomographyDocument incoming)
        {
            var docId = incoming.DocumentId;

            if (_openData.TryGetValue(docId, out var existing))
            {
                existing.UpdateFrom(incoming);
                _docs.TryActivate(docId);
                if (_scansDir != null)
                {
                    var path = ScanFilePath(incoming);
                    if (File.Exists(path)) incoming.Save(path);
                }
                return;
            }

            bool fileExists = false;
            if (_scansDir != null)
            {
                var path = ScanFilePath(incoming);
                if (File.Exists(path))
                {
                    incoming.Save(path);
                    fileExists = true;
                }
            }

            OpenDocumentTab(incoming, dirty: !fileExists);

            if (fileExists) RebuildNavTree();
        }

        // ── IPlayfieldObserver ────────────────────────────────────────────────

        public void OnPlayfieldLoaded(string solarSystem, string playfield, double x, double y, double z)
        {
            if (!string.IsNullOrEmpty(solarSystem)) _solarSystem = solarSystem;
            if (!string.IsNullOrEmpty(playfield))   _playfield   = playfield;
            EdnaLogger.Log($"[Tomography] location updated: solarSystem={_solarSystem} playfield={_playfield}");
        }

        // ── Document lifecycle ────────────────────────────────────────────────

        private void OpenDocumentTab(TomographyDocument doc, bool dirty)
        {
            var docId = doc.DocumentId;
            _openData[docId] = doc;
            _docs.Open(_workspace,
                title:     dirty ? $"*{doc.ShortTitle}" : doc.ShortTitle,
                contentId: docId,
                content:   new TomographyPanel(doc),
                onClosing: dirty ? (EventHandler<CancelEventArgs>)((_, e) => OnDirtyDocumentClosing(doc, e)) : null,
                onClosed:  () => _openData.Remove(docId));

            _workspace.SetDocumentMenuItems(docId, new[]
            {
                new DocumentMenuAction { Header = "Save",   Execute = () => UI.Invoke(() => SaveDocument(docId)) },
                new DocumentMenuAction { Header = "Delete", Execute = () => UI.Invoke(() => DeleteDocument(docId)) },
            });
        }

        private void SaveDocument(string docId)
        {
            if (_scansDir == null || !_openData.TryGetValue(docId, out var doc)) return;
            var path = ScanFilePath(doc);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            doc.Save(path);
            _docs.UpdateTitle(docId, doc.ShortTitle);
            RebuildNavTree();
        }

        private void DeleteDocument(string docId)
        {
            if (_scansDir == null || !_openData.TryGetValue(docId, out var doc)) return;
            DeleteScanFile(ScanFilePath(doc));
        }

        private void OnDirtyDocumentClosing(TomographyDocument doc, CancelEventArgs e)
        {
            if (_scansDir == null) return;

            var result = MessageBox.Show(_workspace.DialogOwner,
                $"Save tomography for entity {doc.EntityId}?",
                "Save Tomography",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) { e.Cancel = true; return; }

            if (result == MessageBoxResult.Yes)
            {
                var path = ScanFilePath(doc);
                try
                {
                    doc.Save(path);
                    RebuildNavTree();
                }
                catch (Exception ex)
                {
                    EdnaLogger.Warn($"Tomography save failed: {ex.Message}");
                }
            }
        }

        private void OpenFromFile(string path)
        {
            var doc = TomographyDocument.Load(path);
            if (doc == null) return;

            var docId = doc.DocumentId;
            if (_docs.TryActivate(docId)) return;
            OpenDocumentTab(doc, dirty: false);
        }

        // ── Nav tree ──────────────────────────────────────────────────────────

        private void BuildNavTree()
        {
            if (_scansDir == null) return;

            _rootNode = new NavNode
            {
                Name       = Title,
                NodeType   = NavNodeType.MapRoot,
                IsExpanded = true,
                ContextItems = new List<NavMenuItem>
                {
                    new NavMenuItem
                    {
                        Header  = "Scan Current Structure",
                        Execute = () =>
                        {
                            EdnaLogger.Log("[Tomography] context menu click");
                            UI.Invoke(StartScan);
                        }
                    },
                },
            };

            PopulateNavFromDisk(_rootNode, _scansDir);
            _workspace.NavViewModel.AddRootSection(_rootNode);
        }

        private void RebuildNavTree()
        {
            if (_rootNode == null || _scansDir == null) return;
            _rootNode.Children.Clear();
            PopulateNavFromDisk(_rootNode, _scansDir);
        }

        private void PopulateNavFromDisk(NavNode root, string scansDir)
        {
            if (!Directory.Exists(scansDir)) return;

            foreach (var ssDir in Directory.GetDirectories(scansDir).OrderBy(Path.GetFileName))
            {
                var ssName = Path.GetFileName(ssDir);
                var ssNode = new NavNode { Name = ssName, NodeType = NavNodeType.MapSolarSystem };

                foreach (var pfDir in Directory.GetDirectories(ssDir).OrderBy(Path.GetFileName))
                {
                    var pfName = Path.GetFileName(pfDir);
                    var pfNode = new NavNode { Name = pfName, NodeType = NavNodeType.MapPlayfield };

                    foreach (var file in Directory.GetFiles(pfDir, "*.json").OrderBy(Path.GetFileName))
                    {
                        var f     = file;
                        var entId = Path.GetFileNameWithoutExtension(file);
                        var leaf  = new NavNode
                        {
                            Name       = $"Entity {entId}",
                            NodeType   = NavNodeType.MapEntity,
                            Tag        = f,
                            OnSelected = () => UI.Invoke(() => OpenFromFile(f)),
                        };
                        leaf.ContextItems = new List<NavMenuItem>
                        {
                            new NavMenuItem { Header = "Open",   Execute = () => UI.Invoke(() => OpenFromFile(f)) },
                            new NavMenuItem { Header = "Delete", Execute = () => UI.Invoke(() => DeleteScanFile(f)) },
                        };
                        pfNode.Children.Add(leaf);
                    }

                    if (pfNode.Children.Count > 0) ssNode.Children.Add(pfNode);
                }

                if (ssNode.Children.Count > 0) root.Children.Add(ssNode);
            }
        }

        private void DeleteScanFile(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (MessageBox.Show(_workspace.DialogOwner, $"Delete tomography '{name}'?", "Delete Tomography",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            // Filename is the entity id; document id has 'tomo-' prefix.
            var docId = $"tomo-{name}";
            _docs.Remove(_workspace, docId);
            _openData.Remove(docId);

            try { File.Delete(path); }
            catch (Exception ex) { EdnaLogger.Warn($"Tomography delete '{path}' failed: {ex.Message}"); }

            RebuildNavTree();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string ScanFilePath(TomographyDocument doc)
        {
            Func<string, string> safe = s => string.Join("_", s.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_scansDir!,
                safe(doc.SolarSystem),
                safe(doc.Playfield),
                $"{doc.EntityId}.json");
        }
    }
}
