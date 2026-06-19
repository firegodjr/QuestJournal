using QuestJournal.Cli.IO;
using QuestJournal.Cli.Rendering;
using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.IO;
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
    private static readonly TimeSpan RecentWindow = TimeSpan.FromHours(24);

    public int Run(string[] args)
    {
        string? entry = null;
        string? fileOverride = null;
        bool all = false;
        bool graph = false;
        bool commits = false;
        GraphScope? explicitScope = null;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-a":
                case "--all":
                    all = true;
                    break;
                case "--graph":
                    graph = true;
                    break;
                case "--commits":
                    graph = true;
                    commits = true;
                    break;
                case "--week":
                    graph = true;
                    explicitScope = GraphScope.Week;
                    break;
                case "--month":
                    graph = true;
                    explicitScope = GraphScope.Month;
                    break;
                case "--year":
                    graph = true;
                    explicitScope = GraphScope.Year;
                    break;
                case "--entry":
                    // Greedily consume the following tokens up to the next flag, so an
                    // unquoted multi-word entry (--entry write the report) works too.
                    entry = ConsumeWords(args, ref i);
                    if (entry is null)
                    {
                        ConsoleReporter.ErrorLine("--entry requires a quest text argument.");
                        return 1;
                    }
                    break;
                case "--file":
                    if (i + 1 >= args.Length)
                    {
                        ConsoleReporter.ErrorLine("--file requires a path argument.");
                        return 1;
                    }
                    fileOverride = args[++i];
                    break;
                default:
                    if (a.StartsWith("--entry="))
                    {
                        var head = a.Substring("--entry=".Length);
                        var tail = ConsumeWords(args, ref i);
                        entry = tail is null ? head : $"{head} {tail}";
                    }
                    else if (a.StartsWith("--file="))
                    {
                        fileOverride = a.Substring("--file=".Length);
                    }
                    else
                    {
                        ConsoleReporter.Error("Unexpected argument", a);
                        return 1;
                    }
                    break;
            }
        }

        // The commit graph reads git + lazygit, not the journal, so it needs no session.
        if (commits)
        {
            var commitScope = explicitScope ?? (all ? GraphScope.All : GraphScope.Week);
            return RenderCommitGraph(commitScope);
        }

        var session = JournalSession.Open(fileOverride, requireConfig: false);

        var entries = new HistoryStore().LoadAll()
            .Where(e => string.Equals(e.JournalPath, session.FilePath, StringComparison.Ordinal))
            .ToList();

        if (graph)
        {
            var scope = explicitScope ?? (all ? GraphScope.All : GraphScope.Week);
            return RenderGraph(session, entries, scope);
        }

        var renderer = new HistoryRenderer(session.Theme);
        return entry is null
            ? RenderRecent(renderer, entries, all)
            : RenderTimeline(renderer, entries, entry);
    }

    private static int RenderGraph(JournalSession session, List<HistoryEntry> entries, GraphScope scope)
    {
        // The archive carries no journal path, so monthly scopes (year/all) include archived
        // XP from every journal. Acceptable for v1 — most users track a single journal.
        var archive = (scope is GraphScope.Year or GraphScope.All)
            ? new HistoryArchiveStore().LoadAll()
            : Array.Empty<HistoryArchiveMonth>();

        var now = DateTimeOffset.Now;
        var xpGrid = XpHistoryGraph.Build(scope, entries, archive, now, GraphMetric.Xp);
        var completedGrid = XpHistoryGraph.Build(scope, entries, archive, now, GraphMetric.Completed);

        if (xpGrid.Max == 0 && completedGrid.Max == 0)
        {
            AnsiConsole.MarkupLine("[dim]No history to graph.[/]");
            return 0;
        }

        var renderer = new HeatmapRenderer(session.Theme);
        renderer.Render(xpGrid);
        AnsiConsole.WriteLine();
        renderer.Render(completedGrid);
        return 0;
    }

    private static int RenderCommitGraph(GraphScope scope)
    {
        var now = DateTimeOffset.Now;
        var repos = LazygitRecentRepos.Load();
        if (repos.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No lazygit repositories found.[/]");
            return 0;
        }

        var since = HeatmapLayout.WindowStartFor(scope, now);
        var commits = new GitCommitCollector().Collect(repos, since);
        var grid = CommitHistoryGraph.Build(scope, commits, now);

        if (grid.IsEmpty)
        {
            AnsiConsole.MarkupLine("[dim]No commits to graph.[/]");
            return 0;
        }

        new CommitHeatmapRenderer().Render(grid);
        return 0;
    }

    private static int RenderRecent(HistoryRenderer renderer, List<HistoryEntry> entries, bool all)
    {
        var selected = all
            ? entries
            : entries.Where(e => e.Timestamp >= DateTimeOffset.UtcNow - RecentWindow).ToList();

        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine(all
                ? "[dim]No history recorded.[/]"
                : "[dim]No history in the last 24 hours.[/]");
            return 0;
        }

        renderer.RenderRecent(selected);
        return 0;
    }

    private static int RenderTimeline(HistoryRenderer renderer, List<HistoryEntry> entries, string entry)
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
            AnsiConsole.MarkupLine($"[dim]No history for \"{Markup.Escape(entry)}\".[/]");
            return 0;
        }

        renderer.RenderTimeline(entry, events);
        return 0;
    }

    /// <summary>
    /// Joins the bare tokens following <paramref name="i"/> (advancing it past them) until
    /// the next flag, so an unquoted multi-word value is captured as one string. Returns
    /// null if no words follow.
    /// </summary>
    private static string? ConsumeWords(string[] args, ref int i)
    {
        var words = new List<string>();
        while (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
        {
            words.Add(args[++i]);
        }
        return words.Count == 0 ? null : string.Join(' ', words);
    }

    private static bool Matches(string entry, string text, List<string> ancestors) =>
        string.Equals(text, entry, StringComparison.Ordinal) || ancestors.Contains(entry);
}
