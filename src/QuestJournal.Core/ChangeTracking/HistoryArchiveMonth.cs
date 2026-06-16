using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// Long-term, compacted summary of one calendar month of history. Produced when a month
/// ages out of the detailed <see cref="HistoryStore"/> window so the raw per-batch entries
/// can be dropped while the month's XP totals and final quest states are preserved.
/// </summary>
public sealed class HistoryArchiveMonth
{
    /// <summary>Local calendar month, formatted <c>yyyy-MM</c> (e.g. <c>2026-03</c>).</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Sum of <see cref="HistoryEntry.XpAwarded"/> across the month.</summary>
    public long TotalXp { get; set; }

    /// <summary>Number of tasks that became Completed during the month.</summary>
    public long CompletedCount { get; set; }

    /// <summary><see cref="TotalXp"/> divided by the number of calendar days in the month.</summary>
    public double AverageXpPerDay { get; set; }

    /// <summary>
    /// XP earned per local day-of-month (1–31). Retained at compaction so monthly-scope
    /// heatmaps can still show true per-day cells. Empty for months archived before this
    /// breakdown was added — callers fall back to <see cref="AverageXpPerDay"/>.
    /// </summary>
    public Dictionary<int, long> XpByDay { get; set; } = new();

    /// <summary>Tasks that became Completed per local day-of-month (1–31). See <see cref="XpByDay"/>.</summary>
    public Dictionary<int, long> CompletedByDay { get; set; } = new();

    /// <summary>Final known state of every quest text touched during the month.</summary>
    public List<ArchivedQuestState> FinalStates { get; set; } = new();
}

/// <summary>The last-seen state of a single quest (identity = <see cref="Text"/>) within a month.</summary>
public sealed class ArchivedQuestState
{
    public string Text { get; set; } = string.Empty;
    public string Day { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public QuestStatus Status { get; set; }
}
