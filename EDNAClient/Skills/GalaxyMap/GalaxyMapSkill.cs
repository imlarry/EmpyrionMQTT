using System.IO;
using EDNAClient.Core;
using EDNAClient.Helpers;
using EDNAClient.Workspace;
using ESB.Messaging;

namespace EDNAClient.Skills.GalaxyMap
{
    public sealed class GalaxyMapSkill : IEdnaSkill, IGameContextReceiver, IPlayfieldObserver
    {
        public string Id => "GalaxyMap";

        private readonly ISkillWorkspace    _workspace;
        private readonly GalaxyMapViewModel _vm = new GalaxyMapViewModel();
        private GalaxyMapPanel? _panel;

        public GalaxyMapSkill(ISkillWorkspace workspace)
        {
            _workspace = workspace;
        }

        public Task StartAsync(EdnaContext ctx) => Task.CompletedTask;

        public void Stop() { }

        public void SnapToGameWindow() { }

        public void OnGameEnter(string saveGamePath)
        {
            var csv     = Path.Combine(saveGamePath, "Content", "Mods", "GalaxyExtract", "galaxy.csv");
            var systems = GalaxyLoader.Load(csv);

            UI.InvokeAsync(() =>
            {
                _vm.LoadSystems(systems);

                var allNode  = NavBuilder.ActionNode("All Systems",  NavNodeType.GalaxyFilter, () => OpenMap(GalaxyFilter.All));
                var nearNode = NavBuilder.ActionNode("Within 30 LY", NavNodeType.GalaxyFilter, () => OpenMap(GalaxyFilter.Near30LY));

                var galaxyRoot = new NavNode
                {
                    Name       = "Galaxy",
                    NodeType   = NavNodeType.Galaxy,
                    IsExpanded = true,
                };
                galaxyRoot.Children.Add(allNode);
                galaxyRoot.Children.Add(nearNode);
                _workspace.NavViewModel.AddRootSection(galaxyRoot);
                EdnaLogger.Detail("[GalaxyMapSkill] nav nodes added");
            });
        }

        public void OnGameExit()
        {
            UI.Invoke(() => _workspace.NavViewModel.RemoveRootSection("Galaxy"));
        }

        public void OnPlayfieldLoaded(string solarSystem, string playfield, double x, double y, double z)
        {
            EdnaLogger.Detail($"[GalaxyMapSkill] player system coords ({x},{y},{z})");
            UI.Invoke(() => _vm.SetPlayerPosition(x, y, z));
        }

        private void OpenMap(GalaxyFilter filter)
        {
            _vm.SetFilter(filter);
            _panel ??= new GalaxyMapPanel(_vm);
            _workspace.OpenDocument("Galaxy Map", Id, _panel);
        }
    }
}
