using QuestJournal.Cli.ChangeTracking;
using QuestJournal.Cli.Rendering;
using QuestJournal.Core.Configuration;
using QuestJournal.Core.Model;
using QuestJournal.Core.Parsing;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

public sealed class StatusCommand
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

        var config = LoadConfig();
        var filePath = fileOverride ?? config.FilePath;
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {Markup.Escape(filePath)}");
            return 1;
        }

        var doc = new JournalParser().ParseFile(filePath);
        var theme = config.NerdFontGlyphs ? GlyphTheme.NerdFont : GlyphTheme.Ascii;
        var renderer = new StatusRenderer(theme);

        var pipeline = new ChangeTrackingPipeline(theme);
        var trackingResult = pipeline.RunAfter(
            doc,
            journalPath: filePath,
            writeSnapshot: fileOverride is null);

        IEnumerable<DaySection> targets;
        if (all)
        {
            targets = doc.Days;
        }
        else if (dayArg is not null)
        {
            var match = doc.Days.FirstOrDefault(d =>
                string.Equals(d.Name, dayArg, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Error:[/] no section named '{Markup.Escape(dayArg)}' in journal. " +
                    $"Found: {Markup.Escape(string.Join(", ", doc.Days.Select(d => d.Name)))}");
                return 2;
            }
            targets = new[] { match };
        }
        else
        {
            var today = doc.Days.FirstOrDefault(d =>
                string.Equals(d.Name, "TODAY", StringComparison.OrdinalIgnoreCase));
            if (today is null)
            {
                AnsiConsole.MarkupLine(
                    "[red]Error:[/] no section named 'TODAY' in journal. " +
                    $"Found: {Markup.Escape(string.Join(", ", doc.Days.Select(d => d.Name)))}");
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
        pipeline.RenderXpFooter(trackingResult);

        return 0;
    }

    private static Config LoadConfig()
    {
        try
        {
            return new ConfigStore().Load();
        }
        catch (ConfigMissingException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Config:[/] {Markup.Escape(ex.Message)}");
            return new Config();
        }
    }
}
