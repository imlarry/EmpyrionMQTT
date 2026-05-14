using System.Collections.Generic;
using System.Text;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace ESBTests.Bus;

public class Test_MessageEnvelope
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static MessageContext MakeContext(
        string scope             = "Player",
        string operation         = "GameEnter",
        string msgType           = "evt",
        string participantType   = "Pfs",
        string routingContextId  = "g2w2k7v3",
        string payload           = "{}",
        byte[]? correlationData  = null)
    {
        return new MessageContext
        {
            ParsedTopic = new ParsedTopic
            {
                ParticipantType  = participantType,
                RoutingContextId = routingContextId,
                Scope            = scope,
                MsgType          = msgType,
                Operation        = operation,
                DispatchKey      = $"{scope}/{msgType}/{operation}"
            },
            Payload         = payload,
            CorrelationData = correlationData
        };
    }

    private class SamplePayload
    {
        public int    EntityId { get; set; }
        public string Name     { get; set; } = string.Empty;
    }

    // -------------------------------------------------------------------------
    // From MessageContext -- property mapping
    // -------------------------------------------------------------------------

    [Fact]
    public void FromContext_SenderType_IsParticipantType()
    {
        var env = MessageEnvelope.From(MakeContext(participantType: "Pfs"));
        Assert.Equal("Pfs", env.SenderType);
    }

    [Fact]
    public void FromContext_RoutingContextId_IsRoutingContextId()
    {
        var env = MessageEnvelope.From(MakeContext(routingContextId: "ab12cd34"));
        Assert.Equal("ab12cd34", env.RoutingContextId);
    }

    [Fact]
    public void FromContext_Scope_Operation_MsgType_AllPopulated()
    {
        var env = MessageEnvelope.From(MakeContext(
            scope: "Structure", operation: "FuelChanged", msgType: "evt"));

        Assert.Equal("Structure",   env.Scope);
        Assert.Equal("FuelChanged", env.Operation);
        Assert.Equal("evt",         env.MsgType);
    }

    [Fact]
    public void FromContext_RawPayload_MatchesContextPayload()
    {
        var env = MessageEnvelope.From(MakeContext(payload: "{\"EntityId\":7}"));
        Assert.Equal("{\"EntityId\":7}", env.RawPayload);
    }

    // -------------------------------------------------------------------------
    // CorrelationId
    // -------------------------------------------------------------------------

    [Fact]
    public void FromContext_CorrelationId_DecodesUtf8Bytes()
    {
        var env = MessageEnvelope.From(
            MakeContext(correlationData: Encoding.UTF8.GetBytes("a1b2c3d4")));
        Assert.Equal("a1b2c3d4", env.CorrelationId);
    }

    [Fact]
    public void FromContext_NullCorrelationData_CorrelationIdIsEmpty()
    {
        var env = MessageEnvelope.From(MakeContext(correlationData: null));
        Assert.Equal(string.Empty, env.CorrelationId);
    }

    [Fact]
    public void FromContext_EmptyCorrelationData_CorrelationIdIsEmpty()
    {
        var env = MessageEnvelope.From(MakeContext(correlationData: new byte[0]));
        Assert.Equal(string.Empty, env.CorrelationId);
    }

    // -------------------------------------------------------------------------
    // From raw string (response path)
    // -------------------------------------------------------------------------

    [Fact]
    public void FromRaw_Scope_Operation_AreSet()
    {
        var env = new MessageEnvelope("{}", "Player", "GetInfo");
        Assert.Equal("Player",  env.Scope);
        Assert.Equal("GetInfo", env.Operation);
    }

    [Fact]
    public void FromRaw_MsgType_IsRes()
    {
        var env = new MessageEnvelope("{}", "Player", "GetInfo");
        Assert.Equal("res", env.MsgType);
    }

    [Fact]
    public void FromRaw_SenderFields_AreEmpty()
    {
        var env = new MessageEnvelope("{}", "Player", "GetInfo");
        Assert.Equal(string.Empty, env.SenderType);
        Assert.Equal(string.Empty, env.RoutingContextId);
        Assert.Equal(string.Empty, env.CorrelationId);
    }

    // -------------------------------------------------------------------------
    // PayloadJson
    // -------------------------------------------------------------------------

    [Fact]
    public void PayloadJson_ValidJson_ReturnsParsedObject()
    {
        var env = MessageEnvelope.From(MakeContext(payload: "{\"EntityId\":42}"));
        Assert.NotNull(env.PayloadJson);
        Assert.Equal(42, (int)env.PayloadJson!["EntityId"]!);
    }

    [Fact]
    public void PayloadJson_EmptyPayload_ReturnsNull()
    {
        var env = MessageEnvelope.From(MakeContext(payload: ""));
        Assert.Null(env.PayloadJson);
    }

    [Fact]
    public void PayloadJson_NotJson_ReturnsNull()
    {
        var env = MessageEnvelope.From(MakeContext(payload: "not json"));
        Assert.Null(env.PayloadJson);
    }

    [Fact]
    public void PayloadJson_CalledTwice_ReturnsSameInstance()
    {
        var env = MessageEnvelope.From(MakeContext(payload: "{\"X\":1}"));
        var first  = env.PayloadJson;
        var second = env.PayloadJson;
        Assert.Same(first, second);
    }

    // -------------------------------------------------------------------------
    // PayloadAs<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void PayloadAs_ValidJson_DeserializesToType()
    {
        var env = MessageEnvelope.From(
            MakeContext(payload: "{\"EntityId\":7,\"Name\":\"Alice\"}"));
        var result = env.PayloadAs<SamplePayload>();
        Assert.Equal(7,       result!.EntityId);
        Assert.Equal("Alice", result.Name);
    }

    [Fact]
    public void PayloadAs_EmptyPayload_ReturnsDefault()
    {
        var env = MessageEnvelope.From(MakeContext(payload: ""));
        var result = env.PayloadAs<SamplePayload>();
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // MessageEnvelope<T> -- typed variant
    // -------------------------------------------------------------------------

    [Fact]
    public void TypedEnvelope_FromContext_Body_IsDeserializedAtConstruction()
    {
        var ctx = MakeContext(payload: "{\"EntityId\":99,\"Name\":\"Bob\"}");
        var env = MessageEnvelope<SamplePayload>.From(ctx);
        Assert.Equal(99,    env.Body.EntityId);
        Assert.Equal("Bob", env.Body.Name);
    }

    [Fact]
    public void TypedEnvelope_FromContext_BaseProperties_Populated()
    {
        var ctx = MakeContext(scope: "Player", operation: "GameEnter");
        var env = MessageEnvelope<SamplePayload>.From(ctx);
        Assert.Equal("Player",    env.Scope);
        Assert.Equal("GameEnter", env.Operation);
    }

    [Fact]
    public void TypedEnvelope_FromRaw_Body_IsDeserialized()
    {
        var env = new MessageEnvelope<SamplePayload>(
            "{\"EntityId\":5,\"Name\":\"Carol\"}", "Player", "GetInfo");
        Assert.Equal(5,       env.Body.EntityId);
        Assert.Equal("Carol", env.Body.Name);
    }
}
