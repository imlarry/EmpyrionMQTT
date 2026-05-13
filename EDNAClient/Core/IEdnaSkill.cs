using System.Threading.Tasks;
using ESB.Messaging;

namespace EDNAClient.Core
{
    public interface IEdnaSkill
    {
        string Id { get; }
        Task StartAsync(IMessageBus bus);
        void Stop();
        void SnapToGameWindow();
    }
}
