using System.Threading.Tasks;

namespace EDNAClient.Core
{
    public interface IEdnaSkill
    {
        string Id { get; }
        Task StartAsync(EdnaContext ctx);
        void Stop();
        void SnapToGameWindow();
    }
}
