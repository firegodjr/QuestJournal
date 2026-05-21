using QuestJournal.Cli.Rendering;
using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;

namespace QuestJournal.Cli.ChangeTracking;

public sealed class ChangeTrackingPipeline
{
    private readonly SnapshotStore _store;
    private readonly ChangeDetector _detector;
    private readonly DiffRenderer _renderer;

    public ChangeTrackingPipeline(GlyphTheme theme, SnapshotStore? store = null)
    {
        _store = store ?? new SnapshotStore();
        _detector = new ChangeDetector();
        _renderer = new DiffRenderer(theme);
    }

    public void RunAfter(JournalDocument currentDoc, string journalPath, bool writeSnapshot)
    {
        var prior = _store.Load();

        var samePath = prior is not null
            && string.Equals(prior.JournalPath, journalPath, StringComparison.Ordinal);
        var basis = samePath ? prior : null;

        var changes = _detector.Detect(basis, currentDoc);

        var isFirstSnapshotForThisJournal = basis is null;
        if (!isFirstSnapshotForThisJournal)
        {
            var xpAwarded = XpCalculator.Award(changes);
            var newTotal = (prior?.TotalXp ?? 0) + xpAwarded;
            _renderer.Render(changes, xpAwarded, newTotal);

            if (writeSnapshot)
            {
                var nextSnapshot = JournalSnapshot.FromDocument(currentDoc, journalPath, newTotal);
                _store.Save(nextSnapshot);
            }
        }
        else if (writeSnapshot)
        {
            var initialTotal = prior?.TotalXp ?? 0;
            var nextSnapshot = JournalSnapshot.FromDocument(currentDoc, journalPath, initialTotal);
            _store.Save(nextSnapshot);
        }
    }
}
