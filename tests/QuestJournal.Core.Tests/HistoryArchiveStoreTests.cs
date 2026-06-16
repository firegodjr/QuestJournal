using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.Tests;

public class HistoryArchiveStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"quest-archive-{Guid.NewGuid():N}.jsonl");

    private static HistoryArchiveMonth Month(string month, long totalXp) => new()
    {
        Month = month,
        TotalXp = totalXp,
        AverageXpPerDay = totalXp / 30d,
        FinalStates =
        {
            new ArchivedQuestState
            {
                Text = $"q-{month}",
                Day = "TODAY",
                Category = "MAINQUESTS",
                Status = QuestStatus.Completed,
            },
        },
    };

    [Fact]
    public void LoadAll_returns_empty_when_file_missing()
    {
        Assert.Empty(new HistoryArchiveStore(TempPath()).LoadAll());
    }

    [Fact]
    public void AppendMonths_roundtrips_ordered_oldest_first()
    {
        var path = TempPath();
        try
        {
            var store = new HistoryArchiveStore(path);
            store.AppendMonths(new[] { Month("2026-03", 30) });
            store.AppendMonths(new[] { Month("2026-01", 10), Month("2026-02", 20) });

            var loaded = store.LoadAll();
            Assert.Equal(new[] { "2026-01", "2026-02", "2026-03" }, loaded.Select(m => m.Month));
            Assert.Equal("q-2026-01", loaded[0].FinalStates[0].Text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void AppendMonths_dedupes_by_month_keeping_latest()
    {
        var path = TempPath();
        try
        {
            var store = new HistoryArchiveStore(path);
            store.AppendMonths(new[] { Month("2026-01", 10) });
            store.AppendMonths(new[] { Month("2026-01", 99) });

            var month = Assert.Single(store.LoadAll());
            Assert.Equal(99, month.TotalXp);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
