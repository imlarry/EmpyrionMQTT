using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ESB.Messaging;
using MQTTnet.Client;

namespace ESBTests.Bus;

public class Test_BusBuilder
{
    // -------------------------------------------------------------------------
    // Stub messenger -- records RegisterHandler calls; no-ops everything else
    // -------------------------------------------------------------------------

    private class StubMessenger : IMessenger
    {
        public int CompressionThreshold { get; set; } = 2048;
        public Dictionary<string, Func<MessageContext, Task>> Handlers { get; }
            = new Dictionary<string, Func<MessageContext, Task>>();

        public void RegisterHandler(string dispatchKey, Func<MessageContext, Task> handler)
            => Handlers[dispatchKey] = handler;

        public string MachineId()      => "stub";
        public string ClientId()       => "stub";
        public string ParticipantType() => "Test";
        public string AvailableTopics() => string.Join(", ", Handlers.Keys);

        public MqttClientOptions CreateMqttClientOptions(
            string withTcpServer = "localhost", int port = 0, string? username = null,
            string? password = null, string? caFilePath = null, string? willTopic = null)
            => throw new NotImplementedException();

        public Task ConnectAsync(BaseContextData ctx, string participantType,
            string withTcpServer = "localhost", int port = 1883,
            string? username = null, string? password = null, string? caFilePath = null)
            => throw new NotImplementedException();

        public Task DisconnectAsync()   => throw new NotImplementedException();
        public Task SubscribeBrokerAsync(
            string? participantType = null, string? connectionId = null,
            string? scope = null, MessageType? msgType = null,
            string? operation = null, Func<string, string, Task>? callback = null)
            => Task.CompletedTask;
        public Task SubscribeEventAsync(string topicFilter,
            Func<string, string, Task> callback)
            => throw new NotImplementedException();
        public Task UnsubscribeAsync(
            string? participantType = null, string? connectionId = null,
            string? scope = null, MessageType? msgType = null, string? operation = null)
            => throw new NotImplementedException();
        public Task ReplyAsync(string responseTopic, byte[] correlationData, string payload)
            => throw new NotImplementedException();
        public Task PublishRetainedAsync(
            string scope, MessageType msgType, string operation, string payload,
            uint expirySeconds = 0u, string? connectionId = null, bool compress = false)
            => throw new NotImplementedException();
        public Task SendAsync(string scope, MessageType msgType, string operation,
            string payload, List<KeyValuePair<string, string>>? userProperties = null,
            bool compress = false)
            => throw new NotImplementedException();
        public Task<string> RequestAsync(string scope, string operation,
            string payload, TimeSpan timeout)
            => throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // Test handler types
    // -------------------------------------------------------------------------

    private class PingPayload  { public int Value { get; set; } }
    private class EchoRequest  { public string Data { get; set; } = string.Empty; }
    private class EchoResponse { public string Data { get; set; } = string.Empty; }

    [BusRoute("Player", "Ping")]
    private class PingEventHandler : IEventHandler<PingPayload>
    {
        public Task HandleAsync(MessageEnvelope<PingPayload> envelope)
            => Task.CompletedTask;
    }

    [BusRoute("Player", "Echo")]
    private class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> HandleAsync(MessageEnvelope<EchoRequest> envelope)
            => Task.FromResult(new EchoResponse { Data = envelope.Body.Data });
    }

    private class UndecoratedEventHandler : IEventHandler<PingPayload>
    {
        public Task HandleAsync(MessageEnvelope<PingPayload> envelope)
            => Task.CompletedTask;
    }

    private BusBuilder MakeBuilder() =>
        new BusBuilder().WithMessenger(new StubMessenger()).WithParticipantType("Test");

