using QuestJournal.Cli.Commands;
using QuestJournal.Cli.IO;
using Spectre.Console;

var commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase)
{
    ["status"] = new StatusCommand(),
    ["edit"] = new EditCommand(),
    ["xp"] = new XpCommand(),
    ["history"] = new HistoryCommand(),
};

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    PrintHelp();
    return 0;
}

if (!commands.TryGetValue(args[0], out var command))
{
    ConsoleReporter.Error("Unknown command", args[0]);
    PrintHelp();
    return 1;
}

try
{
    return command.Run(args[1..]);
}
catch (JournalSessionException ex)
{
    if (!ex.Reported)
    {
        ConsoleReporter.Error("Error", ex.Message);
    }
    return ex.ExitCode;
}
catch (Exception ex)
{
    ConsoleReporter.Error("Error", ex.Message);
    return 1;
}

static void PrintHelp()
{
    AnsiConsole.MarkupLine("[bold]quest[/] - markdown task journal reader");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Commands:");
    AnsiConsole.MarkupLine("  [yellow]status[/] [[day]] [[-a|--all]] [[--file <path>]]");
    AnsiConsole.MarkupLine("    Print top-level quests for a day (default: TODAY).");
    AnsiConsole.MarkupLine("  [yellow]edit[/]");
    AnsiConsole.MarkupLine("    Open the configured journal in $EDITOR. For nvim, drops a bundled .nvim.lua next to the journal if missing.");
    AnsiConsole.MarkupLine("  [yellow]xp[/] [[--format=today|lifetime|full]]");
    AnsiConsole.MarkupLine("    Show XP. Default 'full' prints the footer line; 'today' or 'lifetime' print a single integer.");
    AnsiConsole.MarkupLine("  [yellow]history[/] [[-a|--all]] [[--entry <text>]] [[--graph [[--commits]] [[--week|--month|--year|--all]]]] [[--file <path>]]");
    AnsiConsole.MarkupLine("    Show recorded changes. Default: last 24h; --all: all retained history. With --entry: full timeline of one quest and its children.");
    AnsiConsole.MarkupLine("    With --graph: heatmap of XP over time (--week default, --month, --year, --all).");
    AnsiConsole.MarkupLine("    With --graph --commits: heatmap of your git commits across lazygit's recent repos, one color per repo.");
}
