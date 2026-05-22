using QuestJournal.Cli.Rendering;
using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

public sealed class StatusCommand : ICommand
{
    public int Run(string[] args)
    {
        string? dayArg = null;
        bool all = false;
        string? fileOverride = null;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-a":
                case "--all":
                    all = true;
                    break;
                case "--file":
                    if (i + 1 >= args.Length)
                    {
                        AnsiConsole.MarkupLine("[red]--file requires a path argument.[/]");
                        return 1;
                    }
                    fileOverride = args[++i];
                    break;
                default:
                    if (a.StartsWith("--file="))
                    {
                        fileOverride = a.Substring("--file=".Length);
                    }
                    else if (dayArg is null)
                    {
                        dayArg = a;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Unexpected argument:[/] {Markup.Escape(a)}");
                        return 1;
                    }
                    break;
            }
        }

        var session = JournalSession.Open(fileOverride, requireConfig: false);
        var renderer = new StatusRenderer(session.Theme);
        var trackingResult = session.Pipeline.RunAfter(
            session.Document,
            journalPath: session.FilePath,
            writeSnapshot: !session.FileOverridden);

        IEnumerable<DaySection> targets;
        if (all)
        {
            targets = session.Document.Days;
        }
        else if (dayArg is not null)
        {
            var match = session.Document.Days.FirstOrDefault(d =>
                string.Equals(d.Name, dayArg, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Error:[/] no section named '{Markup.Escape(dayArg)}' in journal. " +
                    $"Found: {Markup.Escape(string.Join(", ", session.Document.Days.Select(d => d.Name)))}");
                return 2;
            }
            targets = new[] { match };
        }
        else
        {
            var today = session.Document.Days.FirstOrDefault(d =>
                string.Equals(d.Name, "TODAY", StringComparison.OrdinalIgnoreCase));
            if (today is null)
            {
                AnsiConsole.MarkupLine(
                    "[red]Error:[/] no section named 'TODAY' in journal. " +
                    $"Found: {Markup.Escape(string.Join(", ", session.Document.Days.Select(d => d.Name)))}");
                return 2;
            }
            targets = new[] { today };
        }

        bool first = true;
        foreach (var day in targets)
        {
            if (!first) AnsiConsole.WriteLine();
            renderer.RenderDay(day);
            first = false;
        }

        AnsiConsole.WriteLine();
        session.Pipeline.RenderXpFooter(trackingResult);

        return 0;
    }
}
