namespace ESB.Payloads
{
    public class DamageEntityRequest
    {
        public int DamageAmount { get; set; }
        public int DamageType   { get; set; }
    }

    public class TeleportRequest
    {
        public string      Playfield { get; set; }
        public Vec3Payload Pos       { get; set; }
        public Vec3Payload Rot       { get; set; }
    }
}
