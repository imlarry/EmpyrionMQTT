using System.Threading.Tasks;

namespace ESB
{
    public interface IGameManager
    {
        string GameName { get; }
        string GameIdentifier { get; }
        string GamePath { get; }
        string GameMode { get; }

        Task Init();
        Task StateChanged(bool hasEntered);
    }
}