    // -------------------------------------------------------------------------
    // BusScope enum
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(BusScope.App,       "App")]
    [InlineData(BusScope.Playfield, "Playfield")]
    [InlineData(BusScope.Entity,    "Entity")]
    [InlineData(BusScope.Chat,      "Chat")]
    [InlineData(BusScope.Player,    "Player")]
    [InlineData(BusScope.Structure, "Structure")]
    [InlineData(BusScope.Device,    "Device")]
    [InlineData(BusScope.Registry,  "Registry")]
    public void BusScope_ToString_MatchesPascalCaseTopicSegment(BusScope scope, string expected)
        => Assert.Equal(expected, scope.ToString());

    // -------------------------------------------------------------------------
    // NormalizeScope
    // -------------------------------------------------------------------------

    [Fact]
    public void NormalizeScope_LowercaseInput_CapitalizesFirstChar()
        => Assert.Equal("Player", BusBuilder.NormalizeScope("player"));

    [Fact]
    public void NormalizeScope_AlreadyUppercase_Unchanged()
        => Assert.Equal("Player", BusBuilder.NormalizeScope("Player"));

    [Fact]
    public void NormalizeScope_SingleChar_Capitalized()
        => Assert.Equal("P", BusBuilder.NormalizeScope("p"));

    [Fact]
    public void NormalizeScope_EmptyString_ReturnsEmpty()
        => Assert.Equal(string.Empty, BusBuilder.NormalizeScope(string.Empty));

    // -------------------------------------------------------------------------
    // Build() validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_WithoutMessenger_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new BusBuilder().WithParticipantType("Test").Build());
        Assert.Contains("WithMessenger", ex.Message);
    }

    // -------------------------------------------------------------------------
    // ScanAssembly -- event handler discovery
    // -------------------------------------------------------------------------

    [Fact]
    public void ScanAssembly_EventHandler_RegistersEvtDispatchKey()
    {
        var stub = new StubMessenger();
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .AddHandler(typeof(PingEventHandler))
            .Build();

        Assert.True(stub.Handlers.ContainsKey("Player/evt/Ping"),
            $"Expected 'Player/evt/Ping'. Keys: {stub.AvailableTopics()}");
    }

    [Fact]
    public void ScanAssembly_RequestHandler_RegistersReqDispatchKey()
    {
        var stub = new StubMessenger();
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .AddHandler(typeof(EchoRequestHandler))
            .Build();

        Assert.True(stub.Handlers.ContainsKey("Player/req/Echo"),
            $"Expected 'Player/req/Echo'. Keys: {stub.AvailableTopics()}");
    }

    [Fact]
    public void ScanAssembly_FindsBothHandlersInAssembly()
    {
        var stub = new StubMessenger();
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .ScanAssembly(typeof(PingEventHandler).Assembly)
            .Build();

        Assert.True(stub.Handlers.ContainsKey("Player/evt/Ping"));
        Assert.True(stub.Handlers.ContainsKey("Player/req/Echo"));
    }

    [Fact]
    public void ScanAssembly_TypeWithoutBusRoute_NotRegistered()
    {
        var stub = new StubMessenger();
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .ScanAssembly(typeof(UndecoratedEventHandler).Assembly)
            .Build();

        Assert.DoesNotContain(stub.Handlers.Keys, k => k.Contains("Undecorated"));
    }

    [Fact]
    public void AddHandler_MissingBusRoute_ThrowsOnBuild()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new BusBuilder()
                .WithMessenger(new StubMessenger())
                .WithParticipantType("Test")
                .AddHandler(typeof(UndecoratedEventHandler))
                .Build());

        Assert.Contains("BusRoute", ex.Message);
    }

    // -------------------------------------------------------------------------
    // Scope normalization during registration
    // -------------------------------------------------------------------------

    [Fact]
    public void ScanAssembly_LowercaseScopeInAttribute_NormalizedToUppercase()
    {
        // Handler decorated with lowercase scope "player" should produce "Player/evt/..."
        var stub = new StubMessenger();
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .AddHandler(typeof(LowercaseScopeHandler))
            .Build();

        Assert.True(stub.Handlers.ContainsKey("Player/evt/Test"),
            $"Keys: {stub.AvailableTopics()}");
    }

    [BusRoute("player", "Test")]
    private class LowercaseScopeHandler : IEventHandler<PingPayload>
    {
        public Task HandleAsync(MessageEnvelope<PingPayload> envelope)
            => Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Lambda handlers registered via builder methods
    // -------------------------------------------------------------------------

    [Fact]
    public void BuilderOnEvent_RegistersEvtDispatchKey()
    {
        var stub = new StubMessenger();
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .OnEvent<PingPayload>("App", "Heartbeat", _ => Task.CompletedTask)
            .Build();

        Assert.True(stub.Handlers.ContainsKey("App/evt/Heartbeat"),
            $"Keys: {stub.AvailableTopics()}");
    }

    [Fact]
    public void BuilderOnRequest_RegistersReqDispatchKey()
    {
        var stub = new StubMessenger();
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .OnRequest<EchoRequest, EchoResponse>("App", "Echo",
                env => Task.FromResult(new EchoResponse { Data = env.Body.Data }))
            .Build();

        Assert.True(stub.Handlers.ContainsKey("App/req/Echo"),
            $"Keys: {stub.AvailableTopics()}");
    }

    [Fact]
    public void BuilderOnEvent_Untyped_RegistersEvtDispatchKey()
    {
        var stub = new StubMessenger();
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .OnEvent("App", "Status", _ => Task.CompletedTask)
            .Build();

        Assert.True(stub.Handlers.ContainsKey("App/evt/Status"),
            $"Keys: {stub.AvailableTopics()}");
    }

    // -------------------------------------------------------------------------
    // WithCompressionThreshold
    // -------------------------------------------------------------------------

    [Fact]
    public void WithCompressionThreshold_SetsMessengerProperty()
    {
        var stub = new StubMessenger { CompressionThreshold = 2048 };
        new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .WithCompressionThreshold(512)
            .Build();

        Assert.Equal(512, stub.CompressionThreshold);
    }
}
