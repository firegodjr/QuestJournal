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
    public void Parent_text_edit_only_reports_parent_change()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] old name\n\t- [ ] child\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] new name\n\t- [ ] child\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        // The child's own text is unchanged across snapshots, so it collapses out.
        // Only the parent's text edit surfaces, as a Remove + Add pair.
        Assert.Equal(2, changes.Changes.Count);
        var added = Assert.Single(changes.Changes.OfType<Change.Added>());
        var removed = Assert.Single(changes.Changes.OfType<Change.Removed>());
        Assert.Equal("new name", added.Key.Text);
        Assert.Equal("old name", removed.Key.Text);
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

    [Fact]
    public void Today_to_yesterday_completion_collapses_to_single_status_change()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] alpha\n# YESTERDAY\n## MAINQUESTS\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n# YESTERDAY\n## MAINQUESTS\n- [x] alpha\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var sc = Assert.IsType<Change.StatusChanged>(change);
        Assert.Equal("alpha", sc.Key.Text);
        Assert.Equal("YESTERDAY", sc.Key.Day);
        Assert.Equal(QuestStatus.Open, sc.OldStatus);
        Assert.Equal(QuestStatus.Completed, sc.NewStatus);
    }

    [Fact]
    public void Tomorrow_to_yesterday_completion_collapses_source_agnostic()
    {
        var prior = Parse("# TOMORROW\n## MAINQUESTS\n- [ ] epsilon\n# YESTERDAY\n## MAINQUESTS\n");
        var current = Parse("# TOMORROW\n## MAINQUESTS\n# YESTERDAY\n## MAINQUESTS\n- [x] epsilon\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var sc = Assert.IsType<Change.StatusChanged>(change);
        Assert.Equal("epsilon", sc.Key.Text);
        Assert.Equal("YESTERDAY", sc.Key.Day);
        Assert.Equal(QuestStatus.Open, sc.OldStatus);
        Assert.Equal(QuestStatus.Completed, sc.NewStatus);
    }

    [Fact]
    public void Today_to_yesterday_cancellation_collapses_to_status_change()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [>] beta\n# YESTERDAY\n## MAINQUESTS\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n# YESTERDAY\n## MAINQUESTS\n- [~] beta\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var sc = Assert.IsType<Change.StatusChanged>(change);
        Assert.Equal(QuestStatus.Active, sc.OldStatus);
        Assert.Equal(QuestStatus.Cancelled, sc.NewStatus);
    }

    [Fact]
    public void Brand_new_yesterday_completion_stays_added()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n# YESTERDAY\n## MAINQUESTS\n- [x] delta\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var added = Assert.IsType<Change.Added>(change);
        Assert.Equal("delta", added.Key.Text);
        Assert.Equal(QuestStatus.Completed, added.Status);
        Assert.Equal("YESTERDAY", added.Key.Day);
    }

    [Fact]
    public void Move_to_non_yesterday_day_with_status_change_emits_StatusChanged()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] gamma\n# TOMORROW\n## MAINQUESTS\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n# TOMORROW\n## MAINQUESTS\n- [x] gamma\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var sc = Assert.IsType<Change.StatusChanged>(change);
        Assert.Equal("gamma", sc.Key.Text);
        Assert.Equal("TOMORROW", sc.Key.Day);
        Assert.Equal(QuestStatus.Open, sc.OldStatus);
        Assert.Equal(QuestStatus.Completed, sc.NewStatus);
    }

    [Fact]
    public void Same_status_move_to_yesterday_emits_nothing()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [x] zeta\n# YESTERDAY\n## MAINQUESTS\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n# YESTERDAY\n## MAINQUESTS\n- [x] zeta\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        Assert.True(changes.IsEmpty);
    }

    [Fact]
    public void Indent_within_same_day_and_category_emits_nothing()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] parent\n- [ ] child\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] parent\n\t- [ ] child\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        Assert.True(changes.IsEmpty);
    }

    [Fact]
    public void Unindent_within_same_day_and_category_emits_nothing()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] parent\n\t- [ ] child\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] parent\n- [ ] child\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        Assert.True(changes.IsEmpty);
    }

    [Fact]
    public void Indent_with_status_change_emits_single_StatusChanged()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] parent\n- [ ] child\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] parent\n\t- [x] child\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        var change = Assert.Single(changes.Changes);
        var sc = Assert.IsType<Change.StatusChanged>(change);
        Assert.Equal("child", sc.Key.Text);
        Assert.Equal(new[] { "parent" }, sc.Key.Ancestors.ToArray());
        Assert.Equal(QuestStatus.Open, sc.OldStatus);
        Assert.Equal(QuestStatus.Completed, sc.NewStatus);
    }

    [Fact]
    public void Cross_day_move_same_status_emits_nothing()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] eta\n# TOMORROW\n## MAINQUESTS\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n# TOMORROW\n## MAINQUESTS\n- [ ] eta\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        Assert.True(changes.IsEmpty);
    }

    [Fact]
    public void Reindent_multiple_levels_emits_nothing()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] alpha\n\t- [ ] theta\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] alpha\n- [ ] beta\n\t- [ ] gamma\n\t\t- [ ] theta\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        // theta moved from depth 1 to depth 3 under a different ancestor chain.
        // beta and gamma are genuinely new.
        Assert.Equal(2, changes.Changes.Count);
        Assert.All(changes.Changes, c => Assert.IsType<Change.Added>(c));
        var texts = changes.Changes.OfType<Change.Added>().Select(a => a.Key.Text).OrderBy(t => t).ToArray();
        Assert.Equal(new[] { "beta", "gamma" }, texts);
    }

    [Fact]
    public void Cross_category_move_same_status_emits_nothing()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] iota\n## SIDEQUESTS\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n## SIDEQUESTS\n- [ ] iota\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        Assert.True(changes.IsEmpty);
    }

    [Fact]
    public void Two_siblings_same_text_under_different_parents_one_reindented_pairs_FIFO()
    {
        var prior = Parse("# TODAY\n## MAINQUESTS\n- [ ] parentA\n\t- [ ] dup\n- [ ] parentB\n\t- [ ] dup\n");
        var current = Parse("# TODAY\n## MAINQUESTS\n- [ ] parentA\n\t- [ ] dup\n- [ ] parentB\n- [ ] dup\n");
        var changes = new ChangeDetector().Detect(SnapshotOf(prior), current);

        Assert.True(changes.IsEmpty);
    }
}
