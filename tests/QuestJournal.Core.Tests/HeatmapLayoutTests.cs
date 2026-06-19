using QuestJournal.Core.ChangeTracking;

namespace QuestJournal.Core.Tests;

public class HeatmapLayoutTests
{
    private static readonly DateTimeOffset Now = Local(2026, 6, 15, 12);

    private static DateTimeOffset Local(int year, int month, int day, int hour)
    {
        var dt = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
    }

    [Fact]
    public void DayHour_drops_hours_outside_the_configured_span()
    {
        var layout = HeatmapLayout.ForDayHour(GraphScope.Week, Now, days: 7, labelFormat: "dd", startHour: 7, endHour: 19, blockHours: 2);

        Assert.False(layout.TryLocate(Local(2026, 6, 15, 3), out _, out _));  // before 07
        Assert.False(layout.TryLocate(Local(2026, 6, 15, 20), out _, out _)); // after 19
        Assert.True(layout.TryLocate(Local(2026, 6, 15, 12), out var row, out var col));
        Assert.Equal(2, row); // 11–13 block
        Assert.Equal(6, col); // today
    }

    [Fact]
    public void DayHour_full_day_span_keeps_night_hours()
    {
        var layout = HeatmapLayout.ForDayHour(GraphScope.Week, Now, days: 7, labelFormat: "dd", startHour: 0, endHour: 24, blockHours: 2);

        Assert.True(layout.TryLocate(Local(2026, 6, 15, 3), out var row, out _));
        Assert.Equal(1, row); // 02–04 block
    }

    [Fact]
    public void DayHour_rejects_days_outside_the_window()
    {
        var layout = HeatmapLayout.ForDayHour(GraphScope.Week, Now, days: 7, labelFormat: "dd", startHour: 0, endHour: 24, blockHours: 2);

        Assert.False(layout.TryLocate(Local(2026, 6, 1, 12), out _, out _)); // two weeks ago
    }

    [Fact]
    public void MonthDay_masks_days_beyond_a_short_month()
    {
        // Start in 2026-01 so February (28 days) is a column.
        var layout = HeatmapLayout.ForMonthDay(GraphScope.Year, new DateOnly(2026, 1, 1), Now, labelFormat: "MMM");
        var febCol = layout.MonthIndex!["2026-02"];

        Assert.True(layout.HasData[27, febCol]);   // day 28 valid
        Assert.False(layout.HasData[28, febCol]);  // day 29 masked (2026 is not a leap year)
        Assert.False(layout.HasData[30, febCol]);  // day 31 masked
    }

    [Fact]
    public void WindowStartFor_matches_each_scope()
    {
        Assert.Equal(new DateOnly(2026, 6, 9), HeatmapLayout.WindowStartFor(GraphScope.Week, Now));
        Assert.Equal(new DateOnly(2026, 3, 15), HeatmapLayout.WindowStartFor(GraphScope.Month, Now));
        Assert.Equal(new DateOnly(2025, 7, 1), HeatmapLayout.WindowStartFor(GraphScope.Year, Now));
        Assert.Null(HeatmapLayout.WindowStartFor(GraphScope.All, Now));
    }
}
