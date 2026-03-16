using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ESBTests.Infrastructure;

/// <summary>
/// Thin MQTT client for integration testing. Each test should create its own
/// instance (await using) so topic subscriptions don't bleed across tests.
///
/// Usage:
///   await using var mqtt = await MqttTestClient.ConnectAsync();
///   var (topic, payload) = await mqtt.RequestAsync("Structure.Info", "{\"EntityId\":5319}");
///   Assert.StartsWith("Client/R/", topic);
/// </summary>
public sealed class MqttTestClient : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttFactory _factory;

    private MqttTestClient(IMqttClient client, MqttFactory factory)
    {
        _client = client;
        _factory = factory;
    }

    public static async Task<MqttTestClient> ConnectAsync(string host = "localhost", int port = 1883)
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .Build();

        await client.ConnectAsync(options, CancellationToken.None);
        return new MqttTestClient(client, factory);
    }

    /// <summary>
    /// Publishes a request to <c>{appId}/Q/{handler}/*/1</c> and awaits the
    /// first message on <c>{appId}/R/{handler}/#</c> or <c>{appId}/X/{handler}/#</c>.
    /// Returns the full topic string and the parsed JSON payload.
    /// Throws <see cref="TimeoutException"/> if no response arrives within <paramref name="timeoutMs"/>.
    /// </summary>
    public async Task<(string Topic, JObject Payload)> RequestAsync(
        string handler,
        string requestJson,
        int timeoutMs = 5000,
        string appId = "Client")
    {
        var tcs = new TaskCompletionSource<(string, JObject)>();

        Func<MqttApplicationMessageReceivedEventArgs, Task> onMessage = e =>
        {
            var t = e.ApplicationMessage.Topic;
            if (t.StartsWith($"{appId}/R/{handler}/") || t.StartsWith($"{appId}/X/{handler}/"))
            {
                var seg = e.ApplicationMessage.PayloadSegment;
                var json = Encoding.UTF8.GetString(seg.Array!, seg.Offset, seg.Count);
                tcs.TrySetResult((t, JObject.Parse(json)));
            }
            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += onMessage;

        try
        {
            var subOptions = _factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"{appId}/R/{handler}/#"))
                .WithTopicFilter(f => f.WithTopic($"{appId}/X/{handler}/#"))
                .Build();
            await _client.SubscribeAsync(subOptions, CancellationToken.None);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"{appId}/Q/{handler}/*/1")
                .WithPayload(requestJson)
                .Build();
            await _client.PublishAsync(message, CancellationToken.None);

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
                tcs.TrySetException(new TimeoutException($"{handler}: no response within {timeoutMs}ms")));

            return await tcs.Task;
        }
        finally
        {
            _client.ApplicationMessageReceivedAsync -= onMessage;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync();
        _client.Dispose();
    }
}
