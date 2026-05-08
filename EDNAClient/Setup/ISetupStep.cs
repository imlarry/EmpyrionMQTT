using System.Threading.Tasks;

namespace EDNAClient.Setup
{
    public interface ISetupStep
    {
        string DisplayName { get; }
        Task RunAsync();
    }
}
