using QuestJournal.Core.ChangeTracking;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

/// <summary>
/// Orchestrates building and rendering XP/completed heatmaps from history data.
/// </summary>
public sealed class HistoryGraphOrchestrator
{
    private readonly QuestTheme _theme;
    private readonly IAnsiConsole _console;
    private readonly IHistoryArchiveStore _archive;

    public HistoryGraphOrchestrator(QuestTheme theme, IAnsiConsole console, IHistoryArchiveStore? archive = null)
    {
        _theme = theme;
        _console = console;
        _archive = archive ?? new HistoryArchiveStore();
    }

    public void Render(IReadOnlyList<HistoryEntry> entries, GraphScope scope)
    {
        var archive = (scope is GraphScope.Year or GraphScope.All)
            ? _archive.LoadAll()
            : Array.Empty<HistoryArchiveMonth>();

        var now = DateTimeOffset.Now;
        var xpGrid = XpHistoryGraph.Build(scope, entries, archive, now, GraphMetric.Xp);
        var completedGrid = XpHistoryGraph.Build(scope, entries, archive, now, GraphMetric.Completed);

        if (xpGrid.Max == 0 && completedGrid.Max == 0)
        {
            _console.MarkupLine("[dim]No history to graph.[/]");
            return;
        }

        var renderer = new HeatmapRenderer(_theme, _console);
        renderer.Render(xpGrid);
        _console.WriteLine();
        renderer.Render(completedGrid);
    }
}
