using System.Globalization;

namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// The scope-specific skeleton of a heatmap: its axis labels, its out-of-period mask, and the
/// rule that places a timestamp into a cell. It carries no values, so it is shared by both the
/// XP/Completed graph (<see cref="XpHistoryGraph"/>) and the commit graph
/// (<see cref="CommitHistoryGraph"/>) — they bucket identically and differ only in what they
/// accumulate. All placement is by local time so day/month boundaries match the wall clock.
/// </summary>
public sealed class HeatmapLayout
{
    /// <summary>Number of hour-blocks shown for week/month scopes is derived from the span.</summary>
    private readonly Func<DateTimeOffset, (int Row, int Col)?> _locator;

    private HeatmapLayout(
        GraphScope scope,
        IReadOnlyList<string> columnLabels,
        IReadOnlyList<string> rowLabels,
        bool[,] hasData,
        Func<DateTimeOffset, (int Row, int Col)?> locator,
        IReadOnlyDictionary<string, int>? monthIndex = null,
        IReadOnlyList<DateOnly>? monthDates = null)
    {
        Scope = scope;
        ColumnLabels = columnLabels;
        RowLabels = rowLabels;
        HasData = hasData;
        _locator = locator;
        MonthIndex = monthIndex;
        MonthDates = monthDates;
    }

    public GraphScope Scope { get; }
    public IReadOnlyList<string> ColumnLabels { get; }
    public IReadOnlyList<string> RowLabels { get; }

    /// <summary>Whether each cell falls inside the period; <c>false</c> cells render as background.</summary>
    public bool[,] HasData { get; }

    public int Rows => RowLabels.Count;
    public int Columns => ColumnLabels.Count;

    /// <summary>For month-day layouts (Year/All): month-key <c>yyyy-MM</c> → column. Null otherwise.</summary>
    public IReadOnlyDictionary<string, int>? MonthIndex { get; }

    /// <summary>For month-day layouts: the first-of-month date backing each column. Null otherwise.</summary>
    public IReadOnlyList<DateOnly>? MonthDates { get; }

    /// <summary>
    /// Places <paramref name="ts"/> into a cell. Returns <c>false</c> when the timestamp falls
    /// outside the displayed window or lands on a masked (out-of-period) cell.
    /// </summary>
    public bool TryLocate(DateTimeOffset ts, out int row, out int col)
    {
        var hit = _locator(ts);
        if (hit is { } h && h.Row >= 0 && h.Row < Rows && h.Col >= 0 && h.Col < Columns && HasData[h.Row, h.Col])
        {
            row = h.Row;
            col = h.Col;
            return true;
        }
        row = -1;
        col = -1;
        return false;
    }

    /// <summary>
    /// The earliest local date the scope displays, used to derive a git <c>--since</c> bound.
    /// Null for <see cref="GraphScope.All"/> (no lower bound — fetch everything).
    /// </summary>
    public static DateOnly? WindowStartFor(GraphScope scope, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
        return scope switch
        {
            GraphScope.Week => today.AddDays(-6),
            GraphScope.Month => today.AddMonths(-3),
            GraphScope.Year => new DateOnly(today.Year, today.Month, 1).AddMonths(-11),
            _ => null,
        };
    }

    /// <summary>
    /// Days on the x-axis, hour-of-day blocks on the y-axis. Rows span
    /// <paramref name="startHour"/>..<paramref name="endHour"/> in <paramref name="blockHours"/>
    /// steps; timestamps whose local hour falls outside that span have no row and are dropped.
    /// </summary>
    public static HeatmapLayout ForDayHour(
        GraphScope scope, DateTimeOffset now, int days, string labelFormat,
        int startHour, int endHour, int blockHours)
    {
        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
        var rows = (endHour - startHour) / blockHours;

        var columnLabels = new List<string>(days);
        var dayIndex = new Dictionary<DateOnly, int>(days);
        for (int i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            dayIndex[day] = columnLabels.Count;
            columnLabels.Add(day.ToString(labelFormat, CultureInfo.InvariantCulture));
        }

        var rowLabels = Enumerable.Range(0, rows)
            .Select(i => (startHour + i * blockHours).ToString("00", CultureInfo.InvariantCulture))
            .ToArray();

        var hasData = new bool[rows, days];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < days; c++)
                hasData[r, c] = true;

        (int, int)? Locate(DateTimeOffset ts)
        {
            var local = ts.ToLocalTime();
            var day = DateOnly.FromDateTime(local.DateTime);
            if (!dayIndex.TryGetValue(day, out var col) || local.Hour < startHour || local.Hour >= endHour)
            {
                return null;
            }
            return ((local.Hour - startHour) / blockHours, col);
        }

        return new HeatmapLayout(scope, columnLabels, rowLabels, hasData, Locate);
    }

    /// <summary>
    /// GitHub-style calendar: weeks on the x-axis, day-of-week (Sunday→Saturday) on the y-axis,
    /// over the trailing <paramref name="monthsBack"/> calendar months. The first column snaps
    /// back to its Sunday; lead-in days and future days are masked.
    /// </summary>
    public static HeatmapLayout ForMonthCalendar(GraphScope scope, DateTimeOffset now, int monthsBack)
    {
        const int Rows = 7; // Sunday..Saturday
        var rowLabels = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
        var windowStart = today.AddMonths(-monthsBack);
        var gridStart = windowStart.AddDays(-(int)windowStart.DayOfWeek); // back to Sunday
        var columns = (today.DayNumber - gridStart.DayNumber) / 7 + 1;

        var hasData = new bool[Rows, columns];
        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < Rows; row++)
            {
                var date = gridStart.AddDays(col * 7 + row);
                hasData[row, col] = date >= windowStart && date <= today;
            }
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

        (int, int)? Locate(DateTimeOffset ts)
        {
            var date = DateOnly.FromDateTime(ts.ToLocalTime().DateTime);
            if (date < windowStart || date > today)
            {
                return null;
            }
            var col = (date.DayNumber - gridStart.DayNumber) / 7;
            return ((int)date.DayOfWeek, col);
        }

        return new HeatmapLayout(scope, columnLabels, rowLabels, hasData, Locate);
    }

    /// <summary>
    /// Months on the x-axis (from <paramref name="start"/>'s month through the current month),
    /// day-of-month (1–31) on the y-axis. Day-rows beyond a month's length are masked. Exposes
    /// <see cref="MonthIndex"/>/<see cref="MonthDates"/> so callers can merge month-keyed data.
    /// </summary>
    public static HeatmapLayout ForMonthDay(GraphScope scope, DateOnly start, DateTimeOffset now, string labelFormat)
    {
        var local = now.ToLocalTime();
        var end = new DateOnly(local.Year, local.Month, 1);

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
        var hasData = new bool[Days, columnLabels.Count];
        for (int col = 0; col < monthDates.Count; col++)
        {
            var inMonth = DateTime.DaysInMonth(monthDates[col].Year, monthDates[col].Month);
            for (int day = 1; day <= inMonth; day++)
            {
                hasData[day - 1, col] = true;
            }
        }

        (int, int)? Locate(DateTimeOffset ts)
        {
            var when = ts.ToLocalTime();
            var key = when.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (!monthIndex.TryGetValue(key, out var col))
            {
                return null;
            }
            return (when.Day - 1, col);
        }

        return new HeatmapLayout(scope, columnLabels, rowLabels, hasData, Locate, monthIndex, monthDates);
    }
}
