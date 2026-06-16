using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.Tests;

public class HistoryStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"quest-hist-{Guid.NewGuid():N}.jsonl");

    private static HistoryEntry EntryAt(DateTimeOffset timestamp, string text = "alpha") => new()
    {
        Timestamp = timestamp,
        JournalPath = "/abs/journal.md",
        XpAwarded = 10,
        Changes =
        {
            new HistoryChange
            {
                Kind = nameof(Change.StatusChanged),
                Day = "TODAY",
                Category = "MAINQUESTS",
                Text = text,
                OldStatus = QuestStatus.Open,
                NewStatus = QuestStatus.Completed,
            },
        },
    };

    [Fact]
    public void LoadAll_returns_empty_when_file_missing()
    {
        var store = new HistoryStore(TempPath());
        Assert.Empty(store.LoadAll());
    }

    [Fact]
    public void Append_roundtrips_entries_oldest_to_newest()
    {
        var path = TempPath();
        try
        {
            var store = new HistoryStore(path);
            var t1 = new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero);
            var t2 = t1.AddHours(1);

            store.Append(EntryAt(t1, "first"), TimeSpan.FromDays(90));
            store.Append(EntryAt(t2, "second"), TimeSpan.FromDays(90));

            var loaded = store.LoadAll();
            Assert.Equal(2, loaded.Count);
            Assert.Equal("first", loaded[0].Changes[0].Text);
            Assert.Equal("second", loaded[1].Changes[0].Text);
            Assert.Equal(QuestStatus.Completed, loaded[1].Changes[0].NewStatus);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Append_prunes_entries_older_than_retention()
    {
        var path = TempPath();
        try
        {
            var store = new HistoryStore(path);
            var old = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var recent = old.AddDays(200);

            store.Append(EntryAt(old, "stale"), TimeSpan.FromDays(90));
            store.Append(EntryAt(recent, "fresh"), TimeSpan.FromDays(90));

            var loaded = store.LoadAll();
            var entry = Assert.Single(loaded);
            Assert.Equal("fresh", entry.Changes[0].Text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Malformed_lines_are_skipped()
    {
        var path = TempPath();
        try
        {
            var store = new HistoryStore(path);
            store.Append(EntryAt(new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero), "good"), TimeSpan.FromDays(90));
            File.AppendAllText(path, "{not json\n");

            var entry = Assert.Single(store.LoadAll());
            Assert.Equal("good", entry.Changes[0].Text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Append_preserves_moves()
    {
        var path = TempPath();
        try
        {
            var store = new HistoryStore(path);
            var entry = EntryAt(new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero));
            entry.Moves.Add(new HistoryMove
            {
                Text = "eta",
                Status = QuestStatus.Open,
                FromDay = "TODAY",
                FromCategory = "MAINQUESTS",
                ToDay = "TOMORROW",
                ToCategory = "MAINQUESTS",
            });

            store.Append(entry, TimeSpan.FromDays(90));

            var loaded = Assert.Single(store.LoadAll());
            var move = Assert.Single(loaded.Moves);
            Assert.Equal("eta", move.Text);
            Assert.Equal("TODAY", move.FromDay);
            Assert.Equal("TOMORROW", move.ToDay);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
