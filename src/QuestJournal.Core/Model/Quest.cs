namespace QuestJournal.Core.Model;

public sealed record Quest(
    QuestStatus Status,
    string Text,
    IReadOnlyList<Quest> Children,
    int IndentDepth,
    int LineNumber);
