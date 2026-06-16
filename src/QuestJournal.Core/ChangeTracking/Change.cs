using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

public abstract record Change(TaskKey Key)
{
    public sealed record Added(TaskKey Key, QuestStatus Status) : Change(Key);

    public sealed record Removed(TaskKey Key, QuestStatus LastStatus) : Change(Key);

    public sealed record StatusChanged(TaskKey Key, QuestStatus OldStatus, QuestStatus NewStatus) : Change(Key);

    public sealed record Moved(TaskKey From, TaskKey To, QuestStatus Status) : Change(To);
}
