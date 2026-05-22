using QuestJournal.Cli.IO;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

public sealed class XpCommand : ICommand
{
    private enum Format { Full, Today, Lifetime }

    public int Run(string[] args)
    {
        var format = Format.Full;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string? value = null;

            if (a == "--format")
            {
                if (i + 1 >= args.Length)
                {
                    ConsoleReporter.ErrorLine("--format requires a value (today|lifetime|full).");
                    return 1;
                }
                value = args[++i];
            }
            else if (a.StartsWith("--format="))
            {
                value = a.Substring("--format=".Length);
            }
            else
            {
                ConsoleReporter.Error("Unexpected argument", a);
                return 1;
            }

            switch (value)
            {
                case "today": format = Format.Today; break;
                case "lifetime": format = Format.Lifetime; break;
                case "full": format = Format.Full; break;
                default:
                    ConsoleReporter.Error("Unknown --format value", $"{value}. Expected today|lifetime|full.");
                    return 1;
            }
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
