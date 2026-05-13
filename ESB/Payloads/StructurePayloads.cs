namespace ESB.Payloads
{
    public class VecInt3Payload             { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } }

    public class EntityIdRequest            { public int EntityId { get; set; } }
    public class GetDevicePositionsRequest  { public int EntityId { get; set; } public string DeviceName { get; set; } }
    public class GetBlockSignalsRequest     { public int EntityId { get; set; } public string Filter     { get; set; } }
    public class GetSignalStateRequest      { public int EntityId { get; set; } public string SignalName { get; set; } }
    public class GetSignalReceiversRequest  { public int EntityId { get; set; } public string SignalName { get; set; } }
    public class AddTankContentRequest      { public int EntityId { get; set; } public string TankType   { get; set; } public float Amount { get; set; } }
    public class SetFactionRequest          { public int EntityId { get; set; } public string FactionGroup { get; set; } public int FactionEntityId { get; set; } }
    public class ScanFloorRequest           { public int EntityId { get; set; } public int Y { get; set; } }

    public class GetSendSignalNameRequest
    {
        public int            EntityId { get; set; }
        public VecInt3Payload Pos      { get; set; }
    }

    public class StructToGlobalPosRequest
    {
        public int            EntityId  { get; set; }
        public VecInt3Payload StructPos { get; set; }
    }

    public class GlobalToStructPosRequest
    {
        public int         EntityId { get; set; }
        public Vec3Payload Pos      { get; set; }
    }

    // Base for all device operations addressed by structure entity + block position.
    public class DevicePosRequest
    {
        public int EntityId { get; set; }
        public int X        { get; set; }
        public int Y        { get; set; }
        public int Z        { get; set; }
    }

    public class ColorPayload { public float R { get; set; } public float G { get; set; } public float B { get; set; } public float A { get; set; } }

    public class SetLcdTextRequest   : DevicePosRequest { public string Text     { get; set; } }
    public class SetLcdFontSizeRequest : DevicePosRequest { public int FontSize   { get; set; } }
    public class SetLcdColorsRequest : DevicePosRequest
    {
        public ColorPayload BackgroundColor { get; set; }
        public ColorPayload TextColor       { get; set; }
    }

    public class ModifyItemsRequest : DevicePosRequest { public int ItemId { get; set; } public int Count { get; set; } }

    public class SetBlockRequest : DevicePosRequest
    {
        public int?  Type     { get; set; }
        public int?  Shape    { get; set; }
        public int?  Rotation { get; set; }
        public bool? Active   { get; set; }
    }

    public class SetSwitchStateRequest : DevicePosRequest { public bool State { get; set; } public int Index { get; set; } }

    public class SetLightColorRequest     : DevicePosRequest { public ColorPayload Color { get; set; } }
    public class SetLightIntensityRequest : DevicePosRequest { public float Intensity    { get; set; } }
    public class SetLightRangeRequest     : DevicePosRequest { public float Range        { get; set; } }
    public class SetLightBlinkRequest     : DevicePosRequest { public float Interval { get; set; } public float Length { get; set; } public float Offset { get; set; } }

    public class SetTeleporterRequest : DevicePosRequest
    {
        public string TargetEntityNameOrGroup { get; set; }
        public string TargetPlayfield         { get; set; }
        public string TargetSolarSystemName   { get; set; }
        public byte?  Origin                  { get; set; }
    }
}
