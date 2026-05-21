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

    public PipelineResult RunAfter(
        JournalDocument currentDoc,
        string journalPath,
        bool writeSnapshot,
        bool renderDiff = true)
    {
        var prior = _store.Load();

        var samePath = prior is not null
            && string.Equals(prior.JournalPath, journalPath, StringComparison.Ordinal);
        var basis = samePath ? prior : null;

        var changes = basis is null ? ChangeSet.Empty : _detector.Detect(basis, currentDoc);
        var xpAwarded = XpCalculator.Award(changes);
        var newTotal = (prior?.TotalXp ?? 0) + xpAwarded;

        var todayKey = XpBucket.TodayKey();
        var priorTodayXp = prior?.TodayXp ?? 0;
        var priorTodayDate = prior?.TodayDate ?? string.Empty;
        var newTodayXp = XpBucket.Roll(priorTodayXp, priorTodayDate, todayKey, xpAwarded);

        if (renderDiff)
        {
            _renderer.RenderDiffTree(changes);
        }

        if (writeSnapshot)
        {
            _store.Save(JournalSnapshot.FromDocument(
                currentDoc, journalPath, newTotal, newTodayXp, todayKey));
        }

        return new PipelineResult(xpAwarded, newTodayXp, newTotal, HasChanges: !changes.IsEmpty);
    }

    public void RenderXpFooter(PipelineResult result) =>
        _renderer.RenderXpFooter(result.XpAwarded, result.TodayXp, result.TotalXp);
}

public sealed record PipelineResult(long XpAwarded, long TodayXp, long TotalXp, bool HasChanges);
