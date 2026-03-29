using System.Collections.Generic;
using System.Threading.Tasks;

namespace ESB
{
    public interface IGameManager
    {
        string GameName { get; }
        string GameIdentifier { get; }
        string GameDataPath { get; }
        string SaveGamePath { get; }
        string GameMode { get; }
        Dictionary<int, string> BlockAndItemMapping { get; }

        Task Init();
        Task StateChanged(bool hasEntered);
    }
}
