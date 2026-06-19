namespace QuestJournal.Core.ChangeTracking;

public interface ISnapshotStore
{
    string SnapshotPath { get; }
    JournalSnapshot? Load();
    void Save(JournalSnapshot snapshot);
}
