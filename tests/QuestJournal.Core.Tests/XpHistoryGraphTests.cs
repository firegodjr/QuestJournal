using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.Tests;

public class XpHistoryGraphTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

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

    private static HistoryArchiveMonth Archived(string month, long totalXp, long completed = 0) =>
        new() { Month = month, TotalXp = totalXp, CompletedCount = completed };

    [Fact]
    public void Week_buckets_detail_by_local_day_today_last()
    {
        var detail = new[]
        {
            Entry(Now, 10),                 // today → last bucket
            Entry(Now.AddDays(-2), 3),      // two days ago
        };

        var bars = XpHistoryGraph.Build(GraphScope.Week, detail, Array.Empty<HistoryArchiveMonth>(), Now);

        Assert.Equal(7, bars.Count);
        Assert.Equal(10, bars[6].Value);
        Assert.Equal(3, bars[4].Value);
        Assert.Equal(0, bars[0].Value);
    }

    [Fact]
    public void Year_merges_detail_and_archive_into_twelve_months()
    {
        var detail = new[] { Entry(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero), 5) };
        var archive = new[] { Archived("2026-01", 40) };

        var bars = XpHistoryGraph.Build(GraphScope.Year, detail, archive, Now);

        Assert.Equal(12, bars.Count);
        Assert.Equal(40, bars[6].Value);   // 2026-01 (start 2025-07 → index 6)
        Assert.Equal(5, bars[11].Value);   // 2026-06 (current month → last)
        Assert.Equal(45, bars.Sum(b => b.Value));
    }

    [Fact]
    public void All_spans_from_earliest_month_to_now()
    {
        var detail = new[] { Entry(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero), 5) };
        var archive = new[] { Archived("2026-03", 7) };

        var bars = XpHistoryGraph.Build(GraphScope.All, detail, archive, Now);

        Assert.Equal(4, bars.Count);    // 2026-03, 04, 05, 06
        Assert.Equal("2026-03", bars[0].Label);
        Assert.Equal(7, bars[0].Value);
        Assert.Equal("2026-06", bars[3].Label);
        Assert.Equal(5, bars[3].Value);
    }

    [Fact]
    public void Completed_metric_counts_completions_per_bucket()
    {
        var detail = new[] { CompletedEntry(Now, 3) };   // three tasks completed today

        var bars = XpHistoryGraph.Build(GraphScope.Week, detail, Array.Empty<HistoryArchiveMonth>(), Now, GraphMetric.Completed);

        Assert.Equal(3, bars[6].Value);
        Assert.Equal(0, bars[0].Value);
    }

    [Fact]
    public void Completed_metric_uses_archive_completed_count_for_monthly_scopes()
    {
        var archive = new[] { Archived("2026-03", totalXp: 70, completed: 7) };

        var bars = XpHistoryGraph.Build(GraphScope.All, Array.Empty<HistoryEntry>(), archive, Now, GraphMetric.Completed);

        Assert.Equal("2026-03", bars[0].Label);
        Assert.Equal(7, bars[0].Value);   // archive CompletedCount, not TotalXp
    }
}
