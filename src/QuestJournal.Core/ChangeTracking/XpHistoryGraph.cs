using System.Globalization;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

/// <summary>Time window for a history graph.</summary>
public enum GraphScope
{
    Week,
    Month,
    Year,
    All,
}

/// <summary>Which quantity a history graph plots.</summary>
public enum GraphMetric
{
    /// <summary>XP earned per bucket.</summary>
    Xp,

    /// <summary>Tasks that became Completed per bucket.</summary>
    Completed,
}

/// <summary>One plotted bucket: a display label and its value.</summary>
public readonly record struct GraphBar(string Label, long Value);

/// <summary>
/// Buckets a metric from the detailed <see cref="HistoryStore"/> log (and, for monthly scopes,
/// the compacted <see cref="HistoryArchiveStore"/>) into an ordered series for charting. All
/// bucketing is by local time so day/month boundaries match the user's wall clock.
/// </summary>
public static class XpHistoryGraph
{
    public static IReadOnlyList<GraphBar> Build(
        GraphScope scope,
        IReadOnlyList<HistoryEntry> detail,
        IReadOnlyList<HistoryArchiveMonth> archive,
        DateTimeOffset now,
        GraphMetric metric = GraphMetric.Xp)
    {
        Func<HistoryEntry, long> entryValue = metric == GraphMetric.Completed
            ? CompletedIn
            : e => e.XpAwarded;
        Func<HistoryArchiveMonth, long> monthValue = metric == GraphMetric.Completed
            ? m => m.CompletedCount
            : m => m.TotalXp;

        return scope switch
        {
            GraphScope.Week => DailyBars(detail, entryValue, now, days: 7, labelFormat: "ddd dd"),
            GraphScope.Month => DailyBars(detail, entryValue, now, days: 30, labelFormat: "MM-dd"),
            GraphScope.Year => MonthlyBars(detail, archive, entryValue, monthValue, EarliestMonth(now, monthsBack: 11), now, labelFormat: "MMM"),
            GraphScope.All => MonthlyBars(detail, archive, entryValue, monthValue, AllStartMonth(detail, archive, now), now, labelFormat: "yyyy-MM"),
            _ => Array.Empty<GraphBar>(),
        };
    }

    /// <summary>Tasks that became Completed in a batch: Added-as-Completed plus StatusChanged→Completed.</summary>
    public static long CompletedIn(HistoryEntry entry) =>
        entry.Changes.Count(c =>
            (c.Kind == nameof(Change.Added) && c.Status == QuestStatus.Completed) ||
            (c.Kind == nameof(Change.StatusChanged) && c.NewStatus == QuestStatus.Completed));

    private static List<GraphBar> DailyBars(
        IReadOnlyList<HistoryEntry> detail, Func<HistoryEntry, long> value,
        DateTimeOffset now, int days, string labelFormat)
    {
        var byDay = detail
            .GroupBy(e => DateOnly.FromDateTime(e.Timestamp.ToLocalTime().DateTime))
            .ToDictionary(g => g.Key, g => g.Sum(value));

        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
        var bars = new List<GraphBar>(days);
        for (int i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            byDay.TryGetValue(day, out var v);
            bars.Add(new GraphBar(day.ToString(labelFormat, CultureInfo.InvariantCulture), v));
        }
        return bars;
    }

    private static List<GraphBar> MonthlyBars(
        IReadOnlyList<HistoryEntry> detail,
        IReadOnlyList<HistoryArchiveMonth> archive,
        Func<HistoryEntry, long> entryValue,
        Func<HistoryArchiveMonth, long> monthValue,
        DateOnly start,
        DateTimeOffset now,
        string labelFormat)
    {
        var detailByMonth = detail
            .GroupBy(e => MonthCompactor.MonthKey(e.Timestamp))
            .ToDictionary(g => g.Key, g => g.Sum(entryValue), StringComparer.Ordinal);

        var archiveByMonth = archive
            .GroupBy(m => m.Month, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(monthValue), StringComparer.Ordinal);

        var end = new DateOnly(now.ToLocalTime().Year, now.ToLocalTime().Month, 1);
        var bars = new List<GraphBar>();
        for (var month = new DateOnly(start.Year, start.Month, 1); month <= end; month = month.AddMonths(1))
        {
            var key = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            long v = 0;
            if (detailByMonth.TryGetValue(key, out var d)) v += d;
            if (archiveByMonth.TryGetValue(key, out var a)) v += a;
            bars.Add(new GraphBar(month.ToString(labelFormat, CultureInfo.InvariantCulture), v));
        }
        return bars;
    }

    private static DateOnly EarliestMonth(DateTimeOffset now, int monthsBack)
    {
        var local = now.ToLocalTime();
        return new DateOnly(local.Year, local.Month, 1).AddMonths(-monthsBack);
    }

    private static DateOnly AllStartMonth(
        IReadOnlyList<HistoryEntry> detail, IReadOnlyList<HistoryArchiveMonth> archive, DateTimeOffset now)
    {
        var keys = detail.Select(e => MonthCompactor.MonthKey(e.Timestamp))
            .Concat(archive.Select(m => m.Month))
            .Where(k => DateOnly.TryParseExact(k + "-01", "yyyy-MM-dd", out _));

        DateOnly? earliest = null;
        foreach (var key in keys)
        {
            if (DateOnly.TryParseExact(key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                && (earliest is null || d < earliest))
            {
                earliest = d;
            }
        }

        var local = now.ToLocalTime();
        return earliest ?? new DateOnly(local.Year, local.Month, 1);
    }
}
