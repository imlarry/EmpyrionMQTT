using ESB.Messaging;
using CommandLine;
using System.Text;

namespace EDNAClient
{
    public class EDNASpecificContext : BaseContextData
    {
        //public SQLiteConnection DBconnection { get; set; } = new("Data Source=Discovery.db");
    }

    public class EDNAconsole
    {
        public EDNASpecificContext CTX { get; set; } = new EDNASpecificContext();
        readonly Messenger buslistener = new();

        public EDNAconsole()
        {
            CTX.Messenger = buslistener;
        }

        public async void Init()
        {
            // create messenger and configure
            await buslistener.ConnectAsync(CTX, "EDNA", "localhost");

            //subscribe to events we want to log
            //await buslistener.Subscribe("Client/Q/Application.OnPlayfieldLoaded/+", SomeEvent);

            // Start the REPL loop
            StartReplLoop();
        }

        public void StartReplLoop()
        {
            bool errorOccurred = false;
            Task.Run(() =>
            {
                try
                {
                    while (!errorOccurred)
                    {
                        //WriteConsole("EDNA> ");
                        string ?cmd = Console.ReadLine()?.Trim();
                        if (cmd != null)
                        {
                            var result = Parser.Default.ParseArguments<ExitOptions, OtherCommandOptions>(cmd.Split(' '));
                            result.MapResult(
                                (ExitOptions opts) => ExitApplication(),
                                (OtherCommandOptions opts) => ExecuteOtherCommand(opts),
                                errs => HandleParseError(errs, ref errorOccurred)
                            );
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // handle exit
                    //WriteConsole("done...");
                }
            });
        }


        // Handler methods:

        int ExitApplication()
        {
            //WriteConsole("Exiting");
            return 0;
        }

        int ExecuteOtherCommand(OtherCommandOptions opts)
        {
            // Execute your other command here using the options in 'opts'
            //WriteConsole(opts?.ToString() ?? "Null command options");
            Console.WriteLine(opts.ToString());
            return 0;
        }

        int HandleParseError(IEnumerable<Error> errs, ref bool errorOccurred)
        {
            if (errs.Any())
            {
                StringBuilder sb = new() ;
                foreach (var err in errs)
                {
                    sb.AppendLine($"Error: {err.Tag}");
                    sb.AppendLine($"Message: {err}");
                }
                //WriteConsole(sb.ToString());
                errorOccurred = true;
            }
            // Handle parsing errors here
            return 1;
        }

    }

    [Verb("exit")]
    class ExitOptions
    {
    }

    [Verb("show", HelpText = "Show command.")]
    public class ShowCommandOptions
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }

        [Option('l', "log", Required = false, Default = "EWEoutput.txt", HelpText = "Name of the log file.")]

        public string ?Log { get; set; }
    }

    [Verb("other")]
    class OtherCommandOptions
    {
        // Define options for your other command here
    }

}
