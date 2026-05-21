using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;
using QuestJournal.Core.Parsing;

namespace QuestJournal.Core.Tests;

public class SnapshotStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"quest-snap-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_returns_null_when_file_missing()
    {
        var store = new SnapshotStore(TempPath());
        Assert.Null(store.Load());
    }

    [Fact]
    public void Roundtrip_preserves_tasks_xp_and_path()
    {
        var path = TempPath();
        try
        {
            var doc = new JournalParser().Parse(
                "# TODAY\n## MAINQUESTS\n- [ ] alpha\n\t- [x] alpha-child\n- [>] beta\n");
            var snap = JournalSnapshot.FromDocument(doc, "/abs/journal.md", totalXp: 42);

            var store = new SnapshotStore(path);
            store.Save(snap);

            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal("/abs/journal.md", loaded!.JournalPath);
            Assert.Equal(42L, loaded.TotalXp);
            Assert.Equal(snap.Tasks.Count, loaded.Tasks.Count);

            for (int i = 0; i < snap.Tasks.Count; i++)
            {
                var a = snap.Tasks[i];
                var b = loaded.Tasks[i];
                Assert.Equal(a.Day, b.Day);
                Assert.Equal(a.Category, b.Category);
                Assert.Equal(a.Text, b.Text);
                Assert.Equal(a.Status, b.Status);
                Assert.Equal(a.Ancestors, b.Ancestors);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Corrupt_json_returns_null_without_throwing()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{not json");
            var store = new SnapshotStore(path);
            Assert.Null(store.Load());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Save_creates_missing_directories()
    {
        var nested = Path.Combine(Path.GetTempPath(), $"qj-{Guid.NewGuid():N}", "nested", "state.json");
        try
        {
            var store = new SnapshotStore(nested);
            store.Save(new JournalSnapshot { JournalPath = "/x", TotalXp = 1 });
            Assert.True(File.Exists(nested));
        }
        finally
        {
            var dir = Path.GetDirectoryName(Path.GetDirectoryName(nested))!;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
