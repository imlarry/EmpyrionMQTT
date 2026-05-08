using System;
using System.Linq;
using ESB.Messaging;

namespace ESBTests.Messaging;

public class Test_MessageType
{
    // -------------------------------------------------------------------------
    // Enum membership
    // -------------------------------------------------------------------------
    [Fact]
    public void MessageType_HasExactlyFourValues()
    {
        Assert.Equal(4, Enum.GetValues(typeof(MessageType)).Length);
    }

    [Fact]
    public void MessageType_DoesNotContainErr()
    {
        Assert.DoesNotContain("Err", Enum.GetNames(typeof(MessageType)));
    }

    // -------------------------------------------------------------------------
    // Lowercase string representations used in topic segments
    // -------------------------------------------------------------------------
    [Fact]
    public void MessageType_Req_ToLowerIsReq()
    {
        Assert.Equal("req", MessageType.Req.ToString().ToLower());
    }

    [Fact]
    public void MessageType_Res_ToLowerIsRes()
    {
        Assert.Equal("res", MessageType.Res.ToString().ToLower());
    }

    [Fact]
    public void MessageType_Evt_ToLowerIsEvt()
    {
        Assert.Equal("evt", MessageType.Evt.ToString().ToLower());
    }

    [Fact]
    public void MessageType_Log_ToLowerIsLog()
    {
        Assert.Equal("log", MessageType.Log.ToString().ToLower());
    }
}
