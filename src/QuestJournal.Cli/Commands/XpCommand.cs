using QuestJournal.Cli.ChangeTracking;
using QuestJournal.Cli.Rendering;
using QuestJournal.Core.Configuration;
using QuestJournal.Core.Parsing;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

public sealed class XpCommand
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
                    AnsiConsole.MarkupLine("[red]--format requires a value (today|lifetime|full).[/]");
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
                AnsiConsole.MarkupLine($"[red]Unexpected argument:[/] {Markup.Escape(a)}");
                return 1;
            }

            switch (value)
            {
                case "today": format = Format.Today; break;
                case "lifetime": format = Format.Lifetime; break;
                case "full": format = Format.Full; break;
                default:
                    AnsiConsole.MarkupLine(
                        $"[red]Unknown --format value:[/] {Markup.Escape(value)}. " +
                        "Expected today|lifetime|full.");
                    return 1;
            }
        }

        var config = LoadConfig();
        var filePath = config.FilePath;
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {Markup.Escape(filePath)}");
            return 1;
        }

        var doc = new JournalParser().ParseFile(filePath);
        var theme = config.NerdFontGlyphs ? GlyphTheme.NerdFont : GlyphTheme.Ascii;
        var pipeline = new ChangeTrackingPipeline(theme);
        var result = pipeline.RunAfter(
            doc,
            journalPath: filePath,
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
                pipeline.RenderXpFooter(result);
                break;
        }

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
