using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// One durable, timestamped record of a single change batch (the diffs and moves
/// detected on one <c>quest</c> invocation). Plain DTOs are used instead of the
/// polymorphic <see cref="Change"/> hierarchy to keep JSON serialization simple.
/// </summary>
public sealed class HistoryEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string JournalPath { get; set; } = string.Empty;
    public long XpAwarded { get; set; }
    public List<HistoryChange> Changes { get; set; } = new();
    public List<HistoryMove> Moves { get; set; } = new();
}

public sealed class HistoryChange
{
    /// <summary>One of <c>Added</c>, <c>Removed</c>, <c>StatusChanged</c>.</summary>
    public string Kind { get; set; } = string.Empty;
    public string Day { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Ancestors { get; set; } = new();
    public string Text { get; set; } = string.Empty;

    /// <summary>Current status for Added; last status for Removed.</summary>
    public QuestStatus Status { get; set; }
    public QuestStatus OldStatus { get; set; }
    public QuestStatus NewStatus { get; set; }
}

public sealed class HistoryMove
{
    public string Text { get; set; } = string.Empty;
    public QuestStatus Status { get; set; }
    public string FromDay { get; set; } = string.Empty;
    public string FromCategory { get; set; } = string.Empty;
    public List<string> FromAncestors { get; set; } = new();
    public string ToDay { get; set; } = string.Empty;
    public string ToCategory { get; set; } = string.Empty;
    public List<string> ToAncestors { get; set; } = new();
}
