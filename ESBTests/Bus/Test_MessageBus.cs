using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ESB.Messaging;
using MQTTnet.Client;

namespace ESBTests.Bus;

public class Test_MessageBus
{
    // Recording stub -- captures PublishRetainedAsync, SendAsync, and RequestToAsync args.
    private sealed class RecordingMessenger : IMessenger
    {
        public int CompressionThreshold { get; set; } = 2048;

        public string       LastRetainedRcId      { get; private set; }
        public string       LastRetainedScope     { get; private set; }
        public MessageType? LastRetainedType      { get; private set; }
        public string       LastRetainedOperation { get; private set; }
        public string       LastRetainedPayload   { get; private set; }
        public uint         LastRetainedExpiry    { get; private set; }

        public string       LastSendRcId       { get; private set; }
        public string       LastSendScope      { get; private set; }
        public MessageType? LastSendType       { get; private set; }
        public string       LastSendOperation  { get; private set; }
        public string       LastSendPayload    { get; private set; }

        public string LastRequestToParticipantType { get; private set; }
        public string LastRequestToRcId            { get; private set; }

        public string LastSubscribedRcId   { get; private set; }
        public string LastUnsubscribedRcId { get; private set; }

        public string RequestToResponse { get; set; } = "{}";

        public readonly Dictionary<string, Func<MessageContext, Task>> Handlers
            = new Dictionary<string, Func<MessageContext, Task>>();

        public void RegisterHandler(string dispatchKey, Func<MessageContext, Task> handler)
            => Handlers[dispatchKey] = handler;

        public string MachineId()       => "stub0001";
        public string ParticipantType() => "Test";
        public string AvailableTopics() => string.Join(", ", Handlers.Keys);

        public MqttClientOptions CreateMqttClientOptions(
            string withTcpServer = "localhost", int port = 0,
            string username = null, string password = null,
            string caFilePath = null, string willTopic = null)
            => throw new NotImplementedException();

        public Task ConnectAsync(string participantType,
            string withTcpServer = "localhost", int port = 1883,
            string username = null, string password = null, string caFilePath = null)
            => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task SubscribeBrokerAsync(
            string participantType = null, string routingContextId = null,
            string scope = null, MessageType? msgType = null, string operation = null)
        {
            LastSubscribedRcId = routingContextId;
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(
            string participantType = null, string routingContextId = null,
            string scope = null, MessageType? msgType = null, string operation = null)
        {
            LastUnsubscribedRcId = routingContextId;
            return Task.CompletedTask;
        }

        public Task ReplyAsync(string responseTopic, byte[] correlationData, string payload)
            => Task.CompletedTask;

        public Task PublishRetainedAsync(
            string routingContextId, string scope, MessageType msgType, string operation,
            string payload, uint expirySeconds = 0u, bool compress = false)
        {
            LastRetainedRcId      = routingContextId;
            LastRetainedScope     = scope;
            LastRetainedType      = msgType;
            LastRetainedOperation = operation;
            LastRetainedPayload   = payload;
            LastRetainedExpiry    = expirySeconds;
            return Task.CompletedTask;
        }

        public Task SendAsync(string routingContextId, string scope, MessageType msgType, string operation,
            string payload, List<KeyValuePair<string, string>> userProperties = null,
            bool compress = false)
        {
            LastSendRcId      = routingContextId;
            LastSendScope     = scope;
            LastSendType      = msgType;
            LastSendOperation = operation;
            LastSendPayload   = payload;
            return Task.CompletedTask;
        }

        public Task<string> RequestAsync(string routingContextId, string scope, string operation,
            string payload, TimeSpan timeout)
            => Task.FromResult("{}");

        public Task<string> RequestToAsync(
            string targetParticipantType, string targetRoutingContextId,
            string scope, string operation, string payload, TimeSpan timeout)
        {
            LastRequestToParticipantType = targetParticipantType;
            LastRequestToRcId            = targetRoutingContextId;
            return Task.FromResult(RequestToResponse);
        }
    }

    private static (IMessageBus bus, RecordingMessenger stub) MakeBus()
    {
        var stub = new RecordingMessenger();
        var bus = new BusBuilder()
            .WithMessenger(stub)
            .WithParticipantType("Test")
            .Build();
        return (bus, stub);
    }

    // -------------------------------------------------------------------------
    // AnnounceAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AnnounceAsync_PublishesToAnnouncementsScope()
    {
        var (bus, stub) = MakeBus();
        await bus.AnnounceAsync(RoutingContextId.BroadcastValue, "Ready", new { Status = "up" });
        Assert.Equal("Announcements", stub.LastRetainedScope);
    }

