namespace QuestJournal.Core.Model;

public sealed record CategorySection(
    string Name,
    IReadOnlyList<Quest> TopLevelQuests,
    int LineNumber);
