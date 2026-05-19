namespace QuestJournal.Core.Model;

public sealed record JournalDocument(
    IReadOnlyList<DaySection> Days,
    IReadOnlyList<string> FrontmatterLines);
