using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

public sealed class ChangeDetector
{
    public ChangeSet Detect(JournalSnapshot? prior, JournalDocument current)
    {
        var priorByKey = new Dictionary<TaskKey, QuestStatus>();
        if (prior is not null)
        {
            foreach (var task in prior.Tasks)
            {
                priorByKey[task.ToKey()] = task.Status;
            }
        }

        var changes = new List<Change>();
        var seen = new HashSet<TaskKey>();

        foreach (var (key, status) in JournalSnapshot.EnumerateTasks(current))
        {
            if (!seen.Add(key))
            {
                continue;
            }

            if (priorByKey.TryGetValue(key, out var priorStatus))
            {
                if (priorStatus != status)
                {
                    changes.Add(new Change.StatusChanged(key, priorStatus, status));
                }
                priorByKey.Remove(key);
            }
            else
            {
                changes.Add(new Change.Added(key, status));
            }
        }

        foreach (var (key, lastStatus) in priorByKey)
        {
            changes.Add(new Change.Removed(key, lastStatus));
        }

        return new ChangeSet(changes);
    }
}
