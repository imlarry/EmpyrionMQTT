using System.Threading.Tasks;

namespace ESB
{
    public interface IESBManager
    {
        string ApplicationName { get; }
        string GameName { get; }
        string GameIdentifier { get; }
        string RootPath { get; }
        string GamePath { get; }

        Task Init();
        Task Shutdown();
        Task InitDataDirectory();
        Task SetGameDirectory(bool hasEntered);
        Task CreateLocalDatabase();
    }
}