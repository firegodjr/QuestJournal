using Spectre.Console;

namespace QuestJournal.Cli.IO;

public sealed class ConsoleReporter
{
    private readonly IAnsiConsole _console;

    public ConsoleReporter(IAnsiConsole console)
    {
        _console = console;
    }

    public void Error(string prefix, string message) =>
        _console.MarkupLine($"[red]{prefix}:[/] {Markup.Escape(message)}");

    public void Warn(string prefix, string message) =>
        _console.MarkupLine($"[yellow]{prefix}:[/] {Markup.Escape(message)}");

    public void ErrorLine(string message) =>
        _console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
}
