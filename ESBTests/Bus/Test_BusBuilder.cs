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

        public string MachineId()       => "stub0001";
        public string ParticipantType() => "Test";
        public string AvailableTopics() => string.Join(", ", Handlers.Keys);

        public MqttClientOptions CreateMqttClientOptions(
            string withTcpServer = "localhost", int port = 0, string? username = null,
            string? password = null, string? caFilePath = null, string? willTopic = null)
            => throw new NotImplementedException();

        public Task ConnectAsync(string participantType,
            string withTcpServer = "localhost", int port = 1883,
            string? username = null, string? password = null, string? caFilePath = null)
            => throw new NotImplementedException();

        public Task DisconnectAsync() => throw new NotImplementedException();

        public Task SubscribeBrokerAsync(
            string? participantType = null, string? routingContextId = null,
            string? scope = null, MessageType? msgType = null,
            string? operation = null)
            => Task.CompletedTask;

        public Task UnsubscribeAsync(
            string? participantType = null, string? routingContextId = null,
            string? scope = null, MessageType? msgType = null, string? operation = null)
            => throw new NotImplementedException();

        public Task ReplyAsync(string responseTopic, byte[] correlationData, string payload)
            => throw new NotImplementedException();

        public Task PublishRetainedAsync(
            string routingContextId, string scope, MessageType msgType, string operation,
            string payload, uint expirySeconds = 0u, bool compress = false)
            => throw new NotImplementedException();

        public Task SendAsync(string routingContextId, string scope, MessageType msgType, string operation,
            string payload, List<KeyValuePair<string, string>>? userProperties = null,
            bool compress = false)
            => throw new NotImplementedException();

        public Task<string> RequestAsync(string routingContextId, string scope, string operation,
            string payload, TimeSpan timeout)
            => throw new NotImplementedException();

        public Task<string> RequestToAsync(string targetParticipantType, string targetRoutingContextId,
            string scope, string operation, string payload, TimeSpan timeout)
            => throw new NotImplementedException();
    }

    private BusBuilder MakeBuilder() =>
        new BusBuilder().WithMessenger(new StubMessenger()).WithParticipantType("Test");

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
