using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.Tests;

public class XpHistoryGraphTests
{
    private static readonly DateTimeOffset Now = Local(2026, 6, 15, 12);

    /// <summary>Builds a DateTimeOffset whose local wall-clock is exactly the given fields,
    /// so <c>ToLocalTime()</c> bucketing is timezone-independent in tests.</summary>
    private static DateTimeOffset Local(int year, int month, int day, int hour)
    {
        var dt = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
    }

    private static HistoryEntry Entry(DateTimeOffset timestamp, long xp) => new()
    {
        Timestamp = timestamp,
        JournalPath = "/abs/journal.md",
        XpAwarded = xp,
    };

    private static HistoryEntry CompletedEntry(DateTimeOffset timestamp, int count)
    {
        var entry = new HistoryEntry { Timestamp = timestamp, JournalPath = "/abs/journal.md", XpAwarded = count * 10 };
        for (int i = 0; i < count; i++)
        {
            entry.Changes.Add(new HistoryChange
            {
                Kind = nameof(Change.StatusChanged),
                Text = $"task-{i}",
                NewStatus = QuestStatus.Completed,
            });
        }
        return entry;
    }

    private static HistoryArchiveMonth Archived(
        string month, long totalXp, long completed = 0,
        Dictionary<int, long>? xpByDay = null, Dictionary<int, long>? completedByDay = null) =>
        new()
        {
            Month = month,
            TotalXp = totalXp,
            CompletedCount = completed,
            XpByDay = xpByDay ?? new(),
            CompletedByDay = completedByDay ?? new(),
        };

    [Fact]
    public void Week_grid_is_days_by_two_hour_blocks_from_seven_am()
    {
        var detail = new[]
        {
            Entry(Local(2026, 6, 15, 9), 10),   // today (col 6), 09–11 block (row 1)
            Entry(Local(2026, 6, 13, 17), 3),   // two days ago (col 4), 17–19 block (row 5)
        };

        var grid = XpHistoryGraph.Build(GraphScope.Week, detail, Array.Empty<HistoryArchiveMonth>(), Now);

        Assert.Equal(7, grid.Columns);
        Assert.Equal(6, grid.Rows);
        Assert.Equal(new[] { "07", "09", "11", "13", "15", "17" }, grid.RowLabels);
        Assert.Equal("15", grid.ColumnLabels[6]);
        Assert.Equal(10, grid.Values[1, 6]);
        Assert.Equal(3, grid.Values[5, 4]);
        Assert.Equal(10, grid.Max);
        Assert.True(grid.HasData[0, 0]);
    }

    [Fact]
    public void Week_grid_clamps_entries_outside_seven_am_to_seven_pm_into_edge_rows()
    {
        var detail = new[]
        {
            Entry(Local(2026, 6, 15, 3), 11),    // 3am: before the window → first row (07)
            Entry(Local(2026, 6, 15, 19), 13),   // 7pm: at/after the window → last row (17)
            Entry(Local(2026, 6, 15, 12), 4),    // noon: 11–13 block (row 2)
        };

        var grid = XpHistoryGraph.Build(GraphScope.Week, detail, Array.Empty<HistoryArchiveMonth>(), Now);

        Assert.Equal(11, grid.Values[0, 6]);   // 3am clamped into the first block, today's column
        Assert.Equal(13, grid.Values[5, 6]);   // 7pm clamped into the last block, today's column
        Assert.Equal(4, grid.Values[2, 6]);
        Assert.Equal(13, grid.Max);            // nothing dropped
    }

