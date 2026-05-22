namespace QuestJournal.Core.Model;

public static class QuestStatusMarks
{
    private static readonly Dictionary<string, QuestStatus> ByMark = new(StringComparer.Ordinal)
    {
        [" "] = QuestStatus.Open,
        [">"] = QuestStatus.Active,
        ["~"] = QuestStatus.Cancelled,
        ["!"] = QuestStatus.Warning,
        ["x"] = QuestStatus.Completed,
        ["X"] = QuestStatus.Completed,
    };

    private static readonly Dictionary<QuestStatus, string> CanonicalByStatus = new()
    {
        [QuestStatus.Open] = " ",
        [QuestStatus.Active] = ">",
        [QuestStatus.Cancelled] = "~",
        [QuestStatus.Warning] = "!",
        [QuestStatus.Completed] = "x",
    };

    public static QuestStatus FromMark(string mark)
        => ByMark.TryGetValue(mark, out var status) ? status : QuestStatus.Comment;

    public static string? CanonicalMark(QuestStatus status)
        => CanonicalByStatus.TryGetValue(status, out var mark) ? mark : null;
}
