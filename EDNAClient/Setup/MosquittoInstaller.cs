using System;
using System.Threading.Tasks;

namespace EDNAClient.Setup
{
    public class MosquittoInstaller : ISetupStep
    {
        public string DisplayName => "Set Up MQTT Broker";

        public Task RunAsync()
        {
            throw new NotImplementedException("Mosquitto installer not yet implemented. See Setup/PLAN.md.");
        }
    }
}
