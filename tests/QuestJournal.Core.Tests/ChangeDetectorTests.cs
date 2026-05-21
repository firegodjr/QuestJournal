using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;
using QuestJournal.Core.Parsing;

namespace QuestJournal.Core.Tests;

public class ChangeDetectorTests
{
    private static JournalDocument Parse(string md) => new JournalParser().Parse(md);

    private static JournalSnapshot SnapshotOf(JournalDocument doc) =>
        JournalSnapshot.FromDocument(doc, "/tmp/test.md", totalXp: 0);

    [Fact]
    public void Empty_prior_reports_all_tasks_as_added()
    {
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] alpha\n- [x] beta\n");
        var changes = new ChangeDetector().Detect(prior: null, current);

        Assert.Equal(2, changes.Changes.Count);
        Assert.All(changes.Changes, c => Assert.IsType<Change.Added>(c));
    }

    [Fact]
    public void Identical_documents_produce_no_changes()
    {
        var doc = Parse("# TODAY\n## MAINQUESTS\n- [ ] alpha\n- [x] beta\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(doc), doc);

        Assert.True(changes.IsEmpty);
    }

    [Fact]
    public void Status_flip_reports_single_status_change()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] alpha\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [x] alpha\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var sc = Assert.IsType<Change.StatusChanged>(change);
        Assert.Equal(QuestStatus.Open, sc.OldStatus);
        Assert.Equal(QuestStatus.Completed, sc.NewStatus);
        Assert.Equal("alpha", sc.Key.Text);
    }

    [Fact]
    public void Sub_task_added_under_unchanged_parent_is_single_added_with_ancestors()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] parent\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] parent\n\t- [ ] child\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var added = Assert.IsType<Change.Added>(change);
        Assert.Equal("child", added.Key.Text);
        Assert.Equal(new[] { "parent" }, added.Key.Ancestors.ToArray());
    }

    [Fact]
    public void Parent_text_edit_appears_as_remove_plus_add_for_parent_and_descendants()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] old name\n\t- [ ] child\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] new name\n\t- [ ] child\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        Assert.Equal(4, changes.Changes.Count);
        Assert.Equal(2, changes.Changes.OfType<Change.Added>().Count());
        Assert.Equal(2, changes.Changes.OfType<Change.Removed>().Count());
    }

    [Fact]
    public void Removed_task_carries_last_known_status()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [>] alpha\n- [ ] beta\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [>] alpha\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var removed = Assert.IsType<Change.Removed>(change);
        Assert.Equal("beta", removed.Key.Text);
        Assert.Equal(QuestStatus.Open, removed.LastStatus);
    }
}
