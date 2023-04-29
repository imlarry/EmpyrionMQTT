using ESBMessaging;
using Newtonsoft.Json.Linq;

namespace Gatetech;

// since the Logger uses a SQL connection in all handler routines so we derive an extended context for it
public class GTConsoleSpecificContext : BaseContextData
{
}

internal class GTConsole
{

    static void Main(string[] args)
    {
        Console.WriteLine("GatetechConsole: MQTT bus listener that coordinates control of the Flexion Amplifier gate network");
        Console.WriteLine();
        if (args.Length != 0) { Console.WriteLine("...no support for params or switches yet"); }

        // create messenger and configure
        Messenger esb = new();
        GTConsoleSpecificContext ctx = new();
        esb.ConnectAsync(ctx,"GatetechConsole", "localhost").Wait();
        var gatetechConsole = new GTConsole();
        //esb.RegisterLocalMethod("Gatetech.GTConsole.DetectGateMessaging", gatetechConsole.DetectGateMessaging);
        esb.Subscribe("ESB/Gatetech/Gatetech.GTConsole.DetectGateMessaging/R", gatetechConsole.DetectGateMessaging).Wait();
        esb.SendAsync("ESB/Gatetech/AvailableTopics/I", esb.AvailableTopics()).Wait(); 

        // console loops while the logger works in background
        while (true)
        {
            Console.Write("Gatetech> ");
            string? cmd = Console.ReadLine()?.Trim();
            Console.WriteLine("...no support for a console CLI yet");
            // TODO: command "EXIT" to cleanup and close .. others?
        }
    }

    // ************************ subscription handler tasks ************************

    // this routine determines if a player has taken control of a flexion amplifier and, if so, presents them with an ingame UI via dialog
    public async void DetectGateMessaging(string topic, string payload)
    {
        //var esb = (Messenger)context;
        // parse messages and classify
        //JObject args = JObject.Parse(payload);

        //await esb.SendAsync(esb.RespondTo(topic, "X"), "In DetectGateMessaging");

        return; 
    }

}


