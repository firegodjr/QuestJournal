using QuestJournal.Cli.IO;
using QuestJournal.Core.Configuration;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

/// <summary>
/// Initializes QuestJournal by creating a config file and an optional skeleton journal.
/// </summary>
public sealed class InitCommand : ICommand
{
    public string Name => "init";
    public string Description => "Initialize QuestJournal: create config and an optional skeleton journal file.";

    private const string SkeletonContent = """
                                           # TODAY
                                           ## MAINQUESTS
                                           - [ ] Your first quest!

                                           ## SIDEQUESTS

                                           # TOMORROW
                                           ## MAINQUESTS

                                           ## SIDEQUESTS

                                           # YESTERDAY
                                           ## MAINQUESTS

                                           ## SIDEQUESTS

                                           """;

    public int Run(string[] args)
    {
        var parser = new ArgsParser(args);
        var fileArg = parser.GetFlagValue("--file");
        var force = parser.HasFlag("--force");

        var configPath = ConfigStore.DefaultPath();
        var console = AnsiConsole.Console;
        var reporter = new ConsoleReporter(console);

        // Check for existing config
        if (File.Exists(configPath))
        {
            if (!force)
            {
                var overwrite = console.Confirm(
                    $"[yellow]Config already exists at[/] [bold]{configPath}[/]. Overwrite?");
                if (!overwrite)
                {
                    console.MarkupLine("[grey]Initialization cancelled.[/]");
                    return 0;
                }
            }
            else
            {
                console.MarkupLine(
                    $"[grey]Overwriting existing config at {configPath}[/]");
            }
        }

        // Resolve journal file path
        string journalPath;
        if (fileArg != null)
        {
            journalPath = Path.GetFullPath(fileArg);
            console.MarkupLine($"Using journal path: [bold]{journalPath}[/]");
        }
        else
        {
            journalPath = PromptForJournalPath(console);
        }

        // Handle the journal file
        if (File.Exists(journalPath))
        {
            var useExisting = console.Confirm(
                $"Found existing file at [bold]{journalPath}[/]. Use it?");
            if (!useExisting)
            {
                console.MarkupLine("[grey]Initialization cancelled.[/]");
                return 1;
            }
        }
        else
        {
            if (!force)
            {
                var create = console.Confirm(
                    $"File doesn't exist. Create skeleton [bold]{journalPath}[/]?");
                if (!create)
                {
                    console.MarkupLine("[grey]Cannot initialize without a journal file.[/]");
                    return 1;
                }
            }

            try
            {
                WriteJournalFile(journalPath);
                console.MarkupLine(
                    $"[green]Created skeleton journal at[/] [bold]{journalPath}[/]");
            }
            catch (Exception ex)
            {
                reporter.Error("Failed to create journal file", ex.Message);
                return 1;
            }
        }

        // Write config
        try
        {
            var store = new ConfigStore(configPath);
            store.Save(new Config
            {
                FilePath = journalPath,
                NerdFontGlyphs = true,
            });
            console.MarkupLine(
                $"[green]Wrote config to[/] [bold]{configPath}[/]");
        }
        catch (Exception ex)
        {
            reporter.Error("Failed to write config", ex.Message);
            return 1;
        }

        console.WriteLine();
        console.MarkupLine(
            "[bold green]QuestJournal initialized![/] Run [yellow]quest status[/] to see your journal.");

        return 0;
    }

    private static string PromptForJournalPath(IAnsiConsole console)
    {
        var cwd = Directory.GetCurrentDirectory();
        var defaultPath = Path.GetFullPath(Path.Combine(cwd, "tasks.md"));

        return console.Prompt(
            new TextPrompt<string>(
                    $"Path to your quest journal markdown file [[[grey]{defaultPath}[/]]]:")
                .DefaultValue(defaultPath)
                .Validate(path =>
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return ValidationResult.Error("Path cannot be empty.");
                    }
                    return ValidationResult.Success();
                }));
    }

    private static void WriteJournalFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, SkeletonContent);
    }
}
