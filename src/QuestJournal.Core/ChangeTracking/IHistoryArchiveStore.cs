namespace QuestJournal.Core.ChangeTracking;

public interface IHistoryArchiveStore
{
    string ArchivePath { get; }
    IReadOnlyList<HistoryArchiveMonth> LoadAll();
    void AppendMonths(IEnumerable<HistoryArchiveMonth> newMonths);
}
