using System;
using System.Threading.Tasks;

// ------------------------------------------------------------
// Public-facing message bus API (interfaces only)
// ------------------------------------------------------------

public interface IMessageBus
{
    // Publish an event (fire-and-forget)
    void PublishEvent(string scope, string operation, object payload);

    // Subscribe to events
    void OnEvent(string scope, string operation, Action<MessageEnvelope> handler);

    // Send a request and await a response
    Task<MessageEnvelope> Request(string scope, string operation, object payload);

    // Handle incoming requests
    void OnRequest(string scope, string operation, Func<MessageEnvelope, Task<object>> handler);

    // Emit logs
    void Log(string level, string message);
}

// ------------------------------------------------------------
// Envelope exposed to developers
// ------------------------------------------------------------

public class MessageEnvelope
{
    public string CorrelationId { get; set; }
    public string ReplyTo { get; set; }
    public string Timestamp { get; set; }
    public string SenderType { get; set; }
    public string SenderConnectionId { get; set; }
    public string Scope { get; set; }
    public string Operation { get; set; }
    public string MsgType { get; set; }
    public object Payload { get; set; }
}

// ------------------------------------------------------------
// Example program demonstrating the entire public API
// ------------------------------------------------------------

public class Program
{
    public static async Task Main()
    {
        // Pretend this is created by your library
        IMessageBus bus = CreateBus();

        // --------------------------------------------------------
        // 1. Publish an event
        // --------------------------------------------------------
        bus.PublishEvent(
            scope: "playfield",
            operation: "BlockAdded",
            payload: new { id = 42, color = "red" }
        );

        // --------------------------------------------------------
        // 2. Subscribe to events
        // --------------------------------------------------------
        bus.OnEvent("playfield", "BlockAdded", evt =>
        {
            Console.WriteLine($"Event received: {evt.Operation}, payload={evt.Payload}");
        });

        // --------------------------------------------------------
        // 3. Register a request handler
        // --------------------------------------------------------
        bus.OnRequest("playfield", "GetAllBlocks", async request =>
        {
            // Return a response payload
            return new[]
            {
                new { id = 1, color = "blue" },
                new { id = 2, color = "green" }
            };
        });

        // --------------------------------------------------------
        // 4. Send a request and await a response
        // --------------------------------------------------------
        var response = await bus.Request(
            scope: "playfield",
            operation: "GetAllBlocks",
            payload: new { }
        );

        Console.WriteLine($"Response received: {response.Operation}, payload={response.Payload}");

        // --------------------------------------------------------
        // 5. Emit a log message
        // --------------------------------------------------------
        bus.Log("info", "Demo program completed");
    }

    // Stub factory for the example
    private static IMessageBus CreateBus()
    {
        return new FakeBus();
    }
}

// ------------------------------------------------------------
// Fake implementation so the demo compiles and runs
// (Your real library would replace this entirely.)
// ------------------------------------------------------------

public class FakeBus : IMessageBus
{
    public void PublishEvent(string scope, string operation, object payload)
    {
        Console.WriteLine($"[PublishEvent] {scope}/{operation}");
    }

    public void OnEvent(string scope, string operation, Action<MessageEnvelope> handler)
    {
        Console.WriteLine($"[OnEvent] Subscribed to {scope}/{operation}");
    }

    public async Task<MessageEnvelope> Request(string scope, string operation, object payload)
    {
        Console.WriteLine($"[Request] {scope}/{operation}");

        await Task.Delay(100); // simulate async

        return new MessageEnvelope
        {
            Operation = operation,
            Scope = scope,
            MsgType = "resp",
            Payload = new[] { new { id = 1, color = "blue" } }
        };
    }

    public void OnRequest(string scope, string operation, Func<MessageEnvelope, Task<object>> handler)
    {
        Console.WriteLine($"[OnRequest] Handler registered for {scope}/{operation}");
    }

    public void Log(string level, string message)
    {
        Console.WriteLine($"[Log:{level}] {message}");
    }
}
