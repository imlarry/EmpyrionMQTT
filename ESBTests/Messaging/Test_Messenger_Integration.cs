using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ESB.Configuration;
using ESB.Helpers;
using ESB.Messaging;

namespace ESBTests.Messaging;

/// <summary>
/// Integration tests for Messenger against a real MQTT broker.
/// Requires broker running with credentials from ESB_Info.yaml.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class Test_Messenger_Integration
{
    private sealed class TestCtx : BaseContextData { }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static async Task<Messenger> ConnectAsync(string participantType)
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ESB_Info.yaml");
        var cfg = YamlFileReader.ReadYamlFile<ESBConfig>(configPath).MQTThost;
        var m = new Messenger();
        await m.ConnectAsync(new TestCtx(), participantType, cfg.WithTcpServer, cfg.Port, cfg.Username, cfg.Password);
        return m;
    }

    // Cancels the TCS after ms, causing it to throw TimeoutException when awaited.
    private static Task<T> AwaitOrTimeout<T>(TaskCompletionSource<T> tcs, int ms)
    {
        var cts = new CancellationTokenSource(ms);
        cts.Token.Register(() => tcs.TrySetException(new TimeoutException($"No result within {ms}ms")));
        return tcs.Task;
    }

    private static async Task Disconnect(Messenger m)
    {
        try { await m.DisconnectAsync(); } catch { }
    }

    // -------------------------------------------------------------------------
    // SendAsync topic construction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_TopicContainsLowercaseMsgType()
    {
        Messenger? pub = null, sub = null;
        try
        {
            var received = new TaskCompletionSource<string>();

            sub = await ConnectAsync("SubLc");
            await sub.SubscribeEventAsync("ESB/+/+/Game/evt/Ping", (topic, _) =>
            {
                received.TrySetResult(topic);
                return Task.CompletedTask;
            });

            pub = await ConnectAsync("PubLc");
            await pub.SendAsync("Game", MessageType.Evt, "Ping", "{}");

            var topic = await AwaitOrTimeout(received, 3000);
            Assert.Contains("/evt/", topic);
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    [Fact]
    public async Task SendAsync_PascalCaseTopicFilterReceivesNothing()
    {
        Messenger? pub = null, sub = null;
        try
        {
            var received = false;

            sub = await ConnectAsync("SubPc");
            await sub.SubscribeEventAsync("ESB/+/+/Game/Evt/Ping", (_, __) =>
            {
                received = true;
                return Task.CompletedTask;
            });

            pub = await ConnectAsync("PubPc");
            await pub.SendAsync("Game", MessageType.Evt, "Ping", "{}");
            await Task.Delay(400);

            Assert.False(received, "uppercase Evt topic filter should not match lowercase evt topic");
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    // -------------------------------------------------------------------------
    // User properties
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_UserProperties_ArriveInMessageContext()
    {
        Messenger? pub = null, sub = null;
        try
        {
            var received = new TaskCompletionSource<MessageContext>();

            sub = await ConnectAsync("SubUp");
            sub.RegisterHandler("Game/evt/Ping", ctx =>
            {
                received.TrySetResult(ctx);
                return Task.CompletedTask;
            });
            await sub.SubscribeBrokerAsync("ESB/+/+/Game/evt/Ping");

            pub = await ConnectAsync("PubUp");
            await pub.SendAsync("Game", MessageType.Evt, "Ping", "{}",
                new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("target", "Edna"),
                    new KeyValuePair<string, string>("targetId", "p32r")
                });

            var ctx = await AwaitOrTimeout(received, 3000);
            Assert.NotNull(ctx.UserProperties);
            Assert.Contains(ctx.UserProperties!, kv => kv.Key == "target"   && kv.Value == "Edna");
            Assert.Contains(ctx.UserProperties!, kv => kv.Key == "targetId" && kv.Value == "p32r");
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    [Fact]
    public async Task SendAsync_NoUserProperties_UserPropertiesIsNull()
    {
        Messenger? pub = null, sub = null;
        try
        {
            var received = new TaskCompletionSource<MessageContext>();

            sub = await ConnectAsync("SubNup");
            sub.RegisterHandler("Game/evt/Ping", ctx =>
            {
                received.TrySetResult(ctx);
                return Task.CompletedTask;
            });
            await sub.SubscribeBrokerAsync("ESB/+/+/Game/evt/Ping");

            pub = await ConnectAsync("PubNup");
            await pub.SendAsync("Game", MessageType.Evt, "Ping", "{}");

            var ctx = await AwaitOrTimeout(received, 3000);
            Assert.Null(ctx.UserProperties);
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    // -------------------------------------------------------------------------
    // Dispatch key routing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RegisterHandler_ReqAndEvt_RoutedToSeparateHandlers()
    {
        Messenger? pub = null, sub = null;
        try
        {
            var reqReceived = new TaskCompletionSource<bool>();
            var evtReceived = new TaskCompletionSource<bool>();

            sub = await ConnectAsync("SubRE");
            sub.RegisterHandler("Game/req/Ping", ctx => { reqReceived.TrySetResult(true); return Task.CompletedTask; });
            sub.RegisterHandler("Game/evt/Ping", ctx => { evtReceived.TrySetResult(true); return Task.CompletedTask; });
            await sub.SubscribeBrokerAsync("ESB/+/+/Game/+/Ping");

            pub = await ConnectAsync("PubRE");
            await pub.SendAsync("Game", MessageType.Req, "Ping", "{}");
            await pub.SendAsync("Game", MessageType.Evt, "Ping", "{}");

            await AwaitOrTimeout(reqReceived, 3000);
            await AwaitOrTimeout(evtReceived, 3000);
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    [Fact]
    public async Task RegisterHandler_UnknownDispatchKey_EvtHandlerDoesNotFire()
    {
        // Send a req message when only an evt handler is registered for the same operation.
        // The req should not trigger the evt handler.
        Messenger? pub = null, sub = null;
        try
        {
            var evtFired = false;

            sub = await ConnectAsync("SubUdk");
            sub.RegisterHandler("Game/evt/Ping", ctx => { evtFired = true; return Task.CompletedTask; });
            await sub.SubscribeBrokerAsync("ESB/+/+/Game/+/Ping");

            pub = await ConnectAsync("PubUdk");
            await pub.SendAsync("Game", MessageType.Req, "Ping", "{}");
            await Task.Delay(400);

            Assert.False(evtFired, "req message must not trigger evt handler");
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    // -------------------------------------------------------------------------
    // RequestAsync / response routing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestAsync_ResponseTopicIsStructuredEsbTopic()
    {
        Messenger? pub = null, sub = null;
        try
        {
            string? capturedResponseTopic = null;

            sub = await ConnectAsync("SubRt");
            sub.RegisterHandler("Tracking/req/Enable", async ctx =>
            {
                capturedResponseTopic = ctx.ResponseTopic;
                await sub.ReplyAsync(ctx.ResponseTopic!, ctx.CorrelationData!, "{}");
            });
            await sub.SubscribeBrokerAsync("ESB/+/+/Tracking/req/Enable");

            pub = await ConnectAsync("PubRt");
            await pub.RequestAsync("Tracking", "Enable", "{}", TimeSpan.FromSeconds(5));

            Assert.NotNull(capturedResponseTopic);
            Assert.True(capturedResponseTopic!.StartsWith("ESB/"), "response topic must begin with ESB/");
            Assert.Contains("/res/",  capturedResponseTopic);
            Assert.DoesNotContain("tmp/", capturedResponseTopic);
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    [Fact]
    public async Task RequestAsync_CorrelationDataDemuxesReply()
    {
        // Two concurrent requests to the same scope/operation both resolve to their own payload.
        Messenger? pub = null, sub = null;
        try
        {
            sub = await ConnectAsync("SubCd");
            sub.RegisterHandler("Tracking/req/Enable", async ctx =>
            {
                // Echo the incoming payload back so we can verify which TCS receives which result.
                await sub.ReplyAsync(ctx.ResponseTopic!, ctx.CorrelationData!, ctx.Payload ?? "");
            });
            await sub.SubscribeBrokerAsync("ESB/+/+/Tracking/req/Enable");

            pub = await ConnectAsync("PubCd");
            var t1 = pub.RequestAsync("Tracking", "Enable", "payload1", TimeSpan.FromSeconds(5));
            var t2 = pub.RequestAsync("Tracking", "Enable", "payload2", TimeSpan.FromSeconds(5));

            var results = await Task.WhenAll(t1, t2);
            Assert.Contains("payload1", results);
            Assert.Contains("payload2", results);
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    [Fact]
    public async Task RequestAsync_Timeout_ThrowsTimeoutException()
    {
        Messenger? pub = null;
        try
        {
            pub = await ConnectAsync("PubTo");
            await Assert.ThrowsAsync<TimeoutException>(() =>
                pub.RequestAsync("Tracking", "NoResponder", "{}", TimeSpan.FromMilliseconds(300)));
        }
        finally { await Disconnect(pub!); }
    }

    [Fact]
    public async Task RequestAsync_AfterTimeout_SubsequentCallSucceeds()
    {
        // Verifies that a timed-out call cleans up its pending entry so a later call works correctly.
        Messenger? pub = null, sub = null;
        try
        {
            pub = await ConnectAsync("PubAt");

            // First call: no responder, should time out.
            await Assert.ThrowsAsync<TimeoutException>(() =>
                pub.RequestAsync("Tracking", "Enable", "{}", TimeSpan.FromMilliseconds(300)));

            // Register a responder, then issue a second call: should succeed.
            sub = await ConnectAsync("SubAt");
            sub.RegisterHandler("Tracking/req/Enable", async ctx =>
                await sub.ReplyAsync(ctx.ResponseTopic!, ctx.CorrelationData!, "ok"));
            await sub.SubscribeBrokerAsync("ESB/+/+/Tracking/req/Enable");

            var result = await pub.RequestAsync("Tracking", "Enable", "{}", TimeSpan.FromSeconds(5));
            Assert.Equal("ok", result);
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    [Fact]
    public async Task RequestAsync_ReplyAsync_EndToEnd()
    {
        Messenger? pub = null, sub = null;
        try
        {
            sub = await ConnectAsync("SubE2e");
            sub.RegisterHandler("Tracking/req/Enable", async ctx =>
                await sub.ReplyAsync(ctx.ResponseTopic!, ctx.CorrelationData!, "{\"Status\":\"ok\"}"));
            await sub.SubscribeBrokerAsync("ESB/+/+/Tracking/req/Enable");

            pub = await ConnectAsync("PubE2e");
            var result = await pub.RequestAsync("Tracking", "Enable", "{}", TimeSpan.FromSeconds(5));

            Assert.Contains("Status", result);
            Assert.Contains("ok",     result);
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }

    // -------------------------------------------------------------------------
    // ConnectAsync establishes persistent response subscription
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConnectAsync_EstablishesResponseSubscription()
    {
        // RequestAsync must receive its response via the subscription set up in ConnectAsync --
        // no extra SubscribeBrokerAsync call is made on the requester side.
        Messenger? pub = null, sub = null;
        try
        {
            sub = await ConnectAsync("SubPs");
            sub.RegisterHandler("Tracking/req/Enable", async ctx =>
                await sub.ReplyAsync(ctx.ResponseTopic!, ctx.CorrelationData!, "pong"));
            await sub.SubscribeBrokerAsync("ESB/+/+/Tracking/req/Enable");

            // Requester: ConnectAsync auto-subscribes ESB/{type}/{id}/+/res/+.
            // No additional subscribe call is made.
            pub = await ConnectAsync("PubPs");
            var result = await pub.RequestAsync("Tracking", "Enable", "{}", TimeSpan.FromSeconds(5));

            Assert.Equal("pong", result);
        }
        finally { await Disconnect(pub!); await Disconnect(sub!); }
    }
}
