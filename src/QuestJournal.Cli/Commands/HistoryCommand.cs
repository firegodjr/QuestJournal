using QuestJournal.Cli.IO;
using QuestJournal.Cli.Rendering;
using QuestJournal.Core.ChangeTracking;
using Spectre.Console;

namespace QuestJournal.Cli.Commands;

/// <summary>
/// Read-only view over the durable change log. Default shows the last 24 hours of
/// changes (standup prep); <c>--entry "text"</c> shows the full timeline of one quest
/// and its children; <c>--graph</c> plots XP over time (<c>--week</c>/<c>--month</c>/
/// <c>--year</c>/<c>--all</c> scopes). <c>--graph --commits</c> instead plots the user's
/// git commits across lazygit's recent repositories, one color gradient per repo.
/// </summary>
public sealed class HistoryCommand : ICommand
{
    public string Name => "history";
    public string Description => "Show recorded changes. Default: last 24h; --all: all retained history. With --entry: full timeline of one quest and its children.";

    private static readonly TimeSpan RecentWindow = TimeSpan.FromHours(24);

    public int Run(string[] args)
    {
        var parser = new ArgsParser(args);
        var all = parser.HasFlag("-a") || parser.HasFlag("--all");
        var graph = parser.HasFlag("--graph");
        var commits = parser.HasFlag("--commits");
        if (commits) graph = true;
        var entry = parser.GetFlagValue("--entry");
        var fileOverride = parser.GetFlagValue("--file");

        // Scope flags: --week, --month, --year
        GraphScope? explicitScope = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--week": graph = true; explicitScope = GraphScope.Week; break;
                case "--month": graph = true; explicitScope = GraphScope.Month; break;
                case "--year": graph = true; explicitScope = GraphScope.Year; break;
            }
        }

        // The commit graph reads git + lazygit, not the journal, so it needs no session.
        if (commits)
        {
            var commitScope = explicitScope ?? (all ? GraphScope.All : GraphScope.Week);
            new CommitGraphOrchestrator(AnsiConsole.Console).Render(commitScope);
            return 0;
        }

        var session = JournalSession.Open(fileOverride, requireConfig: false);

        var entries = new HistoryStore().LoadAll()
            .Where(e => string.Equals(e.JournalPath, session.FilePath, StringComparison.Ordinal))
            .ToList();

        if (graph)
        {
            var scope = explicitScope ?? (all ? GraphScope.All : GraphScope.Week);
            new HistoryGraphOrchestrator(session.Theme, session.Console).Render(entries, scope);
            return 0;
        }

        var renderer = new HistoryRenderer(session.Theme, session.Console);
        return entry is null
            ? RenderRecent(session, renderer, entries, all)
            : RenderTimeline(session, renderer, entries, entry);
    }

    private static int RenderRecent(JournalSession session, HistoryRenderer renderer, List<HistoryEntry> entries, bool all)
    {
        var selected = all
            ? entries
            : entries.Where(e => e.Timestamp >= DateTimeOffset.UtcNow - RecentWindow).ToList();

        if (selected.Count == 0)
        {
            session.Console.MarkupLine(all
                ? "[dim]No history recorded.[/]"
                : "[dim]No history in the last 24 hours.[/]");
            return 0;
        }

        renderer.RenderRecent(selected);
        return 0;
    }

    private static int RenderTimeline(JournalSession session, HistoryRenderer renderer, List<HistoryEntry> entries, string entry)
    {
        var events = new List<(DateTimeOffset Timestamp, string Line)>();

        foreach (var batch in entries)
        {
            foreach (var change in batch.Changes)
            {
                if (Matches(entry, change.Text, change.Ancestors))
                {
                    events.Add((batch.Timestamp, renderer.ChangeLine(change)));
                }
            }
            foreach (var move in batch.Moves)
            {
                if (Matches(entry, move.Text, move.FromAncestors) || Matches(entry, move.Text, move.ToAncestors))
                {
                    events.Add((batch.Timestamp, renderer.MoveLine(move)));
                }
            }
        }

        if (events.Count == 0)
        {
            session.Console.MarkupLine($"[dim]No history for \"{Markup.Escape(entry)}\".[/]");
            return 0;
        }

        renderer.RenderTimeline(entry, events);
        return 0;
    }

    private static bool Matches(string entry, string text, List<string> ancestors) =>
        string.Equals(text, entry, StringComparison.Ordinal) || ancestors.Contains(entry);
}
