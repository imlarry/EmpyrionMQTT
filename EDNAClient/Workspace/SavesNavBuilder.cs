using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EDNAClient.Startup;

namespace EDNAClient.Workspace
{
    // Builds the lobby/offline level-0 navbar tree rooted at "Games" (which maps to
    // {EmpyrionRoot}/Saves/Games/). Only EDNA-aware save games are shown -- those with
    // a Content/Mods/ESB/EDNA/ folder. The path from the game folder down to EDNA is
    // intentionally not collapsed: the Content/Mods/ESB skeleton nodes are present
    // as-is so the structural gap stays honest. Inside the EDNA folder the tree
    // mirrors the actual filesystem.
    public static class SavesNavBuilder
    {
        public const string RootSectionName = "Games";

        // Returns null if the Empyrion root cannot be resolved or no Saves directory exists.
        public static NavNode? Build()
        {
            string? empyrionRoot = SteamLocator.GetEmpyrionPath();
            if (empyrionRoot == null) return null;

            string gamesDir = Path.Combine(empyrionRoot, "Saves", "Games");
            if (!Directory.Exists(gamesDir)) return null;

            var gamesNode = new NavNode { Name = RootSectionName, NodeType = NavNodeType.SavesRoot, Tag = gamesDir };

            foreach (var gameDir in SafeDirs(gamesDir).OrderBy(Path.GetFileName))
            {
                string ednaDir = Path.Combine(gameDir, "Content", "Mods", "ESB", "EDNA");
                if (!Directory.Exists(ednaDir)) continue;
                gamesNode.Children.Add(BuildGameSkeleton(gameDir, ednaDir));
            }

            return gamesNode;
        }

        // Builds {GameName} -> Content -> Mods -> ESB -> [recursive EDNA subtree].
        // The skeleton nodes are constructed without scanning siblings, so save folders
        // with large Content/Maps or Content/Playfields trees don't get enumerated.
        private static NavNode BuildGameSkeleton(string gameDir, string ednaDir)
        {
            var ednaNode = BuildFolderRecursive(ednaDir);

            var esbNode  = new NavNode { Name = "ESB",  NodeType = NavNodeType.SaveFolder, Tag = Path.Combine(gameDir, "Content", "Mods", "ESB") };
            esbNode.Children.Add(ednaNode);

            var modsNode = new NavNode { Name = "Mods", NodeType = NavNodeType.SaveFolder, Tag = Path.Combine(gameDir, "Content", "Mods") };
            modsNode.Children.Add(esbNode);

            var contentNode = new NavNode { Name = "Content", NodeType = NavNodeType.SaveFolder, Tag = Path.Combine(gameDir, "Content") };
            contentNode.Children.Add(modsNode);

            var gameNode = new NavNode { Name = Path.GetFileName(gameDir), NodeType = NavNodeType.SaveFolder, Tag = gameDir };
            gameNode.Children.Add(contentNode);

            return gameNode;
        }

        private static NavNode BuildFolderRecursive(string dir)
        {
            var node = new NavNode
            {
                Name     = Path.GetFileName(dir),
                NodeType = NavNodeType.SaveFolder,
                Tag      = dir,
            };
            foreach (var subDir in SafeDirs(dir).OrderBy(Path.GetFileName))
                node.Children.Add(BuildFolderRecursive(subDir));
            foreach (var file in SafeFiles(dir).OrderBy(Path.GetFileName))
                node.Children.Add(new NavNode
                {
                    Name     = Path.GetFileName(file),
                    NodeType = NavNodeType.SaveFile,
                    Tag      = file,
                });
            return node;
        }

        private static IEnumerable<string> SafeDirs(string dir)
        {
            try { return Directory.GetDirectories(dir); }
            catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> SafeFiles(string dir)
        {
            try { return Directory.GetFiles(dir); }
            catch { return Array.Empty<string>(); }
        }
    }
}
