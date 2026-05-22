using System.Diagnostics;
using QuestJournal.Core.Parsing;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

public sealed class EditCommand : ICommand
{
    public int Run(string[] args)
    {
        var session = JournalSession.Open(fileOverride: null, requireConfig: true);

        var editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            AnsiConsole.MarkupLine("[red]$EDITOR is not set.[/] Set it to your preferred editor (e.g. nvim).");
            return 1;
        }

        var questlogDir = Path.GetDirectoryName(session.FilePath)!;

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

        var psi = new ProcessStartInfo(editor, $"\"{session.FilePath}\"")
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

        if (File.Exists(session.FilePath))
        {
            var doc = new JournalParser().ParseFile(session.FilePath);
            var result = session.Pipeline.RunAfter(doc, session.FilePath, writeSnapshot: true);
            if (result.HasChanges)
            {
                session.Pipeline.RenderXpFooter(result);
            }
        }

        return exitCode;
    }
}
