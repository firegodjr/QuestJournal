namespace QuestJournal.Core.ChangeTracking;

public static class XpBucket
{
    public static long Roll(long priorTodayXp, string priorDate, string todayKey, long xpAwarded)
    {
        var carry = string.Equals(priorDate, todayKey, StringComparison.Ordinal) ? priorTodayXp : 0;
        return carry + xpAwarded;
    }

    public static string TodayKey() => DateTime.Now.ToString("yyyy-MM-dd");
}
