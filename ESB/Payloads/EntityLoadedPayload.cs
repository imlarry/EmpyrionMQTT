namespace ESB.Payloads
{
    public class Vec3Payload
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class EntityLoadedPayload
    {
        public ulong       GameTicks { get; set; }
        public int         Id        { get; set; }
        public string      Name      { get; set; }
        public string      Faction   { get; set; }
        public Vec3Payload Position  { get; set; }
        public bool        IsLocal   { get; set; }
        public bool        IsProxy   { get; set; }
        public bool        IsPoi     { get; set; }
        public int         BelongsTo { get; set; }
        public int         DockedTo  { get; set; }
        public string      Type      { get; set; }
    }
}
