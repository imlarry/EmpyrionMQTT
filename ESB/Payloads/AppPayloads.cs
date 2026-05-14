namespace ESB.Payloads
{
    public class GameTicksResponse         { public ulong GameTicks { get; set; } }
    public class ModeResponse              { public string Mode     { get; set; } }
    public class StateResponse             { public string State    { get; set; } }

    public class ModApiPropertiesResponse
    {
        public string ClientPlayfield { get; set; }
        public string Network         { get; set; }
        public string GUI             { get; set; }
        public string PDA             { get; set; }
        public string Scripting       { get; set; }
        public string SoundPlayer     { get; set; }
        public string Application     { get; set; }
    }

    public class PlayfieldInfoResponse
    {
        public string PlayfieldName { get; set; }
        public string PlayfieldType { get; set; }
        public bool   IsInstance    { get; set; }
    }

    public class GetPathForRequest  { public string AppFolder { get; set; } }
    public class GetPathForResponse { public string AppFolder { get; set; } public string Path { get; set; } }

    public class GetPlayerDataForRequest { public int? PlayerEntityId { get; set; } }

    public class GetStructureRequest  { public int? EntityId { get; set; } }

    public class GetStructuresRequest
    {
        public string PlayfieldName { get; set; }
        public byte?  FactionId    { get; set; }
        public byte?  FactionGroup { get; set; }
        public string EntityType   { get; set; }
    }

    public class SendChatMessageRequest
    {
        public string Text               { get; set; }
        public string Channel            { get; set; }
        public string SenderType         { get; set; }
        public int?   SenderEntityId     { get; set; }
        public string SenderNameOverride { get; set; }
        public int?   RecipientEntityId  { get; set; }
        public bool?  IsTextLocaKey      { get; set; }
        public string Arg1               { get; set; }
        public string Arg2               { get; set; }
    }

    public class ShowDialogBoxRequest
    {
        public int?     PlayerEntityId    { get; set; }
        public string   TitleText         { get; set; }
        public string   BodyText          { get; set; }
        public string[] ButtonTexts       { get; set; }
        public bool?    CloseOnLinkClick  { get; set; }
        public int?     ButtonIdxForEsc   { get; set; }
        public int?     ButtonIdxForEnter { get; set; }
        public int?     MaxChars          { get; set; }
        public string   Placeholder       { get; set; }
        public string   InitialContent    { get; set; }
        public int?     CustomValue       { get; set; }
    }

    public class ChatMessageSentPayload
    {
        public ulong  GameTicks          { get; set; }
        public int    SenderEntityId     { get; set; }
        public string SenderType         { get; set; }
        public string SenderNameOverride { get; set; }
        public string SenderFaction      { get; set; }
        public int    RecipientEntityId  { get; set; }
        public string RecipientFaction   { get; set; }
        public float  GameTime           { get; set; }
        public bool   IsTextLocaKey      { get; set; }
        public string Arg1               { get; set; }
        public string Arg2               { get; set; }
        public string Channel            { get; set; }
        public string Text               { get; set; }
    }

    public class GameStatePayload
    {
        public ulong  GameTicks    { get; set; }
        public string GameName     { get; set; }
        public string GameRcId     { get; set; }
        public string SaveGamePath { get; set; }
        public string GameMode     { get; set; }
    }

    public class DialogResponsePayload
    {
        public int    PlayerEntityId { get; set; }
        public int    ButtonIdx      { get; set; }
        public string LinkId         { get; set; }
        public string InputContent   { get; set; }
        public int    CustomValue    { get; set; }
    }
}