    [Fact]
    public async Task AnnounceAsync_MsgTypeIsEvt()
    {
        var (bus, stub) = MakeBus();
        await bus.AnnounceAsync(RoutingContextId.BroadcastValue, "Ready", new { Status = "up" });
        Assert.Equal(MessageType.Evt, stub.LastRetainedType);
    }

    [Fact]
    public async Task AnnounceAsync_OperationIsForwarded()
    {
        var (bus, stub) = MakeBus();
        await bus.AnnounceAsync(RoutingContextId.BroadcastValue, "Ready", new { });
        Assert.Equal("Ready", stub.LastRetainedOperation);
    }

    [Fact]
    public async Task AnnounceAsync_RoutingContextIdIsForwarded()
    {
        var (bus, stub) = MakeBus();
        await bus.AnnounceAsync("a1b2c3d4", "Ready", new { });
        Assert.Equal("a1b2c3d4", stub.LastRetainedRcId);
    }

    [Fact]
    public async Task AnnounceAsync_PayloadIsSerializedJson()
    {
        var (bus, stub) = MakeBus();
        await bus.AnnounceAsync(RoutingContextId.BroadcastValue, "Ready", new { Status = "up" });
        Assert.Contains("Status", stub.LastRetainedPayload);
        Assert.Contains("up",     stub.LastRetainedPayload);
    }

    [Fact]
    public async Task AnnounceAsync_ExpirySecondsIsForwarded()
    {
        var (bus, stub) = MakeBus();
        await bus.AnnounceAsync(RoutingContextId.BroadcastValue, "Ready", new { }, expirySeconds: 300u);
        Assert.Equal(300u, stub.LastRetainedExpiry);
    }

    // -------------------------------------------------------------------------
    // LogAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LogAsync_MsgTypeIsLog()
    {
        var (bus, stub) = MakeBus();
        await bus.LogAsync(stub.MachineId(), "App", "ConnectAsync", "{}");
        Assert.Equal(MessageType.Log, stub.LastSendType);
    }

    [Fact]
    public async Task LogAsync_NormalizesScope()
    {
        var (bus, stub) = MakeBus();
        await bus.LogAsync(stub.MachineId(), "app", "ConnectAsync", "{}");
        Assert.Equal("App", stub.LastSendScope);
    }

    [Fact]
    public async Task LogAsync_OperationAndPayloadForwarded()
    {
        var (bus, stub) = MakeBus();
        await bus.LogAsync(stub.MachineId(), "App", "ConnectAsync", "{\"ms\":12}");
        Assert.Equal("ConnectAsync", stub.LastSendOperation);
        Assert.Equal("{\"ms\":12}",  stub.LastSendPayload);
    }

    [Fact]
    public async Task LogAsync_RoutingContextIdIsForwarded()
    {
        var (bus, stub) = MakeBus();
        await bus.LogAsync("xyz12345", "App", "ConnectAsync", "{}");
        Assert.Equal("xyz12345", stub.LastSendRcId);
    }

    // -------------------------------------------------------------------------
    // SubscribeAsync / UnsubscribeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SubscribeAsync_BeforeConnect_DefersBrokerCall()
    {
        var (bus, stub) = MakeBus();
        await bus.SubscribeAsync("game1234");
        // Not connected yet -- broker subscribe should not have fired.
        Assert.Null(stub.LastSubscribedRcId);
    }

    [Fact]
    public async Task UnsubscribeAsync_EmptyRcId_IsNoOp()
    {
        var (bus, stub) = MakeBus();
        await bus.UnsubscribeAsync("");
        Assert.Null(stub.LastUnsubscribedRcId);
    }

    // -------------------------------------------------------------------------
    // OnBroadcastRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void OnBroadcastRequest_RegistersHandlerWithEvtDispatchKey()
    {
        var (bus, stub) = MakeBus();
        bus.OnBroadcastRequest("Pfs", "Player", "GameEnter", _ => Task.CompletedTask);
        Assert.True(stub.Handlers.ContainsKey("Player/evt/GameEnter"));
    }

    [Fact]
    public void OnBroadcastRequest_NormalizesScope()
    {
        var (bus, stub) = MakeBus();
        bus.OnBroadcastRequest("Pfs", "player", "GameEnter", _ => Task.CompletedTask);
        Assert.True(stub.Handlers.ContainsKey("Player/evt/GameEnter"));
    }
}
