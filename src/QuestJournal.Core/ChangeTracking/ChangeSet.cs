namespace QuestJournal.Core.ChangeTracking;

public sealed class ChangeSet
{
    public IReadOnlyList<Change> Changes { get; }

    public IReadOnlyList<Change> Moves { get; }

    public ChangeSet(IReadOnlyList<Change> changes, IReadOnlyList<Change>? moves = null)
    {
        Changes = changes;
        Moves = moves ?? Array.Empty<Change>();
    }

    public bool IsEmpty => Changes.Count == 0;

    public static ChangeSet Empty { get; } = new(Array.Empty<Change>());
}
