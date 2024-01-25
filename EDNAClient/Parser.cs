using CommandLine;
using System.Text;

namespace EDNA
{
    public class Options
    {
        [Option('o', "output", Required = false, HelpText = "Output to a specific file.")]
        public string? OutputFileName { get; set; }
    }

    public class CommandLineParser
    {
        public static void HandleParseError(IEnumerable<Error> errs, NonFocusableRichTextBox outputConsole)
        {
            if (errs.Any())
            {
                StringBuilder sb = new();
                foreach (var err in errs)
                {
                    sb.AppendLine($"Error: {err.Tag}");
                    sb.AppendLine($"Message: {err}");
                }
                outputConsole.AppendText(sb.ToString());
            }
        }

        public static void RunWithOptions(Options opts, NonFocusableRichTextBox outputConsole)
        {
            outputConsole.AppendText(Environment.NewLine + $"OutputFileName: {opts.OutputFileName}");
        }

        public static void Evaluate(string input, NonFocusableRichTextBox outputConsole)
        {
            Parser.Default.ParseArguments<Options>(input.Split(' '))
                .WithParsed<Options>(opts => RunWithOptions(opts, outputConsole))
                .WithNotParsed(errs => HandleParseError(errs, outputConsole));
        }
    }
}