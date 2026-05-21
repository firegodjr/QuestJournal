using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

public sealed class ChangeDetector
{
    private const string PathDelim = "\u001F";

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

        CollapseYesterdayLandings(changes);

        return new ChangeSet(changes);
    }

    private static void CollapseYesterdayLandings(List<Change> changes)
    {
        var removedByPath = new Dictionary<string, int>();
        for (int i = 0; i < changes.Count; i++)
        {
            if (changes[i] is Change.Removed r)
            {
                var fp = PathFingerprint(r.Key);
                if (!removedByPath.ContainsKey(fp))
                {
                    removedByPath[fp] = i;
                }
            }
        }

        if (removedByPath.Count == 0) return;

        var toDelete = new HashSet<int>();
        for (int i = 0; i < changes.Count; i++)
        {
            if (changes[i] is not Change.Added a) continue;
            if (!DayNames.IsYesterday(a.Key.Day)) continue;
            if (a.Status != QuestStatus.Completed && a.Status != QuestStatus.Cancelled) continue;

            var fp = PathFingerprint(a.Key);
            if (!removedByPath.TryGetValue(fp, out var removedIdx)) continue;
            if (toDelete.Contains(removedIdx)) continue;

            var removed = (Change.Removed)changes[removedIdx];
            if (string.Equals(removed.Key.Day, a.Key.Day, StringComparison.OrdinalIgnoreCase)) continue;
            if (removed.LastStatus == a.Status) continue;

            changes[i] = new Change.StatusChanged(a.Key, removed.LastStatus, a.Status);
            toDelete.Add(removedIdx);
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

    private static string PathFingerprint(TaskKey key)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(key.Category);
        sb.Append(PathDelim);
        foreach (var ancestor in key.Ancestors)
        {
            sb.Append(ancestor);
            sb.Append(PathDelim);
        }
        sb.Append(key.Text);
        return sb.ToString();
    }
}
