namespace ESB.Payloads
{
    public class SetEntityPositionRequest
    {
        public int         EntityId { get; set; }
        public Vec3Payload Pos      { get; set; }
    }

    public class SetEntityRotationRequest
    {
        public int         EntityId { get; set; }
        public QuatPayload Rot      { get; set; }
    }

    public class MoveEntityRequest
    {
        public int         EntityId  { get; set; }
        public Vec3Payload Direction { get; set; }
    }

    public class MoveForwardRequest
    {
        public int   EntityId { get; set; }
        public float Speed    { get; set; }
    }

    public class DamageEntityRequest
    {
        public int EntityId     { get; set; }
        public int DamageAmount { get; set; }
        public int DamageType   { get; set; }
    }
}
