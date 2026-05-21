namespace QuestJournal.Core.ChangeTracking;

public static class DayNames
{
    public const string Yesterday = "YESTERDAY";

    public static bool IsYesterday(string day) =>
        string.Equals(day, Yesterday, StringComparison.OrdinalIgnoreCase);
}
