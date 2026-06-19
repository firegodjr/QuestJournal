using QuestJournal.Cli.Commands;
using QuestJournal.Cli.IO;
using Spectre.Console;

var console = AnsiConsole.Console;
var reporter = new ConsoleReporter(console);

var commands = new List<ICommand>
{
    new StatusCommand(),
    new EditCommand(),
    new XpCommand(),
    new HistoryCommand(),
    new InitCommand(),
};
var commandMap = commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    PrintHelp(commands, console);
    return 0;
}

if (!commandMap.TryGetValue(args[0], out var command))
{
    reporter.Error("Unknown command", args[0]);
    PrintHelp(commands, console);
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
        reporter.Error("Error", ex.Message);
    }
    return ex.ExitCode;
}
catch (Exception ex)
{
    reporter.Error("Error", ex.Message);
    return 1;
}

static void PrintHelp(List<ICommand> commands, IAnsiConsole console)
{
    console.MarkupLine("[bold]quest[/] - markdown task journal reader");
    console.WriteLine();
    console.MarkupLine("Commands:");
    foreach (var cmd in commands)
    {
        console.MarkupLine($"  [yellow]{cmd.Name}[/]");
        console.MarkupLine($"    {cmd.Description}");
    }
}
