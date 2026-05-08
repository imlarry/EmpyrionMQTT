using System;
using System.Threading.Tasks;

namespace EDNAClient.Setup
{
    public class EsbInstaller : ISetupStep
    {
        public string DisplayName => "Install ESB Mod";

        public Task RunAsync()
        {
            throw new NotImplementedException("ESB installer not yet implemented. See Setup/PLAN.md.");
        }
    }
}
