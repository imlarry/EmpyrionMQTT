using ESBMessaging;
using Newtonsoft.Json.Linq;

namespace Gatetech;

// since the Logger uses a SQL connection in all handler routines so we derive an extended context for it
public class GTConsoleSpecificContext : BaseContextData
{
}

public class GTConsole
{
    public GTConsoleSpecificContext CTX { get; set; } = new GTConsoleSpecificContext();

    readonly Messenger esb = new();
    static void Main(string[] args)
    {
        Console.WriteLine("GatetechConsole: MQTT bus listener that coordinates control of the Flexion Amplifier gate network");
        Console.WriteLine();
        if (args.Length != 0) { Console.WriteLine("...no support for params or switches yet"); }

        // create the non-static class
        GTConsole console = new();
        console.Init();

        // console loops while the logger works in background
        while (true)
        {
            Console.Write("Gatetech> ");
            string? cmd = Console.ReadLine()?.Trim();
            if (cmd != null && cmd.ToLower() == "exit") { return; }
            Console.WriteLine("the only command right now is 'exit'");
        }
    }

    async void Init()
    {
        // create messenger and configure
        await esb.ConnectAsync(CTX, "Gatetech", "localhost");

        // subscribe to events we want to respond to
        await esb.Subscribe("ESB/Gatetech/Gatetech.GTConsole.DetectGateMessaging/R", DetectGateMessaging);
        await esb.SendAsync("ESB/Gatetech/AvailableTopics/I", esb.AvailableTopics());
    }

    // ************************ subscription handler tasks ************************

    // this routine determines if a player has taken control of a flexion amplifier and, if so, presents them with an ingame UI via dialog
    public async void DetectGateMessaging(string topic, string payload)
    {
        //var esb = (Messenger)context;
        // parse messages and classify
        //JObject args = JObject.Parse(payload);

        await esb.SendAsync(esb.RespondTo(topic, "X"), "In DetectGateMessaging");
    }

}


