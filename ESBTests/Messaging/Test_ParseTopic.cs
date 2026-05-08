using ESB.Messaging;

namespace ESBTests.Messaging;

public class Test_ParseTopic
{
    private static ParsedTopic Parse(string topic) => new Messenger().ParseTopic(topic);

    // -------------------------------------------------------------------------
    // All six fields extracted correctly
    // -------------------------------------------------------------------------
    [Fact]
    public void ParseTopic_Evt_AllFieldsCorrect()
    {
        var pt = Parse("ESB/Client/a1b2/Game/evt/GameEntered");

        Assert.Equal("Client",       pt.ParticipantType);
        Assert.Equal("a1b2",         pt.ConnectionId);
        Assert.Equal("Game",         pt.Scope);
        Assert.Equal("evt",          pt.MsgType);
        Assert.Equal("GameEntered",  pt.Operation);
        Assert.Null(pt.MetaOperation);
    }

    // -------------------------------------------------------------------------
    // DispatchKey includes msgType for all four values
    // -------------------------------------------------------------------------
    [Fact]
    public void ParseTopic_Req_DispatchKeyIncludesMsgType()
    {
        var pt = Parse("ESB/Edna/p32r/Tracking/req/Enable");
        Assert.Equal("Tracking/req/Enable", pt.DispatchKey);
    }

    [Fact]
    public void ParseTopic_Res_DispatchKeyIncludesMsgType()
    {
        var pt = Parse("ESB/Edna/p32r/Tracking/res/Enable");
        Assert.Equal("Tracking/res/Enable", pt.DispatchKey);
    }

    [Fact]
    public void ParseTopic_Evt_DispatchKeyIncludesMsgType()
    {
        var pt = Parse("ESB/Client/a1b2/Game/evt/GameEntered");
        Assert.Equal("Game/evt/GameEntered", pt.DispatchKey);
    }

    [Fact]
    public void ParseTopic_Log_DispatchKeyIncludesMsgType()
    {
        var pt = Parse("ESB/Client/a1b2/App/log/ConnectAsync");
        Assert.Equal("App/log/ConnectAsync", pt.DispatchKey);
    }

    // -------------------------------------------------------------------------
    // MsgType is preserved exactly as it appears in the topic (lowercase)
    // -------------------------------------------------------------------------
    [Fact]
    public void ParseTopic_MsgType_IsPreservedAsLowercase()
    {
        var pt = Parse("ESB/Client/a1b2/Game/evt/GameEntered");
        Assert.Equal("evt", pt.MsgType);
    }

    // -------------------------------------------------------------------------
    // Dot-suffix handling (MetaOperation)
    // -------------------------------------------------------------------------
    [Fact]
    public void ParseTopic_DotSuffix_SetsMetaOperation()
    {
        var pt = Parse("ESB/Client/a1b2/App/log/GetPathFor.Describe");
        Assert.Equal("GetPathFor", pt.Operation);
        Assert.Equal("Describe",   pt.MetaOperation);
    }

    [Fact]
    public void ParseTopic_DotSuffix_DispatchKeyUsesBaseOp()
    {
        var pt = Parse("ESB/Client/a1b2/App/log/GetPathFor.Describe");
        Assert.Equal("App/log/GetPathFor", pt.DispatchKey);
    }

    [Fact]
    public void ParseTopic_NoDotSuffix_MetaOperationNull()
    {
        var pt = Parse("ESB/Client/a1b2/Game/evt/GameEntered");
        Assert.Null(pt.MetaOperation);
    }
}
