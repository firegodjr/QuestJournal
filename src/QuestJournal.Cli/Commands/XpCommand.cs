using QuestJournal.Cli.IO;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

public sealed class XpCommand : ICommand
{
    public string Name => "xp";
    public string Description => "Show XP. Default 'full' prints the footer line; 'today' or 'lifetime' print a single integer.";

    private enum Format { Full, Today, Lifetime }

    public int Run(string[] args)
    {
        var parser = new ArgsParser(args);
        var formatValue = parser.GetFlagValue("--format") ?? "full";
        var format = formatValue.ToLowerInvariant() switch
        {
            "today" => Format.Today,
            "lifetime" => Format.Lifetime,
            "full" => Format.Full,
            _ => (Format)(-1),
        };

        if (format == (Format)(-1))
        {
            var reporter = new ConsoleReporter(AnsiConsole.Console);
            reporter.Error("Unknown --format value", $"{formatValue}. Expected today|lifetime|full.");
            return 1;
        }

        var session = JournalSession.Open(fileOverride: null, requireConfig: false);
        var result = session.Pipeline.RunAfter(
            session.Document,
            journalPath: session.FilePath,
            writeSnapshot: true,
            renderDiff: format == Format.Full);

        switch (format)
        {
            case Format.Today:
                Console.WriteLine(result.TodayXp);
                break;
            case Format.Lifetime:
                Console.WriteLine(result.TotalXp);
                break;
            case Format.Full:
                session.Pipeline.RenderXpFooter(result);
                break;
        }

        return 0;
    }
}
