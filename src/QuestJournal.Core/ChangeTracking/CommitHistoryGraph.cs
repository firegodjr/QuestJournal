namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// Buckets each repository's commit timestamps into a shared <see cref="HeatmapLayout"/>,
/// producing one <see cref="RepoLayer"/> per repo that committed in the window. Uses the same
/// layout (and therefore the same local-time bucketing) as <see cref="XpHistoryGraph"/>, except
/// the week scope spans the full 24 hours so late-night commits aren't dropped.
/// </summary>
public static class CommitHistoryGraph
{
    public static CommitHeatmap Build(GraphScope scope, IReadOnlyList<RepoCommits> repos, DateTimeOffset now)
    {
        var layout = LayoutFor(scope, repos, now);
        if (layout is null)
        {
            return Empty(scope);
        }

        var layers = new List<RepoLayer>(repos.Count);
        foreach (var repo in repos)
        {
            var counts = new long[layout.Rows, layout.Columns];
            long total = 0;
            long max = 0;
            foreach (var commit in repo.Commits)
            {
                if (layout.TryLocate(commit, out var row, out var col))
                {
                    var v = ++counts[row, col];
                    total++;
                    if (v > max)
                    {
                        max = v;
                    }
                }
            }

            if (total == 0)
            {
                continue; // No in-window commits → no layer (per product decision).
            }

            layers.Add(new RepoLayer
            {
                Name = repo.Name,
                FullPath = repo.FullPath,
                Counts = counts,
                Max = max,
            });
        }

        return new CommitHeatmap
        {
            Scope = scope,
            ColumnLabels = layout.ColumnLabels,
            RowLabels = layout.RowLabels,
            HasData = layout.HasData,
            Layers = layers,
        };
    }

    private static HeatmapLayout? LayoutFor(GraphScope scope, IReadOnlyList<RepoCommits> repos, DateTimeOffset now)
    {
        var local = now.ToLocalTime();
        switch (scope)
        {
            case GraphScope.Week:
                return HeatmapLayout.ForDayHour(scope, now, days: 7, labelFormat: "dd", startHour: 0, endHour: 24, blockHours: 2);
            case GraphScope.Month:
                return HeatmapLayout.ForMonthCalendar(scope, now, monthsBack: 3);
            case GraphScope.Year:
                var yearStart = new DateOnly(local.Year, local.Month, 1).AddMonths(-11);
                return HeatmapLayout.ForMonthDay(scope, yearStart, now, labelFormat: "MMM");
            case GraphScope.All:
                var earliest = EarliestCommit(repos);
                if (earliest is null)
                {
                    return null; // No commits anywhere → nothing to graph.
                }
                var allStart = new DateOnly(earliest.Value.Year, earliest.Value.Month, 1);
                return HeatmapLayout.ForMonthDay(scope, allStart, now, labelFormat: "yyyy-MM");
            default:
                return null;
        }
    }

    private static DateTimeOffset? EarliestCommit(IReadOnlyList<RepoCommits> repos)
    {
        DateTimeOffset? earliest = null;
        foreach (var repo in repos)
        {
            foreach (var commit in repo.Commits)
            {
                var local = commit.ToLocalTime();
                if (earliest is null || local < earliest)
                {
                    earliest = local;
                }
            }
        }
        return earliest;
    }

    private static CommitHeatmap Empty(GraphScope scope) => new()
    {
        Scope = scope,
        ColumnLabels = Array.Empty<string>(),
        RowLabels = Array.Empty<string>(),
        HasData = new bool[0, 0],
        Layers = Array.Empty<RepoLayer>(),
    };
}
