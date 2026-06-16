using System.Globalization;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// Collapses a calendar month of detailed <see cref="HistoryEntry"/> batches into a single
/// <see cref="HistoryArchiveMonth"/> summary: month XP total, average XP per calendar day, and
/// the final known state of every quest text touched that month. Quest identity is
/// <see cref="HistoryChange.Text"/> only (day/category/parent are location attributes).
/// </summary>
public static class MonthCompactor
{
    /// <summary>Local-time <c>yyyy-MM</c> key for an entry's timestamp.</summary>
    public static string MonthKey(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static HistoryArchiveMonth Compact(string monthKey, IEnumerable<HistoryEntry> entries)
    {
        var ordered = entries.OrderBy(e => e.Timestamp).ToList();

        long totalXp = 0;
        long completed = 0;
        var states = new Dictionary<string, ArchivedQuestState>(StringComparer.Ordinal);

        foreach (var entry in ordered)
        {
            totalXp += entry.XpAwarded;
            completed += XpHistoryGraph.CompletedIn(entry);

            foreach (var change in entry.Changes)
            {
                switch (change.Kind)
                {
                    case nameof(Change.Added):
                        states[change.Text] = new ArchivedQuestState
                        {
                            Text = change.Text,
                            Day = change.Day,
                            Category = change.Category,
                            Status = change.Status,
                        };
                        break;

                    case nameof(Change.StatusChanged):
                        states[change.Text] = new ArchivedQuestState
                        {
                            Text = change.Text,
                            Day = change.Day,
                            Category = change.Category,
                            Status = change.NewStatus,
                        };
                        break;

                    case nameof(Change.Removed):
                        states.Remove(change.Text);
                        break;
                }
            }

            foreach (var move in entry.Moves)
            {
                if (states.TryGetValue(move.Text, out var existing))
                {
                    existing.Day = move.ToDay;
                    existing.Category = move.ToCategory;
                }
                else
                {
                    states[move.Text] = new ArchivedQuestState
                    {
                        Text = move.Text,
                        Day = move.ToDay,
                        Category = move.ToCategory,
                        Status = move.Status,
                    };
                }
            }
        }

        var daysInMonth = DaysInMonth(monthKey);

        return new HistoryArchiveMonth
        {
            Month = monthKey,
            TotalXp = totalXp,
            CompletedCount = completed,
            AverageXpPerDay = daysInMonth > 0 ? (double)totalXp / daysInMonth : 0,
            FinalStates = states.Values
                .OrderBy(s => s.Text, StringComparer.Ordinal)
                .ToList(),
        };
    }

    private static int DaysInMonth(string monthKey)
    {
        var parts = monthKey.Split('-');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var year)
            && int.TryParse(parts[1], out var month)
            && month is >= 1 and <= 12)
        {
            return DateTime.DaysInMonth(year, month);
        }
        return 30;
    }
}
