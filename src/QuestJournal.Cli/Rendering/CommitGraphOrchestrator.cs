using QuestJournal.Cli.IO;
using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.IO;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

/// <summary>
/// Orchestrates building and rendering git commit heatmaps across lazygit's recent repos.
/// </summary>
public sealed class CommitGraphOrchestrator
{
    private readonly IAnsiConsole _console;

    public CommitGraphOrchestrator(IAnsiConsole console)
    {
        _console = console;
    }

    public void Render(GraphScope scope)
    {
        var now = DateTimeOffset.Now;
        var repos = LazygitRecentRepos.Load();
        if (repos.Count == 0)
        {
            _console.MarkupLine("[dim]No lazygit repositories found.[/]");
            return;
        }

        var since = HeatmapLayout.WindowStartFor(scope, now);
        var commits = new GitCommitCollector().Collect(repos, since);
        var grid = CommitHistoryGraph.Build(scope, commits, now);

        if (grid.IsEmpty)
        {
            _console.MarkupLine("[dim]No commits to graph.[/]");
            return;
        }

        new CommitHeatmapRenderer(_console).Render(grid);
    }
}
