using System;

namespace ESB.Messaging
{
    public enum RoutingContextKind
    {
        Broadcast,
        Machine,
        Game,
        Playfield,
        Player,
        PlayerInGame,
    }

    // RoutingContextId names the audience for an ESB message.
    // On the wire only Id is sent (8-char base-36, position 3 of the 6-segment topic).
    // Kind is an in-process aid for logging and discoverability; not transmitted.
    public struct RoutingContextId : IEquatable<RoutingContextId>
    {
        public const string BroadcastValue = "00000000";
        private const int Width = 8;
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
                IdentifierHelper.GenerateIdentifier(machineToken, Width),
                RoutingContextKind.Machine);
        }

        public static RoutingContextId Game(string saveGamePath, string machineId)
        {
            return new RoutingContextId(
                IdentifierHelper.GenerateIdentifier(saveGamePath + Sep + machineId, Width),
                RoutingContextKind.Game);
        }

        public static RoutingContextId Playfield(string gameId, string solarSystemName, string playfieldName)
        {
            return new RoutingContextId(
                IdentifierHelper.GenerateIdentifier(gameId + Sep + solarSystemName + Sep + playfieldName, Width),
                RoutingContextKind.Playfield);
        }

        public static RoutingContextId Player(string steamId)
        {
            return new RoutingContextId(
                IdentifierHelper.GenerateIdentifier(steamId, Width),
                RoutingContextKind.Player);
        }

        // PlayerInGame uses canonical (lexicographic) ordering so both ends derive the same id
        // regardless of argument order.
        public static RoutingContextId PlayerInGame(string playerId, string gameId)
        {
            string seed = string.CompareOrdinal(playerId, gameId) <= 0
                ? playerId + Sep + gameId
                : gameId + Sep + playerId;
            return new RoutingContextId(
                IdentifierHelper.GenerateIdentifier(seed, Width),
                RoutingContextKind.PlayerInGame);
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