    [Fact]
    public void Month_grid_is_weeks_by_day_of_week_over_three_months()
    {
        // Now is Monday 2026-06-15; the window starts 2026-03-15 (3 calendar months back).
        var detail = new[]
        {
            Entry(Local(2026, 6, 15, 1), 7),    // today: Monday (row 1), last column
            Entry(Local(2026, 4, 1, 9), 5),     // ~2.5 months ago: inside the window
            Entry(Local(2026, 2, 1, 9), 99),    // >3 months ago: outside the window, dropped
        };

        var grid = XpHistoryGraph.Build(GraphScope.Month, detail, Array.Empty<HistoryArchiveMonth>(), Now);

        Assert.Equal(7, grid.Rows);
        Assert.Equal(new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" }, grid.RowLabels);
        Assert.InRange(grid.Columns, 13, 14);            // ~3 months of weeks
        Assert.Equal(7, grid.Values[1, grid.Columns - 1]); // today (Monday, last column)
        Assert.Equal(7, grid.Max);                        // the out-of-window 99 was dropped
        Assert.Equal(12, SumInPeriod(grid));              // only the two in-window entries (7 + 5)
        // Today is Monday: Sunday is in-window, but Tue–Sat of this week are still in the future.
        Assert.True(grid.HasData[0, grid.Columns - 1]);
        Assert.True(grid.HasData[1, grid.Columns - 1]);
        Assert.False(grid.HasData[2, grid.Columns - 1]);
    }

    private static long SumInPeriod(Heatmap grid)
    {
        long sum = 0;
        for (int r = 0; r < grid.Rows; r++)
            for (int c = 0; c < grid.Columns; c++)
                if (grid.HasData[r, c])
                    sum += grid.Values[r, c];
        return sum;
    }

    [Fact]
    public void Year_grid_places_detail_on_its_day_of_month_row()
    {
        var detail = new[] { Entry(Local(2026, 6, 15, 12), 5) };

        var grid = XpHistoryGraph.Build(GraphScope.Year, detail, Array.Empty<HistoryArchiveMonth>(), Now);

        Assert.Equal(12, grid.Columns);
        Assert.Equal(31, grid.Rows);
        Assert.Equal("Jun", grid.ColumnLabels[11]);
        Assert.Equal(5, grid.Values[14, 11]);   // 2026-06 (last col), day 15 (row 14)
    }

    [Fact]
    public void Year_grid_uses_archive_per_day_breakdown_and_masks_short_months()
    {
        var archive = new[]
        {
            Archived("2026-01", totalXp: 40, xpByDay: new() { [3] = 40 }),  // col 6, day 3 → row 2
        };

        var grid = XpHistoryGraph.Build(GraphScope.Year, Array.Empty<HistoryEntry>(), archive, Now);

        Assert.Equal(40, grid.Values[2, 6]);
        Assert.Equal(40, grid.Max);
        // 2026-02 (col 7) has 28 days: day 28 valid, day 29 out of period.
        Assert.True(grid.HasData[27, 7]);
        Assert.False(grid.HasData[28, 7]);
        Assert.False(grid.HasData[30, 7]);
    }

    [Fact]
    public void Year_grid_falls_back_to_average_for_legacy_archive_without_per_day()
    {
        var archive = new[] { Archived("2026-01", totalXp: 62) };   // 31 days → round(62/31)=2 per day

        var grid = XpHistoryGraph.Build(GraphScope.Year, Array.Empty<HistoryEntry>(), archive, Now);

        Assert.Equal(2, grid.Values[0, 6]);
        Assert.Equal(2, grid.Values[30, 6]);   // day 31 valid for January
    }

    [Fact]
    public void All_spans_from_earliest_month_to_now()
    {
        var detail = new[] { Entry(Local(2026, 6, 15, 12), 5) };
        var archive = new[] { Archived("2026-03", totalXp: 7, xpByDay: new() { [1] = 7 }) };

        var grid = XpHistoryGraph.Build(GraphScope.All, detail, archive, Now);

        Assert.Equal(4, grid.Columns);    // 2026-03, 04, 05, 06
        Assert.Equal("2026-03", grid.ColumnLabels[0]);
        Assert.Equal("2026-06", grid.ColumnLabels[3]);
        Assert.Equal(7, grid.Values[0, 0]);    // 2026-03 day 1
        Assert.Equal(5, grid.Values[14, 3]);   // 2026-06 day 15
    }

    [Fact]
    public void Completed_metric_counts_completions_per_cell()
    {
        var detail = new[] { CompletedEntry(Local(2026, 6, 15, 13), 3) };   // today, 12–16 block (row 3)

        var grid = XpHistoryGraph.Build(GraphScope.Week, detail, Array.Empty<HistoryArchiveMonth>(), Now, GraphMetric.Completed);

        Assert.Equal(3, grid.Values[3, 6]);
        Assert.Equal(3, grid.Max);
    }

    [Fact]
    public void Completed_metric_uses_archive_completed_by_day_for_monthly_scopes()
    {
        var archive = new[]
        {
            Archived("2026-03", totalXp: 70, completed: 7, completedByDay: new() { [10] = 7 }),
        };

        var grid = XpHistoryGraph.Build(GraphScope.All, Array.Empty<HistoryEntry>(), archive, Now, GraphMetric.Completed);

        Assert.Equal(7, grid.Values[9, 0]);   // 2026-03 (col 0), day 10 (row 9): CompletedCount, not XP
        Assert.Equal(7, grid.Max);
    }
}
