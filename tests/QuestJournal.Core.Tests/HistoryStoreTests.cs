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

            store.Append(EntryAt(t1, "first"));
            store.Append(EntryAt(t2, "second"));

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
    public void Append_keeps_four_recent_months_as_detail()
    {
        var path = TempPath();
        var archivePath = TempPath();
        try
        {
            var store = new HistoryStore(path, new HistoryArchiveStore(archivePath));

            // Five distinct local months, one batch each.
            for (int month = 1; month <= 5; month++)
            {
                store.Append(EntryAt(new DateTimeOffset(2026, month, 15, 9, 0, 0, TimeSpan.Zero), $"m{month}"));
            }

            // Months 2..5 remain as detail; month 1 was compacted out.
            var detailTexts = store.LoadAll().Select(e => e.Changes[0].Text).ToList();
            Assert.Equal(new[] { "m2", "m3", "m4", "m5" }, detailTexts);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
    }

    [Fact]
    public void Append_compacts_oldest_month_into_archive()
    {
        var path = TempPath();
        var archivePath = TempPath();
        try
        {
            var archive = new HistoryArchiveStore(archivePath);
            var store = new HistoryStore(path, archive);

            for (int month = 1; month <= 5; month++)
            {
                store.Append(EntryAt(new DateTimeOffset(2026, month, 15, 9, 0, 0, TimeSpan.Zero), $"m{month}"));
            }

            var archived = Assert.Single(archive.LoadAll());
            Assert.Equal("2026-01", archived.Month);
            Assert.Equal(10, archived.TotalXp);               // single EntryAt awards 10
            Assert.Equal(1, archived.CompletedCount);         // one StatusChanged → Completed
            Assert.Equal(10d / 31, archived.AverageXpPerDay, 5);
            var state = Assert.Single(archived.FinalStates);
            Assert.Equal("m1", state.Text);
            Assert.Equal(QuestStatus.Completed, state.Status); // StatusChanged → Completed
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
    }

    [Fact]
    public void Malformed_lines_are_skipped()
    {
        var path = TempPath();
        try
        {
            var store = new HistoryStore(path);
            store.Append(EntryAt(new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero), "good"));
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

            store.Append(entry);

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
