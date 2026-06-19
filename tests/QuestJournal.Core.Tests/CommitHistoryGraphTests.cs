using QuestJournal.Core.ChangeTracking;

namespace QuestJournal.Core.Tests;

public class CommitHistoryGraphTests
{
    private static readonly DateTimeOffset Now = Local(2026, 6, 15, 12);

    /// <summary>A DateTimeOffset whose local wall-clock matches the fields, so ToLocalTime()
    /// bucketing is timezone-independent in tests.</summary>
    private static DateTimeOffset Local(int year, int month, int day, int hour)
    {
        var dt = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
    }

    private static RepoCommits Repo(string name, params DateTimeOffset[] commits) => new()
    {
        Name = name,
        FullPath = $"/repos/{name}",
        Commits = commits,
    };

    [Fact]
    public void Week_buckets_commits_into_day_hour_cells()
    {
        var repos = new[]
        {
            Repo("a", Local(2026, 6, 15, 9), Local(2026, 6, 15, 9)), // today (col 6), 08–10 block (row 4)
            Repo("b", Local(2026, 6, 13, 17)),                       // two days ago (col 4), 16–18 block (row 8)
        };

        var grid = CommitHistoryGraph.Build(GraphScope.Week, repos, Now);

        Assert.Equal(7, grid.Columns);
        Assert.Equal(12, grid.Rows); // full 24h in 2-hour blocks
        Assert.Equal(new[] { "00", "02", "04", "06", "08", "10", "12", "14", "16", "18", "20", "22" }, grid.RowLabels);
        Assert.Equal(2, grid.Layers.Count);
        Assert.Equal(2, grid.Layers[0].Counts[4, 6]); // repo a: two commits in one cell
        Assert.Equal(1, grid.Layers[1].Counts[8, 4]); // repo b
    }

    [Fact]
    public void Week_keeps_night_commits_unlike_xp_graph()
    {
        var repos = new[] { Repo("a", Local(2026, 6, 15, 3)) }; // 3am

        var grid = CommitHistoryGraph.Build(GraphScope.Week, repos, Now);

        Assert.Equal(1, grid.Layers[0].Counts[1, 6]); // 02–04 block (row 1), today (col 6)
    }

    [Fact]
    public void Month_buckets_by_day_of_week()
    {
        var repos = new[] { Repo("a", Local(2026, 6, 15, 1)) }; // today is Monday

        var grid = CommitHistoryGraph.Build(GraphScope.Month, repos, Now);

        Assert.Equal(7, grid.Rows);
        Assert.Equal(1, grid.Layers[0].Counts[1, grid.Columns - 1]); // Monday (row 1), last column
    }

    [Fact]
    public void Year_buckets_by_day_of_month()
    {
        var repos = new[] { Repo("a", Local(2026, 6, 15, 12)) };

        var grid = CommitHistoryGraph.Build(GraphScope.Year, repos, Now);

        Assert.Equal(12, grid.Columns);
        Assert.Equal(31, grid.Rows);
        Assert.Equal(1, grid.Layers[0].Counts[14, 11]); // 2026-06 (last col), day 15 (row 14)
    }

    [Fact]
    public void All_starts_from_earliest_commit_month()
    {
        var repos = new[]
        {
            Repo("a", Local(2026, 3, 1, 12)),
            Repo("b", Local(2026, 6, 15, 12)),
        };

        var grid = CommitHistoryGraph.Build(GraphScope.All, repos, Now);

        Assert.Equal(4, grid.Columns); // 2026-03, 04, 05, 06
        Assert.Equal("2026-03", grid.ColumnLabels[0]);
        Assert.Equal("2026-06", grid.ColumnLabels[3]);
        Assert.Equal(1, grid.Layers[0].Counts[0, 0]);  // repo a: 2026-03 day 1
        Assert.Equal(1, grid.Layers[1].Counts[14, 3]); // repo b: 2026-06 day 15
    }

    [Fact]
    public void Repos_with_no_in_window_commits_are_excluded()
    {
        var repos = new[]
        {
            Repo("present", Local(2026, 6, 15, 12)),
            Repo("absent", Local(2026, 1, 1, 12)), // outside the trailing-week window
        };

        var grid = CommitHistoryGraph.Build(GraphScope.Week, repos, Now);

        Assert.Single(grid.Layers);
        Assert.Equal("present", grid.Layers[0].Name);
    }

    [Fact]
    public void Each_repo_layer_normalizes_to_its_own_max()
    {
        var repos = new[]
        {
            Repo("busy", Local(2026, 6, 15, 9), Local(2026, 6, 15, 9), Local(2026, 6, 15, 9)),
            Repo("quiet", Local(2026, 6, 15, 9)),
        };

        var grid = CommitHistoryGraph.Build(GraphScope.Week, repos, Now);

        Assert.Equal(3, grid.Layers[0].Max);
        Assert.Equal(1, grid.Layers[1].Max);
    }

    [Fact]
    public void Layer_order_follows_input_order()
    {
        var repos = new[]
        {
            Repo("first", Local(2026, 6, 15, 9)),
            Repo("second", Local(2026, 6, 15, 9)),
        };

        var grid = CommitHistoryGraph.Build(GraphScope.Week, repos, Now);

        Assert.Equal(new[] { "first", "second" }, grid.Layers.Select(l => l.Name));
    }

    [Fact]
    public void Empty_when_no_repos_or_no_commits()
    {
        Assert.True(CommitHistoryGraph.Build(GraphScope.Week, Array.Empty<RepoCommits>(), Now).IsEmpty);
        Assert.True(CommitHistoryGraph.Build(GraphScope.All, new[] { Repo("a") }, Now).IsEmpty);
    }
}
