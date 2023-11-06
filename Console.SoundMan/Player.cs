using ESBMessaging;
using NAudio.Wave;
using Newtonsoft.Json.Linq;

namespace ESBlog;

// the sound player does not have any program specific things to pass around so just the base class (for now anyhow)
public class PlayerSpecificContext : BaseContextData { }
public class SoundManPlayer
{
    public PlayerSpecificContext CTX { get; set; } = new PlayerSpecificContext();

    readonly Messenger buslistener = new();

    static void Main(string[] args)
    {
        Console.WriteLine("ESB.SoundMan: MQTT bus listener that plays sounds");
        Console.WriteLine();
        if (args.Length != 0) { Console.WriteLine("...no support for params or switches yet"); }

        // create the non-static SoundManPlayer class
        SoundManPlayer soundManPlayer = new();

        soundManPlayer.Init();

        // console loops while the logger works in background
        while (true)
        {
            Console.Write("SoundMan> ");
            string? cmd = Console.ReadLine()?.Trim();
            if (cmd != null && cmd.ToLower() == "exit") { return; }
            Console.WriteLine("the only command right now is 'exit'");
        }
    }

    async void Init()
    {
        // create messenger and configure
        if (buslistener == null) return;
        await buslistener.ConnectAsync(CTX, "SoundMan", "localhost");       // THIS LINE TRIGGERS ERROR .. WHY?

        // subscribe to events that trigger the player
        //await buslistener.Subscribe("ESB/Client/ModApi.GameEvent.WindowOpened/E", PlayEventSound);

        await buslistener.Subscribe("ESB/SoundMan/#/Q", Wildcard);

    }

    // ************************ subscription handler tasks ************************

    // this routine ties a single GameEvent message to playing a sound
    async void PlayEventSound(string topic, string payload)
    {
        await buslistener.SendAsync("inSoundManPlayer", "");
        string? filePath = null;

        // grab Type and Arg1
        JObject GameEvent = JObject.Parse(payload);

        var type = (GameEvent.GetValue("Type") ?? "").ToString();
        var arg1 = (GameEvent.GetValue("Arg1") ?? "").ToString();

        if ((type == "WindowOpened") && (arg1 == "RecursiveConstructor")) filePath = "SampleMedia/Cash-counter-machine-sound-effect.mp3";

        if (filePath != null)
        {
            using var fileReader = new AudioFileReader(filePath);
            using var outputDevice = new WaveOutEvent();
            outputDevice.Init(fileReader);
            outputDevice.Play();
            while (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                System.Threading.Thread.Sleep(100);
            }
        }
    }

    async void Test(string topic, string payload)
    {
        await buslistener.SendAsync("in test", topic);
    }

    async void Test2(string topic, string payload)
    {
        await buslistener.SendAsync(" in test2", topic);
    }

    async void Wildcard(string topic, string payload)
    {
        await buslistener.SendAsync("in wildcard", topic);
    }

}


