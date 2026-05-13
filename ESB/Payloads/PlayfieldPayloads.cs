using System.Collections.Generic;

namespace ESB.Payloads
{
    public class EntityUnloadedPayload
    {
        public ulong  GameTicks { get; set; }
        public int    Id        { get; set; }
        public string Name      { get; set; }
    }

    public class PlayfieldEntitySnapshot
    {
        public int    Id       { get; set; }
        public string Name     { get; set; }
        public string Type     { get; set; }
        public string Position { get; set; }
    }

    public class PlayfieldLoadedPayload
    {
        public ulong                         GameTicks              { get; set; }
        public string                        Name                   { get; set; }
        public string                        PlayfieldType          { get; set; }
        public string                        PlanetType             { get; set; }
        public string                        PlanetClass            { get; set; }
        public string                        SolarSystemName        { get; set; }
        public Vec3Payload                   SolarSystemCoordinates { get; set; }
        public bool                          IsPvP                  { get; set; }
        public List<PlayfieldEntitySnapshot> Entities               { get; set; }
    }

    public class PlayfieldUnloadingPayload
    {
        public ulong  GameTicks { get; set; }
        public string Name      { get; set; }
    }

    public class QuatPayload { public float X { get; set; } public float Y { get; set; } public float Z { get; set; } public float W { get; set; } }

    public class GetTerrainHeightRequest { public float X { get; set; } public float Z { get; set; } }

    public class SpawnEntityRequest
    {
        public string      EntityType { get; set; }
        public Vec3Payload Pos        { get; set; }
        public QuatPayload Rot        { get; set; }
    }
}
