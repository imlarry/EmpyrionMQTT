using System.Threading.Tasks;
using ESB.Messaging;

namespace EDNAClient.Core
{
    public interface IEdnaSkill
    {
        string Id { get; }
        Task StartAsync(IMessenger messenger);
        void Stop();
        void SnapToGameWindow();
    }
}
