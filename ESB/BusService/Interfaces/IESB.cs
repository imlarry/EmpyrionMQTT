using Eleon.Modding;

namespace ESB
{
    public interface IEmpyrionServiceBus
    {
        void Initialize(ModGameAPI dediAPI);    // is there a dediAPI analog for Shutdown()?
        void Init(IModApi modApi);
        void Shutdown();
    }
}