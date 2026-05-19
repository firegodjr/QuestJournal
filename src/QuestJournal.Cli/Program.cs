using QuestJournal.Cli.Commands;
using Spectre.Console;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    PrintHelp();
    return 0;
}

try
{
    return args[0] switch
    {
        "status" => new StatusCommand().Run(args[1..]),
        _ => UnknownCommand(args[0]),
    };
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
    return 1;
}

static int UnknownCommand(string name)
{
    AnsiConsole.MarkupLine($"[red]Unknown command:[/] {Markup.Escape(name)}");
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    AnsiConsole.MarkupLine("[bold]quest[/] - markdown task journal reader");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Commands:");
    AnsiConsole.MarkupLine("  [yellow]status[/] [[day]] [[-a|--all]] [[--file <path>]]");
    AnsiConsole.MarkupLine("    Print top-level quests for a day (default: TODAY).");
}
