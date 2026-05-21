using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

public static class XpCalculator
{
    public static long Award(Change change) => change switch
    {
        Change.StatusChanged sc when sc.NewStatus == QuestStatus.Completed => 10,
        Change.StatusChanged sc when sc.NewStatus == QuestStatus.Cancelled => 1,
        Change.StatusChanged => 2,
        Change.Added a when a.Status == QuestStatus.Comment => 0,
        Change.Added a when a.Status == QuestStatus.Completed && DayNames.IsYesterday(a.Key.Day) => 10,
        Change.Added => 1,
        Change.Removed => 0,
        _ => 0,
    };

    public static long Award(ChangeSet changes)
    {
        long total = 0;
        foreach (var change in changes.Changes)
        {
            total += Award(change);
        }
        return total;
    }
}
