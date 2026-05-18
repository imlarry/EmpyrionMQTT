using System;

namespace ESB.Messaging
{
    public enum RoutingContextKind
    {
        Broadcast,
        Machine,
        Lobby,
        Game,
    }

    // RoutingContextId names the audience for an ESB message.
    // On the wire only Id is sent (position 3 of the 6-segment topic).
    // Widths are eyeball-distinguishable: Machine=5, Game=8, Broadcast fixed "00000000".
    // Kind is an in-process aid for logging and discoverability; not transmitted.
    public struct RoutingContextId : IEquatable<RoutingContextId>
    {
        public const string BroadcastValue = "00000000";
        private const int MachineWidth = 5;
        private const int GameWidth    = 8;
        private const string Sep = "|";

        public string Id { get; }
        public RoutingContextKind Kind { get; }

        private RoutingContextId(string id, RoutingContextKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public static RoutingContextId Broadcast()
        {
            return new RoutingContextId(BroadcastValue, RoutingContextKind.Broadcast);
        }

        public static RoutingContextId Machine(string machineToken)
        {
            return new RoutingContextId(
                IdentifierHelper.GenerateIdentifier(machineToken, MachineWidth),
                RoutingContextKind.Machine);
        }

        public static RoutingContextId Game(string saveGamePath, string machineId)
        {
            return new RoutingContextId(
                IdentifierHelper.GenerateIdentifier(saveGamePath + Sep + machineId, GameWidth),
                RoutingContextKind.Game);
        }

        // Lobby names the pre-game audience for a Client/EDNA pair on a given machine.
        // Width-identical to Game so subscriptions and topics are uniform; only the in-process
        // Kind distinguishes it. Stable per machine, so Client and EDNA (which share machineId)
        // derive the same value and see each other's pre-game events.
        public static RoutingContextId Lobby(string machineId)
        {
            return new RoutingContextId(
                IdentifierHelper.GenerateIdentifier("__lobby__" + Sep + machineId, GameWidth),
                RoutingContextKind.Lobby);
        }

        public bool Equals(RoutingContextId other)
        {
            return Id == other.Id && Kind == other.Kind;
        }

        public override bool Equals(object obj)
        {
            return obj is RoutingContextId && Equals((RoutingContextId)obj);
        }

        public override int GetHashCode()
        {
            int idHash = Id != null ? Id.GetHashCode() : 0;
            return idHash ^ (int)Kind;
        }

        public override string ToString()
        {
            return Kind + ":" + Id;
        }

        public static implicit operator string(RoutingContextId rc)
        {
            return rc.Id;
        }
    }
}
