using QuestJournal.Core.ChangeTracking;

namespace QuestJournal.Core.Tests;

public class XpBucketTests
{
    [Fact]
    public void Same_day_accumulates_prior_today_xp()
    {
        var result = XpBucket.Roll(priorTodayXp: 7, priorDate: "2026-05-21", todayKey: "2026-05-21", xpAwarded: 5);
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Day_rollover_resets_to_awarded_only()
    {
        var result = XpBucket.Roll(priorTodayXp: 42, priorDate: "2026-05-20", todayKey: "2026-05-21", xpAwarded: 3);
        Assert.Equal(3L, result);
    }

    [Fact]
    public void Missing_prior_date_treated_as_rollover()
    {
        var result = XpBucket.Roll(priorTodayXp: 0, priorDate: "", todayKey: "2026-05-21", xpAwarded: 10);
        Assert.Equal(10L, result);
    }

    [Fact]
    public void Same_day_with_zero_award_keeps_prior_bucket()
    {
        var result = XpBucket.Roll(priorTodayXp: 99, priorDate: "2026-05-21", todayKey: "2026-05-21", xpAwarded: 0);
        Assert.Equal(99L, result);
    }
}
