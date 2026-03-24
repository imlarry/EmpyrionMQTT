using ESB.Messaging;

namespace EDNAClient.Core
{
    // EdnaContext is EDNA's concrete implementation of BaseContextData.
    // BaseContextData provides the Messenger instance that owns the MQTT connection.
    public class EdnaContext : BaseContextData
    {
        // The MQTT SourceId of the ESB instance that owns authoritative game state.
        // Set on Application.GameEnter: "Client" for SinglePlayer, "DedicatedServer" for MP.
        // Skills send requests to {AuthoritativeSource}/Q/{Subject}/... without knowing
        // which API version (V1/V2) answers.
        public string? AuthoritativeSource { get; set; }
    }
}
