using ESB.Messaging;
using CommandLine;
using ESBLog.Common;
using ESBLog.TopicHandlers;
using ESBLog.Database;

namespace ESBlog;

public class Logger
{
    public LoggerSpecificContext CTX { get; set; } = new LoggerSpecificContext();
    readonly private DbAccess _dbAccess = new("YourConnectionString", true);

    readonly Messenger buslistener = new();

    public Logger()
    {
        CTX.Messenger = buslistener;
    }

    // parse option definitions
    public class Options
    {
        [Option('o', "output", Required = false, HelpText = "Output to a specific file.")]
        public string? OutputFileName { get; set; }
    }

    // ************************ MAIN PROGRAM ************************
    static void Main(string[] args)
    {
        Console.WriteLine("ESBlog: MQTT bus listener to SQLite event db");
        Console.WriteLine();
        Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunWithOptions(opts))
                .WithNotParsed(errs => HandleParseError(errs));
    }

    static void HandleParseError(IEnumerable<Error> errs)
    {
        foreach (var err in errs)
        {
            switch (err)
            {
                case MissingRequiredOptionError missingRequiredOptionError:
                    Console.WriteLine($"Error: Missing required option '-{missingRequiredOptionError.NameInfo.NameText}'.");
                    break;
                case NamedError namedError:
                    Console.WriteLine($"Error: Invalid argument '-{namedError.NameInfo.NameText}'.");
                    break;
                case BadFormatTokenError badFormatTokenError:
                    Console.WriteLine($"Error: Bad format for argument '{badFormatTokenError.Token}'.");
                    break;
                case NoVerbSelectedError:
                    Console.WriteLine("Error: No verb selected.");
                    break;
                default:
                    Console.WriteLine("Error: Unknown error occurred while parsing arguments.");
                    break;
            }
        }
    }

    static void RunWithOptions(Options opts)
    {
        // create the logger and initialize it
        Logger logger = new();
        logger.Init();

        // -output[=filename] .. no console out or redirect to an output file if specified (or sysout if -output without filename)
        if (opts.OutputFileName == null)
        {
            var nullStream = Stream.Null;
            var nullStreamWriter = new StreamWriter(nullStream);
            Console.SetOut(nullStreamWriter);
        }
        else if (!string.IsNullOrEmpty(opts.OutputFileName))
        {
            var fileStream = new FileStream(opts.OutputFileName, FileMode.Create);
            var streamWriter = new StreamWriter(fileStream);
            Console.SetOut(streamWriter);
        }

        // keep the logger running until the user presses Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("Cleanup complete. Exiting...");
                Environment.Exit(0);
            };        
        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    // initialize db connection, open messenger, subscribe to messages we log
    async void Init()
    {
        CTX.DBconnection = _dbAccess;
        await buslistener.ConnectAsync(CTX, "Logger", "localhost");        
        DatabaseLogging dbLogging = new(CTX);                           // instantiate topic handlers
        await dbLogging.Subscribe();                                    // enable subscriptions
    }
}
