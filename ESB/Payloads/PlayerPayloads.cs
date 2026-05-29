namespace ESB.Payloads
{
    public class TeleportRequest
    {
        public string      Playfield { get; set; }
        public Vec3Payload Pos       { get; set; }
        public Vec3Payload Rot       { get; set; }
    }
}
