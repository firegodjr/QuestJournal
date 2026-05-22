using Spectre.Console;

namespace QuestJournal.Cli.IO;

public static class ConsoleReporter
{
    public static void Error(string prefix, string message) =>
        AnsiConsole.MarkupLine($"[red]{prefix}:[/] {Markup.Escape(message)}");

    public static void Warn(string prefix, string message) =>
        AnsiConsole.MarkupLine($"[yellow]{prefix}:[/] {Markup.Escape(message)}");

    public static void ErrorLine(string message) =>
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
}
