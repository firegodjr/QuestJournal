using System.Diagnostics;
using QuestJournal.Cli.ChangeTracking;
using QuestJournal.Cli.Rendering;
using QuestJournal.Core.Configuration;
using QuestJournal.Core.Parsing;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

public sealed class EditCommand
{
    public int Run(string[] args)
    {
        Config config;
        try
        {
            config = new ConfigStore().Load();
        }
        catch (ConfigMissingException ex)
        {
            AnsiConsole.MarkupLine($"[red]Config:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        var filePath = config.FilePath;
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {Markup.Escape(filePath)}");
            return 1;
        }

        var editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            AnsiConsole.MarkupLine("[red]$EDITOR is not set.[/] Set it to your preferred editor (e.g. nvim).");
            return 1;
        }

        var questlogDir = Path.GetDirectoryName(filePath)!;

        if (string.Equals(Path.GetFileName(editor), "nvim", StringComparison.Ordinal))
        {
            var targetExrc = Path.Combine(questlogDir, ".nvim.lua");
            var bundledExrc = Path.Combine(AppContext.BaseDirectory, "Assets", "nvim.lua");
            if (!File.Exists(targetExrc) && File.Exists(bundledExrc))
            {
                try
                {
                    File.Copy(bundledExrc, targetExrc);
                }
                catch (IOException ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] could not write {Markup.Escape(targetExrc)}: {Markup.Escape(ex.Message)}");
                }
            }
        }

        var psi = new ProcessStartInfo(editor, $"\"{filePath}\"")
        {
            WorkingDirectory = questlogDir,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            AnsiConsole.MarkupLine($"[red]Failed to start editor:[/] {Markup.Escape(editor)}");
            return 1;
        }
        proc.WaitForExit();
        var exitCode = proc.ExitCode;

        if (File.Exists(filePath))
        {
            var doc = new JournalParser().ParseFile(filePath);
            var theme = config.NerdFontGlyphs ? GlyphTheme.NerdFont : GlyphTheme.Ascii;
            var pipeline = new ChangeTrackingPipeline(theme);
            var result = pipeline.RunAfter(doc, filePath, writeSnapshot: true);
            if (result.HasChanges)
            {
                pipeline.RenderXpFooter(result);
            }
        }

        return exitCode;
    }
}
