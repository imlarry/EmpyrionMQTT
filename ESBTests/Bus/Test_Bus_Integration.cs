using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ESB.Configuration;
using ESB.Helpers;
using ESB.Messaging;

namespace ESBTests.Bus;

/// <summary>
/// Integration tests for IMessageBus against a real MQTT broker.
/// Requires broker running with credentials from ESB_Info.yaml.
/// Run with: dotnet test --filter "Category=Integration"
///       or: dotnet test --filter "FullyQualifiedName~ESBTests.Bus"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Bus_Integration
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<IMessageBus> ConnectBusAsync(
        string participantType, Action<IMessageBus>? configure = null)
    {
        var configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "ESB_Info.yaml");
        var cfg = YamlFileReader.ReadYamlFile<ESBConfig>(configPath).MQTThost;

        var messenger = new Messenger();
        var builder = new BusBuilder()
            .WithMessenger(messenger)
            .WithParticipantType(participantType)
            .WithConnection(cfg.WithTcpServer, cfg.Port);

        if (!string.IsNullOrEmpty(cfg.Username))
            builder = builder.WithCredentials(cfg.Username, cfg.Password);

        var bus = builder.Build();
        configure?.Invoke(bus);  // register handlers on IMessageBus before connecting
        await bus.ConnectAsync();
        return bus;
    }

    private static Task<T> AwaitOrTimeout<T>(TaskCompletionSource<T> tcs, int ms = 3000)
    {
        var cts = new CancellationTokenSource(ms);
        cts.Token.Register(() =>
            tcs.TrySetException(new TimeoutException($"No result within {ms}ms")));
        return tcs.Task;
    }

    private static async Task Disconnect(IMessageBus? bus)
    {
        if (bus != null) try { await bus.DisconnectAsync(); } catch { }
    }

    // -------------------------------------------------------------------------
    // Payload types
    // -------------------------------------------------------------------------

    private class PingPayload  { public int Value { get; set; } }
    private class EchoRequest  { public string Data { get; set; } = string.Empty; }
    private class EchoResponse { public string Data { get; set; } = string.Empty; }

    // -------------------------------------------------------------------------
    // Event publish / subscribe
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishEventAsync_TypedEvent_DeliveredToOnEventHandler()
    {
        IMessageBus? sub = null, pub = null;
        var received = new TaskCompletionSource<PingPayload>();
        try
        {
            sub = await ConnectBusAsync("BusEvtSub", b =>
                b.OnEvent<PingPayload>("BusTest", "Ping",
                    env => { received.TrySetResult(env.Body); return Task.CompletedTask; }));

            pub = await ConnectBusAsync("BusEvtPub");

            await pub.PublishEventAsync(RoutingContextId.BroadcastValue, "BusTest", "Ping", new PingPayload { Value = 42 });

            var payload = await AwaitOrTimeout(received);
            Assert.Equal(42, payload.Value);
        }
        finally { await Disconnect(sub); await Disconnect(pub); }
    }

    [Fact]
    public async Task PublishEventAsync_UntypedHandler_DeliveredWithRawPayload()
    {
        IMessageBus? sub = null, pub = null;
        var received = new TaskCompletionSource<string>();
        try
        {
            sub = await ConnectBusAsync("BusEvtRawSub", b =>
                b.OnEvent("BusTest", "Status",
                    env => { received.TrySetResult(env.RawPayload); return Task.CompletedTask; }));

            pub = await ConnectBusAsync("BusEvtRawPub");

            await pub.PublishEventAsync(RoutingContextId.BroadcastValue, "BusTest", "Status", new { Code = "ok" });

            var raw = await AwaitOrTimeout(received);
            Assert.Contains("ok", raw);
        }
        finally { await Disconnect(sub); await Disconnect(pub); }
    }

    // -------------------------------------------------------------------------
    // Request / response
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestAsync_TypedRoundTrip_ReturnsExpectedBody()
    {
        IMessageBus? server = null, client = null;
        try
        {
            server = await ConnectBusAsync("BusReqSvr", b =>
                b.OnRequest<EchoRequest, EchoResponse>("BusTest", "Echo",
                    env => Task.FromResult(new EchoResponse { Data = env.Body.Data + "_reply" })));

            client = await ConnectBusAsync("BusReqCli");

            var response = await client.RequestAsync<EchoRequest, EchoResponse>(
                RoutingContextId.BroadcastValue, "BusTest", "Echo",
                new EchoRequest { Data = "hello" },
                TimeSpan.FromSeconds(5));

            Assert.Equal("hello_reply", response.Body.Data);
        }
        finally { await Disconnect(server); await Disconnect(client); }
    }

    [Fact]
    public async Task RequestAsync_UntypedHandler_RespondsWithPreserializedJson()
    {
        IMessageBus? server = null, client = null;
        try
        {
            server = await ConnectBusAsync("BusRawSvr", b =>
                b.OnRequest("BusTest", "RawEcho",
                    _ => Task.FromResult("{\"Result\":\"raw_ok\"}")));

            client = await ConnectBusAsync("BusRawCli");

            var response = await client.RequestAsync<EchoRequest>(
                RoutingContextId.BroadcastValue, "BusTest", "RawEcho",
                new EchoRequest { Data = "test" },
                TimeSpan.FromSeconds(5));

            Assert.NotNull(response.PayloadJson);
            Assert.Equal("raw_ok", (string)response.PayloadJson!["Result"]!);
        }
        finally { await Disconnect(server); await Disconnect(client); }
    }

    // -------------------------------------------------------------------------
    // Error convention
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestAsync_ErrorFieldInResponse_ThrowsBusRequestException()
    {
        IMessageBus? server = null, client = null;
        try
        {
            server = await ConnectBusAsync("BusErrSvr", b =>
                b.OnRequest("BusTest", "Fail",
                    _ => Task.FromResult("{\"error\":\"entity not found\"}")));

            client = await ConnectBusAsync("BusErrCli");

            var ex = await Assert.ThrowsAsync<BusRequestException>(() =>
                client.RequestAsync<EchoRequest>(
                    RoutingContextId.BroadcastValue, "BusTest", "Fail",
                    new EchoRequest(),
                    TimeSpan.FromSeconds(5)));

            Assert.Equal("entity not found", ex.BusError);
        }
        finally { await Disconnect(server); await Disconnect(client); }
    }

    // -------------------------------------------------------------------------
    // Dynamic handler registration after connect
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OnEvent_RegisteredAfterConnect_DeliveredToHandler()
    {
        IMessageBus? sub = null, pub = null;
        var received = new TaskCompletionSource<int>();
        try
        {
            sub = await ConnectBusAsync("BusDynSub");

            // Register after ConnectAsync -- subscription must be set up on the fly.
            sub.OnEvent<PingPayload>("BusTest", "Late",
                env => { received.TrySetResult(env.Body.Value); return Task.CompletedTask; });

            // Allow the subscription round-trip to the broker to complete.
            await Task.Delay(200);

            pub = await ConnectBusAsync("BusDynPub");
            await pub.PublishEventAsync(RoutingContextId.BroadcastValue, "BusTest", "Late", new PingPayload { Value = 55 });

            var value = await AwaitOrTimeout(received);
            Assert.Equal(55, value);
        }
        finally { await Disconnect(sub); await Disconnect(pub); }
    }

    // -------------------------------------------------------------------------
    // A1 -- inbound non-ASCII payload decoded as UTF-8 (not Encoding.Default)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishEventAsync_NonAsciiPayload_HandlerSeesOriginalString()
    {
        IMessageBus? sub = null, pub = null;
        var received = new TaskCompletionSource<string>();
        try
        {
            sub = await ConnectBusAsync("BusUtf8Sub", b =>
                b.OnEvent("BusTest", "Utf8",
                    env => { received.TrySetResult(env.RawPayload); return Task.CompletedTask; }));

            pub = await ConnectBusAsync("BusUtf8Pub");

            // Marker built from char codes so the source file stays 7-bit ASCII.
            // Codepoints: e-acute (U+00E9), snowman (U+2603), two Japanese codepoints (U+65E5, U+672C).
            var marker = "caf" + (char)0x00E9 + "_" + (char)0x2603 + "_" + (char)0x65E5 + (char)0x672C;
            await pub.PublishEventAsync(RoutingContextId.BroadcastValue, "BusTest", "Utf8", new { Name = marker });

            var raw = await AwaitOrTimeout(received);
            Assert.Contains(marker, raw);
        }
        finally { await Disconnect(sub); await Disconnect(pub); }
    }

    // -------------------------------------------------------------------------
    // A2 -- throwing handler does not kill the MQTTnet receive loop
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessMessageAsync_ThrowingHandler_BusStaysResponsive()
    {
        IMessageBus? sub = null, pub = null;
        var alive = new TaskCompletionSource<int>();
        try
        {
            sub = await ConnectBusAsync("BusThrowSub", b =>
            {
                b.OnEvent("BusTest", "Boom",
                    _ => throw new InvalidOperationException("simulated handler failure"));
                b.OnEvent<PingPayload>("BusTest", "Alive",
                    env => { alive.TrySetResult(env.Body.Value); return Task.CompletedTask; });
            });

            pub = await ConnectBusAsync("BusThrowPub");

            // Fire the throwing handler first; the receive loop must absorb the exception.
            await pub.PublishEventAsync(RoutingContextId.BroadcastValue, "BusTest", "Boom", new { });
            await Task.Delay(200);

            // Then a normal message -- it must still be delivered.
            await pub.PublishEventAsync(RoutingContextId.BroadcastValue, "BusTest", "Alive", new PingPayload { Value = 99 });

            var value = await AwaitOrTimeout(alive);
            Assert.Equal(99, value);
        }
        finally { await Disconnect(sub); await Disconnect(pub); }
    }

    // -------------------------------------------------------------------------
    // B3 -- DisconnectAsync drains pending RequestAsync TCSes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestAsync_DisconnectMidFlight_FaultsWithoutWaitingForTimeout()
    {
        IMessageBus? client = null;
        try
        {
            client = await ConnectBusAsync("BusDrainCli");

            // No server is registered for this op, so the request will park on the
            // pending-response TCS until either its 30s timeout fires or Disconnect drains it.
            var requestTask = client.RequestAsync<EchoRequest>(
                RoutingContextId.BroadcastValue, "BusTest", "NoHandler",
                new EchoRequest { Data = "x" },
                TimeSpan.FromSeconds(30));

            // Let the publish complete and the TCS get registered in _pendingResponses.
            await Task.Delay(200);

            await client.DisconnectAsync();
            client = null;  // suppress the finally Disconnect

            // Without B3 this would hang for ~30s. The drain should fault the task right away.
            var raced = await Task.WhenAny(requestTask, Task.Delay(2000));
            Assert.Same(requestTask, raced);
            await Assert.ThrowsAnyAsync<Exception>(() => requestTask);
        }
        finally { if (client != null) await Disconnect(client); }
    }
}
