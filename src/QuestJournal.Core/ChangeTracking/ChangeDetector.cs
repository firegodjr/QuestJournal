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

        CollapseMoves(changes);

        return new ChangeSet(changes);
    }

    private static void CollapseMoves(List<Change> changes)
    {
        var removedByText = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        for (int i = 0; i < changes.Count; i++)
        {
            if (changes[i] is Change.Removed r)
            {
                if (!removedByText.TryGetValue(r.Key.Text, out var queue))
                {
                    queue = new Queue<int>();
                    removedByText[r.Key.Text] = queue;
                }
                queue.Enqueue(i);
            }
        }

        if (removedByText.Count == 0) return;

        var toDelete = new HashSet<int>();
        for (int i = 0; i < changes.Count; i++)
        {
            if (changes[i] is not Change.Added a) continue;
            if (!removedByText.TryGetValue(a.Key.Text, out var queue) || queue.Count == 0) continue;

            var removedIdx = queue.Dequeue();
            var removed = (Change.Removed)changes[removedIdx];

            if (removed.LastStatus == a.Status)
            {
                toDelete.Add(i);
                toDelete.Add(removedIdx);
            }
            else
            {
                changes[i] = new Change.StatusChanged(a.Key, removed.LastStatus, a.Status);
                toDelete.Add(removedIdx);
            }
        }

        if (toDelete.Count == 0) return;

        for (int i = changes.Count - 1; i >= 0; i--)
        {
            if (toDelete.Contains(i))
            {
                changes.RemoveAt(i);
            }
        }
    }
}
