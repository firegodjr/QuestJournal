using System.Collections.Immutable;
using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.Tests;

public class XpCalculatorTests
{
    private static readonly TaskKey K = new("TODAY", "MAINQUESTS", ImmutableArray<string>.Empty, "alpha");

    [Theory]
    [InlineData(QuestStatus.Open, QuestStatus.Completed, 10L)]
    [InlineData(QuestStatus.Active, QuestStatus.Completed, 10L)]
    [InlineData(QuestStatus.Open, QuestStatus.Cancelled, 1L)]
    [InlineData(QuestStatus.Open, QuestStatus.Active, 2L)]
    [InlineData(QuestStatus.Active, QuestStatus.Warning, 2L)]
    public void Status_change_xp(QuestStatus oldStatus, QuestStatus newStatus, long expected)
    {
        Assert.Equal(expected, XpCalculator.Award(new Change.StatusChanged(K, oldStatus, newStatus)));
    }

    [Theory]
    [InlineData(QuestStatus.Open, 1L)]
    [InlineData(QuestStatus.Active, 1L)]
    [InlineData(QuestStatus.Completed, 1L)]
    [InlineData(QuestStatus.Comment, 0L)]
    public void Added_xp(QuestStatus status, long expected)
    {
        Assert.Equal(expected, XpCalculator.Award(new Change.Added(K, status)));
    }

    [Theory]
    [InlineData(QuestStatus.Open)]
    [InlineData(QuestStatus.Completed)]
    [InlineData(QuestStatus.Cancelled)]
    public void Removed_awards_no_xp(QuestStatus lastStatus)
    {
        Assert.Equal(0L, XpCalculator.Award(new Change.Removed(K, lastStatus)));
    }

    [Fact]
    public void Changeset_sums_individual_awards()
    {
        var set = new ChangeSet(new Change[]
        {
            new Change.StatusChanged(K, QuestStatus.Open, QuestStatus.Completed), // 10
            new Change.Added(K with { Text = "beta" }, QuestStatus.Open),         // 1
            new Change.Removed(K with { Text = "gamma" }, QuestStatus.Open),      // 0
            new Change.StatusChanged(K with { Text = "delta" }, QuestStatus.Open, QuestStatus.Active), // 2
        });

        Assert.Equal(13L, XpCalculator.Award(set));
    }
}
