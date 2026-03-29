using System.Threading.Tasks;

namespace ESB
{
    public interface IBusManager
    {
        string ApplicationName { get; }
        string ESBModPath { get; }

        Task Init();
        Task Shutdown();
    }
}