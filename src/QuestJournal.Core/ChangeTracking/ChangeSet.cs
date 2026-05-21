namespace QuestJournal.Core.ChangeTracking;

public sealed class ChangeSet
{
    public IReadOnlyList<Change> Changes { get; }

    public ChangeSet(IReadOnlyList<Change> changes)
    {
        Changes = changes;
    }

    public bool IsEmpty => Changes.Count == 0;

    public static ChangeSet Empty { get; } = new(Array.Empty<Change>());
}
