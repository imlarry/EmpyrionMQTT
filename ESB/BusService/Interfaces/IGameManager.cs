using System.Threading.Tasks;

namespace ESB
{
    public interface IGameManager
    {
        string GameName { get; }
        string GameIdentifier { get; }
        string GamePath { get; }

        Task Init();
        void SetGameDirectory();
        Task CreateLocalDatabase();
    }
}
