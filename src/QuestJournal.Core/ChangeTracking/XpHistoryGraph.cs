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
/// the compacted <see cref="HistoryArchiveStore"/>) into a 2D <see cref="Heatmap"/>. The scope's
/// primary subdivision is the x-axis and the next-finer subdivision is the y-axis:
/// week/month → days × 4-hour blocks; year/all → months × day-of-month. All bucketing is by
/// local time so day/month boundaries match the user's wall clock.
/// </summary>
public static class XpHistoryGraph
{
    /// <summary>Number of 4-hour blocks in a day (the y-axis for week/month scopes).</summary>
    private const int HourBlocks = 6;

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

        return scope switch
        {
            GraphScope.Week => DayHourGrid(scope, metric, detail, entryValue, now, days: 7, labelFormat: "dd", startHour: 7, blockHours: 2),
            GraphScope.Month => MonthCalendarGrid(scope, metric, detail, entryValue, now, monthsBack: 3),
            GraphScope.Year => MonthDayGrid(scope, metric, detail, archive, entryValue, EarliestMonth(now, monthsBack: 11), now, labelFormat: "MMM"),
            GraphScope.All => MonthDayGrid(scope, metric, detail, archive, entryValue, AllStartMonth(detail, archive, now), now, labelFormat: "yyyy-MM"),
            _ => Empty(scope, metric),
        };
    }

    /// <summary>Tasks that became Completed in a batch: Added-as-Completed plus StatusChanged→Completed.</summary>
    public static long CompletedIn(HistoryEntry entry) =>
        entry.Changes.Count(c =>
            (c.Kind == nameof(Change.Added) && c.Status == QuestStatus.Completed) ||
            (c.Kind == nameof(Change.StatusChanged) && c.NewStatus == QuestStatus.Completed));

    /// <summary>
    /// Days on the x-axis, hour-of-day blocks on the y-axis (week: 07–19 in 2-hour steps;
    /// month: the full day in 4-hour steps). Sourced purely from the per-batch detail log,
    /// which carries exact timestamps. Entries whose local hour falls outside the displayed
    /// span are dropped (no row to place them on).
    /// </summary>
    private static Heatmap DayHourGrid(
        GraphScope scope, GraphMetric metric,
        IReadOnlyList<HistoryEntry> detail, Func<HistoryEntry, long> value,
        DateTimeOffset now, int days, string labelFormat, int startHour, int blockHours)
    {
        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);

        var columnLabels = new List<string>(days);
        var dayIndex = new Dictionary<DateOnly, int>(days);
        for (int i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            dayIndex[day] = columnLabels.Count;
            columnLabels.Add(day.ToString(labelFormat, CultureInfo.InvariantCulture));
        }

        var endHour = startHour + blockHours * HourBlocks;
        var rowLabels = Enumerable.Range(0, HourBlocks)
            .Select(i => (startHour + i * blockHours).ToString("00", CultureInfo.InvariantCulture))
            .ToArray();
        var values = new long[HourBlocks, days];
        var hasData = new bool[HourBlocks, days];
        for (int r = 0; r < HourBlocks; r++)
            for (int c = 0; c < days; c++)
                hasData[r, c] = true;

        foreach (var entry in detail)
        {
            var local = entry.Timestamp.ToLocalTime();
            var day = DateOnly.FromDateTime(local.DateTime);
            if (!dayIndex.TryGetValue(day, out var col) || local.Hour < startHour || local.Hour >= endHour)
            {
                continue;
            }
            var row = (local.Hour - startHour) / blockHours;
            values[row, col] += value(entry);
        }

        return Finish(scope, metric, columnLabels, rowLabels, values, hasData);
    }

    /// <summary>
    /// GitHub-style calendar: weeks on the x-axis, day-of-week (Sunday→Saturday) on the y-axis.
    /// Each cell is one calendar date's total over the trailing <paramref name="monthsBack"/>
    /// calendar months. The first column is snapped back to the window's Sunday so weeks align;
    /// lead-in days before the window and future days after today are masked (<c>HasData = false</c>).
    /// </summary>
    private static Heatmap MonthCalendarGrid(
        GraphScope scope, GraphMetric metric,
        IReadOnlyList<HistoryEntry> detail, Func<HistoryEntry, long> value,
        DateTimeOffset now, int monthsBack)
    {
        const int Rows = 7; // Sunday..Saturday
        var rowLabels = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
        var windowStart = today.AddMonths(-monthsBack);
        var gridStart = windowStart.AddDays(-(int)windowStart.DayOfWeek); // back to Sunday
        var columns = (today.DayNumber - gridStart.DayNumber) / 7 + 1;

        var values = new long[Rows, columns];
        var hasData = new bool[Rows, columns];
        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < Rows; row++)
            {
                var date = gridStart.AddDays(col * 7 + row);
                hasData[row, col] = date >= windowStart && date <= today;
            }
        }

        foreach (var entry in detail)
        {
            var date = DateOnly.FromDateTime(entry.Timestamp.ToLocalTime().DateTime);
            if (date < windowStart || date > today)
            {
                continue;
            }
            var col = (date.DayNumber - gridStart.DayNumber) / 7;
            values[(int)date.DayOfWeek, col] += value(entry);
        }

        // Label a week column with its month abbreviation only when the month changes.
        var columnLabels = new string[columns];
        int prevMonth = 0;
        for (int col = 0; col < columns; col++)
        {
            var weekStart = gridStart.AddDays(col * 7);
            columnLabels[col] = (col == 0 || weekStart.Month != prevMonth)
                ? weekStart.ToString("MMM", CultureInfo.InvariantCulture)
                : string.Empty;
            prevMonth = weekStart.Month;
        }

        return Finish(scope, metric, columnLabels, rowLabels, values, hasData);
    }

    /// <summary>
    /// Months on the x-axis, day-of-month (1–31) on the y-axis. Recent months come from the
    /// detail log (true per-day); archived months use the retained <see cref="HistoryArchiveMonth.XpByDay"/>/
    /// <see cref="HistoryArchiveMonth.CompletedByDay"/> breakdown, falling back to the stored
    /// per-day average for months archived before that breakdown existed.
    /// </summary>
    private static Heatmap MonthDayGrid(
        GraphScope scope, GraphMetric metric,
        IReadOnlyList<HistoryEntry> detail,
        IReadOnlyList<HistoryArchiveMonth> archive,
        Func<HistoryEntry, long> entryValue,
        DateOnly start,
        DateTimeOffset now,
        string labelFormat)
    {
        var end = new DateOnly(now.ToLocalTime().Year, now.ToLocalTime().Month, 1);

        var columnLabels = new List<string>();
        var monthIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var monthDates = new List<DateOnly>();
        for (var month = new DateOnly(start.Year, start.Month, 1); month <= end; month = month.AddMonths(1))
        {
            monthIndex[month.ToString("yyyy-MM", CultureInfo.InvariantCulture)] = columnLabels.Count;
            monthDates.Add(month);
            columnLabels.Add(month.ToString(labelFormat, CultureInfo.InvariantCulture));
        }

        const int Days = 31;
        var rowLabels = Enumerable.Range(1, Days).Select(d => d.ToString(CultureInfo.InvariantCulture)).ToArray();
        var values = new long[Days, columnLabels.Count];
        var hasData = new bool[Days, columnLabels.Count];

        // Mark which day-rows are valid for each month column.
        for (int col = 0; col < monthDates.Count; col++)
        {
            var inMonth = DateTime.DaysInMonth(monthDates[col].Year, monthDates[col].Month);
            for (int day = 1; day <= inMonth; day++)
            {
                hasData[day - 1, col] = true;
            }
        }

        // Detail entries → exact day-of-month cell.
        foreach (var entry in detail)
        {
            var local = entry.Timestamp.ToLocalTime();
            var key = MonthCompactor.MonthKey(entry.Timestamp);
            if (monthIndex.TryGetValue(key, out var col))
            {
                values[local.Day - 1, col] += entryValue(entry);
            }
        }

        // Archived months → per-day breakdown, or spread the stored average across valid days.
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
                    if (day is >= 1 and <= Days && hasData[day - 1, col])
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

        return Finish(scope, metric, columnLabels, rowLabels, values, hasData);
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
