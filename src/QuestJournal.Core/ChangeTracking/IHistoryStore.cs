namespace QuestJournal.Core.ChangeTracking;

public interface IHistoryStore
{
    string HistoryPath { get; }
    IReadOnlyList<HistoryEntry> LoadAll();
    void Append(HistoryEntry entry);
}
