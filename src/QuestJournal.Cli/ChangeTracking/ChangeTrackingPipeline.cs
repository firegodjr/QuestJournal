using QuestJournal.Cli.Rendering;
using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;

namespace QuestJournal.Cli.ChangeTracking;

public sealed class ChangeTrackingPipeline
{
    private readonly SnapshotStore _store;
    private readonly HistoryStore _history;
    private readonly ChangeDetector _detector;
    private readonly DiffRenderer _renderer;

    public ChangeTrackingPipeline(QuestTheme theme, SnapshotStore? store = null, HistoryStore? history = null)
    {
        _store = store ?? new SnapshotStore();
        _history = history ?? new HistoryStore();
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

            if (changes.Changes.Count > 0 || changes.Moves.Count > 0)
            {
                _history.Append(BuildHistoryEntry(changes, journalPath, xpAwarded));
            }
        }

        return new PipelineResult(xpAwarded, newTodayXp, newTotal, HasChanges: !changes.IsEmpty);
    }

    public void RenderXpFooter(PipelineResult result) =>
        _renderer.RenderXpFooter(result.XpAwarded, result.TodayXp, result.TotalXp);

    private static HistoryEntry BuildHistoryEntry(ChangeSet changes, string journalPath, long xpAwarded)
    {
        var entry = new HistoryEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            JournalPath = journalPath,
            XpAwarded = xpAwarded,
        };

        foreach (var change in changes.Changes)
        {
            var hc = new HistoryChange
            {
                Day = change.Key.Day,
                Category = change.Key.Category,
                Ancestors = change.Key.Ancestors.ToList(),
                Text = change.Key.Text,
            };

            switch (change)
            {
                case Change.Added a:
                    hc.Kind = nameof(Change.Added);
                    hc.Status = a.Status;
                    break;
                case Change.Removed r:
                    hc.Kind = nameof(Change.Removed);
                    hc.Status = r.LastStatus;
                    break;
                case Change.StatusChanged sc:
                    hc.Kind = nameof(Change.StatusChanged);
                    hc.OldStatus = sc.OldStatus;
                    hc.NewStatus = sc.NewStatus;
                    break;
            }

            entry.Changes.Add(hc);
        }

        foreach (var move in changes.Moves.OfType<Change.Moved>())
        {
            entry.Moves.Add(new HistoryMove
            {
                Text = move.To.Text,
                Status = move.Status,
                FromDay = move.From.Day,
                FromCategory = move.From.Category,
                FromAncestors = move.From.Ancestors.ToList(),
                ToDay = move.To.Day,
                ToCategory = move.To.Category,
                ToAncestors = move.To.Ancestors.ToList(),
            });
        }

        return entry;
    }
}

public sealed record PipelineResult(long XpAwarded, long TodayXp, long TotalXp, bool HasChanges);
