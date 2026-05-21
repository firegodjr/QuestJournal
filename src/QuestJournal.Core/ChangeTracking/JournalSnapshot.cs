using System.Collections.Immutable;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

public sealed class JournalSnapshot
{
    public int Version { get; set; } = 1;
    public string JournalPath { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; }
    public long TotalXp { get; set; }
    public long TodayXp { get; set; }
    public string TodayDate { get; set; } = string.Empty;
    public List<SnapshotTask> Tasks { get; set; } = new();

    public static JournalSnapshot FromDocument(JournalDocument doc, string journalPath, long totalXp)
        => FromDocument(doc, journalPath, totalXp, todayXp: 0, todayDate: string.Empty);

    public static JournalSnapshot FromDocument(
        JournalDocument doc,
        string journalPath,
        long totalXp,
        long todayXp,
        string todayDate)
    {
        var snapshot = new JournalSnapshot
        {
            JournalPath = journalPath,
            CapturedAt = DateTimeOffset.UtcNow,
            TotalXp = totalXp,
            TodayXp = todayXp,
            TodayDate = todayDate,
        };

        foreach (var (key, status) in EnumerateTasks(doc))
        {
            snapshot.Tasks.Add(new SnapshotTask
            {
                Day = key.Day,
                Category = key.Category,
                Ancestors = key.Ancestors.ToList(),
                Text = key.Text,
                Status = status,
            });
        }

        return snapshot;
    }

    public static IEnumerable<(TaskKey Key, QuestStatus Status)> EnumerateTasks(JournalDocument doc)
    {
        foreach (var day in doc.Days)
        {
            foreach (var category in day.Categories)
            {
                foreach (var top in category.TopLevelQuests)
                {
                    foreach (var item in Walk(day.Name, category.Name, ImmutableArray<string>.Empty, top))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    private static IEnumerable<(TaskKey Key, QuestStatus Status)> Walk(
        string day,
        string category,
        ImmutableArray<string> ancestors,
        Quest quest)
    {
        var text = quest.Text.Trim();
        yield return (new TaskKey(day, category, ancestors, text), quest.Status);

        var nextAncestors = ancestors.Add(text);
        foreach (var child in quest.Children)
        {
            foreach (var item in Walk(day, category, nextAncestors, child))
            {
                yield return item;
            }
        }
    }
}

public sealed class SnapshotTask
{
    public string Day { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Ancestors { get; set; } = new();
    public string Text { get; set; } = string.Empty;
    public QuestStatus Status { get; set; }

    public TaskKey ToKey() => new(
        Day,
        Category,
        Ancestors.ToImmutableArray(),
        Text);
}
