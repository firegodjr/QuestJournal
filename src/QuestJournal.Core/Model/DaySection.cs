namespace QuestJournal.Core.Model;

public sealed record DaySection(
    string Name,
    IReadOnlyList<CategorySection> Categories,
    int LineNumber);
