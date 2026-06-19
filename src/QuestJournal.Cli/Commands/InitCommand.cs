using QuestJournal.Core.Configuration;
using QuestJournal.Cli.IO;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

/// <summary>
/// Initializes QuestJournal by creating a config file and an optional skeleton journal.
/// </summary>
public sealed class InitCommand : ICommand
{
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
        // Parse optional flags: --file <path>, --force
        string? fileArg = null;
        bool force = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file" when i + 1 < args.Length:
                    fileArg = args[++i];
                    break;
                case "--force":
                    force = true;
                    break;
                default:
                    ConsoleReporter.Error("Unknown argument", args[i]);
                    return 1;
            }
        }

        var configPath = ConfigStore.DefaultPath();

        // Check for existing config
        if (File.Exists(configPath))
        {
            if (!force)
            {
                var overwrite = AnsiConsole.Confirm(
                    $"[yellow]Config already exists at[/] [bold]{configPath}[/]. Overwrite?");
                if (!overwrite)
                {
                    AnsiConsole.MarkupLine("[grey]Initialization cancelled.[/]");
                    return 0;
                }
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"[grey]Overwriting existing config at {configPath}[/]");
            }
        }

        // Resolve journal file path
        string journalPath;
        if (fileArg != null)
        {
            journalPath = Path.GetFullPath(fileArg);
            AnsiConsole.MarkupLine($"Using journal path: [bold]{journalPath}[/]");
        }
        else
        {
            journalPath = PromptForJournalPath();
        }

        // Handle the journal file
        if (File.Exists(journalPath))
        {
            var useExisting = AnsiConsole.Confirm(
                $"Found existing file at [bold]{journalPath}[/]. Use it?");
            if (!useExisting)
            {
                AnsiConsole.MarkupLine("[grey]Initialization cancelled.[/]");
                return 1;
            }
        }
        else
        {
            if (!force)
            {
                var create = AnsiConsole.Confirm(
                    $"File doesn't exist. Create skeleton [bold]{journalPath}[/]?");
                if (!create)
                {
                    AnsiConsole.MarkupLine("[grey]Cannot initialize without a journal file.[/]");
                    return 1;
                }
            }

            try
            {
                WriteJournalFile(journalPath);
                AnsiConsole.MarkupLine(
                    $"[green]Created skeleton journal at[/] [bold]{journalPath}[/]");
            }
            catch (Exception ex)
            {
                ConsoleReporter.Error("Failed to create journal file", ex.Message);
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
            AnsiConsole.MarkupLine(
                $"[green]Wrote config to[/] [bold]{configPath}[/]");
        }
        catch (Exception ex)
        {
            ConsoleReporter.Error("Failed to write config", ex.Message);
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[bold green]QuestJournal initialized![/] Run [yellow]quest status[/] to see your journal.");

        return 0;
    }

    private static string PromptForJournalPath()
    {
        var cwd = Directory.GetCurrentDirectory();
        var defaultPath = Path.GetFullPath(Path.Combine(cwd, "tasks.md"));

        return AnsiConsole.Prompt(
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
