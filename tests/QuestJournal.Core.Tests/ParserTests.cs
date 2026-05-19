using QuestJournal.Core.Model;
using QuestJournal.Core.Parsing;

namespace QuestJournal.Core.Tests;

public class ParserTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-tasks.md");

    private static JournalDocument ParseFixture() =>
        new JournalParser().ParseFile(FixturePath);

    [Fact]
    public void Captures_frontmatter_lines()
    {
        var doc = ParseFixture();
        Assert.Equal(3, doc.FrontmatterLines.Count);
        Assert.Equal("id: Tasks", doc.FrontmatterLines[0]);
        Assert.Equal("aliases: []", doc.FrontmatterLines[1]);
        Assert.Equal("tags: []", doc.FrontmatterLines[2]);
    }

    [Fact]
    public void Detects_three_days_in_order()
    {
        var doc = ParseFixture();
        Assert.Equal(new[] { "TODAY", "TOMORROW", "YESTERDAY" },
            doc.Days.Select(d => d.Name).ToArray());
    }

    [Fact]
    public void Preserves_custom_subheadings()
    {
        var doc = ParseFixture();
        var today = doc.Days.First(d => d.Name == "TODAY");
        Assert.Contains(today.Categories, c => c.Name == "EPICS");
    }

    [Theory]
    [InlineData(" ", QuestStatus.Open)]
    [InlineData(">", QuestStatus.Active)]
    [InlineData("~", QuestStatus.Cancelled)]
    [InlineData("!", QuestStatus.Warning)]
    [InlineData("x", QuestStatus.Completed)]
    [InlineData("X", QuestStatus.Completed)]
    public void Maps_checkbox_marks_to_status(string mark, QuestStatus expected)
    {
        var md = $"# DAY\n## CAT\n- [{mark}] Task\n";
        var doc = new JournalParser().Parse(md);
        var quest = doc.Days[0].Categories[0].TopLevelQuests[0];
        Assert.Equal(expected, quest.Status);
        Assert.Equal("Task", quest.Text);
    }

    [Fact]
    public void Plain_bullets_become_comment_quests()
    {
        var doc = ParseFixture();
        var miku = doc.Days[0].Categories[0].TopLevelQuests[2];
        Assert.Equal("Miku Portal Issues", miku.Text);
        Assert.Equal(QuestStatus.Comment, miku.Children[0].Status);
        Assert.Contains("Errors when completing", miku.Children[0].Text);
    }

    [Fact]
    public void Numbered_list_items_become_comment_quests_without_crashing()
    {
        var doc = ParseFixture();
        var tomorrow = doc.Days.First(d => d.Name == "TOMORROW");
        var portalPipeline = tomorrow.Categories[0].TopLevelQuests[0];
        var numbered = portalPipeline.Children
            .Where(c => c.Status == QuestStatus.Comment)
            .Select(c => c.Text)
            .ToList();
        Assert.Contains(numbered, t => t.StartsWith("build whole Portal"));
        Assert.Contains(numbered, t => t.StartsWith("push built portal"));
    }

    [Fact]
    public void Custom_text_insight_has_four_direct_children()
    {
        var doc = ParseFixture();
        var insight = doc.Days[0].Categories[0].TopLevelQuests[0];
        Assert.Equal("Custom Text Insight", insight.Text);
        Assert.Equal(4, insight.Children.Count);
    }

    [Fact]
    public void Add_caching_has_two_children_including_nested_klfetchservice()
    {
        var doc = ParseFixture();
        var insight = doc.Days[0].Categories[0].TopLevelQuests[0];
        var addCaching = insight.Children[0];
        Assert.StartsWith("Add caching per-call", addCaching.Text);
        Assert.Equal(2, addCaching.Children.Count);
        Assert.Contains(addCaching.Children, c =>
            c.Status == QuestStatus.Completed && c.Text == "Add to KLFetchService");
    }

    [Fact]
    public void Today_mainquests_has_three_top_level_quests()
    {
        var doc = ParseFixture();
        var mq = doc.Days[0].Categories.First(c => c.Name == "MAINQUESTS");
        Assert.Equal(3, mq.TopLevelQuests.Count);
    }

    [Fact]
    public void Today_sidequests_has_two_top_level_quests()
    {
        var doc = ParseFixture();
        var sq = doc.Days[0].Categories.First(c => c.Name == "SIDEQUESTS");
        Assert.Equal(2, sq.TopLevelQuests.Count);
    }

    [Fact]
    public void Tomorrow_mainquests_flush_left_still_parses_as_top_level()
    {
        var doc = ParseFixture();
        var tomorrow = doc.Days.First(d => d.Name == "TOMORROW");
        var mq = tomorrow.Categories.First(c => c.Name == "MAINQUESTS");
        Assert.Single(mq.TopLevelQuests);
        Assert.Equal("Portal Pipeline Changes", mq.TopLevelQuests[0].Text);
    }

    [Fact]
    public void Frontmatter_separator_is_not_treated_as_horizontal_rule()
    {
        var doc = ParseFixture();
        Assert.NotEmpty(doc.Days);
        Assert.Equal("TODAY", doc.Days[0].Name);
    }
}
