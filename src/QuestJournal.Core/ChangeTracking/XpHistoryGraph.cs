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

/// <summary>
/// Buckets a metric from the detailed <see cref="HistoryStore"/> log (and, for monthly scopes,
/// the compacted <see cref="HistoryArchiveStore"/>) into a 2D <see cref="Heatmap"/>. The grid's
/// shape is supplied by a shared <see cref="HeatmapLayout"/>; this type only accumulates values
/// into it. The scope's primary subdivision is the x-axis and the next-finer subdivision is the
/// y-axis: week → days × 2-hour blocks (07–19); month → weeks × day-of-week; year/all → months ×
/// day-of-month. All bucketing is by local time.
/// </summary>
public static class XpHistoryGraph
{
    private const int Days = 31;

    public static Heatmap Build(
        GraphScope scope,
        IReadOnlyList<HistoryEntry> detail,
        IReadOnlyList<HistoryArchiveMonth> archive,
        DateTimeOffset now,
        GraphMetric metric = GraphMetric.Xp)
    {
        Func<HistoryEntry, long> entryValue = metric == GraphMetric.Completed
            ? CompletedIn
            : e => e.XpAwarded;

        var layout = scope switch
        {
            GraphScope.Week => HeatmapLayout.ForDayHour(scope, now, days: 7, labelFormat: "dd", startHour: 7, endHour: 19, blockHours: 2),
            GraphScope.Month => HeatmapLayout.ForMonthCalendar(scope, now, monthsBack: 3),
            GraphScope.Year => HeatmapLayout.ForMonthDay(scope, EarliestMonth(now, monthsBack: 11), now, labelFormat: "MMM"),
            GraphScope.All => HeatmapLayout.ForMonthDay(scope, AllStartMonth(detail, archive, now), now, labelFormat: "yyyy-MM"),
            _ => null,
        };

        if (layout is null)
        {
            return Empty(scope, metric);
        }

        var values = new long[layout.Rows, layout.Columns];
        foreach (var entry in detail)
        {
            if (layout.TryLocate(entry.Timestamp, out var row, out var col))
            {
                values[row, col] += entryValue(entry);
            }
        }

        // Year/All: merge compacted archive months against the same month-day layout.
        if (layout.MonthIndex is not null && layout.MonthDates is not null)
        {
            MergeArchive(metric, archive, layout, values);
        }

        return Finish(scope, metric, layout.ColumnLabels, layout.RowLabels, values, layout.HasData);
    }

    /// <summary>Tasks that became Completed in a batch: Added-as-Completed plus StatusChanged→Completed.</summary>
    public static long CompletedIn(HistoryEntry entry) =>
        entry.Changes.Count(c =>
            (c.Kind == nameof(Change.Added) && c.Status == QuestStatus.Completed) ||
            (c.Kind == nameof(Change.StatusChanged) && c.NewStatus == QuestStatus.Completed));

    /// <summary>
    /// Adds archived months into a month-day grid: the retained per-day breakdown when present,
    /// else the stored monthly total spread evenly across the month's days (legacy fallback).
    /// </summary>
    private static void MergeArchive(
        GraphMetric metric, IReadOnlyList<HistoryArchiveMonth> archive,
        HeatmapLayout layout, long[,] values)
    {
        var monthIndex = layout.MonthIndex!;
        var monthDates = layout.MonthDates!;

        foreach (var month in archive)
        {
            if (!monthIndex.TryGetValue(month.Month, out var col))
            {
                continue;
            }

            var perDay = metric == GraphMetric.Completed ? month.CompletedByDay : month.XpByDay;
            if (perDay.Count > 0)
            {
                foreach (var (day, v) in perDay)
                {
                    if (day is >= 1 and <= Days && layout.HasData[day - 1, col])
                    {
                        values[day - 1, col] += v;
                    }
                }
            }
            else
            {
                // Legacy archive line without a per-day breakdown: spread the monthly total
                // evenly across the month's days as a per-day average.
                var inMonth = DateTime.DaysInMonth(monthDates[col].Year, monthDates[col].Month);
                var total = metric == GraphMetric.Completed ? month.CompletedCount : month.TotalXp;
                var average = (long)Math.Round((double)total / inMonth, MidpointRounding.AwayFromZero);
                if (average != 0)
                {
                    for (int day = 1; day <= inMonth; day++)
                    {
                        values[day - 1, col] += average;
                    }
                }
            }
        }
    }

    private static Heatmap Finish(
        GraphScope scope, GraphMetric metric,
        IReadOnlyList<string> columnLabels, IReadOnlyList<string> rowLabels,
        long[,] values, bool[,] hasData)
    {
        long max = 0;
        for (int r = 0; r < values.GetLength(0); r++)
            for (int c = 0; c < values.GetLength(1); c++)
                if (hasData[r, c] && values[r, c] > max)
                    max = values[r, c];

        return new Heatmap
        {
            Scope = scope,
            Metric = metric,
            ColumnLabels = columnLabels,
            RowLabels = rowLabels,
            Values = values,
            HasData = hasData,
            Max = max,
        };
    }

    private static Heatmap Empty(GraphScope scope, GraphMetric metric) => new()
    {
        Scope = scope,
        Metric = metric,
        ColumnLabels = Array.Empty<string>(),
        RowLabels = Array.Empty<string>(),
        Values = new long[0, 0],
        HasData = new bool[0, 0],
        Max = 0,
    };

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
