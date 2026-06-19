using QuestJournal.Core.IO;

namespace QuestJournal.Core.Tests;

public class LazygitRecentReposTests
{
    [Fact]
    public void Parses_the_recentrepos_list_in_order()
    {
        var lines = new[]
        {
            "lastupdatecheck: 1760036021",
            "recentrepos:",
            "    - /home/ethan/dev",
            "    - /home/ethan/repos/quest-journal",
            "startuppopupversion: 5",
        };

        var repos = LazygitRecentRepos.ParseRecentRepos(lines);

        Assert.Equal(new[] { "/home/ethan/dev", "/home/ethan/repos/quest-journal" }, repos);
    }

    [Fact]
    public void Collapses_trailing_slash_duplicates_preserving_first_order()
    {
        var lines = new[]
        {
            "recentrepos:",
            "    - /home/ethan/dev",
            "    - /home/ethan/repos/quest-journal",
            "    - /home/ethan/dev/",
        };

        var repos = LazygitRecentRepos.ParseRecentRepos(lines);

        Assert.Equal(new[] { "/home/ethan/dev", "/home/ethan/repos/quest-journal" }, repos);
    }

    [Fact]
    public void Stops_at_the_next_top_level_key()
    {
        var lines = new[]
        {
            "recentrepos:",
            "    - /a",
            "customcommandshistory:",
            "    - git submodule update", // belongs to a different key, must be ignored
        };

        var repos = LazygitRecentRepos.ParseRecentRepos(lines);

        Assert.Equal(new[] { "/a" }, repos);
    }

    [Fact]
    public void Strips_surrounding_quotes()
    {
        var lines = new[]
        {
            "recentrepos:",
            "    - \"/home/ethan/my repo\"",
        };

        var repos = LazygitRecentRepos.ParseRecentRepos(lines);

        Assert.Equal(new[] { "/home/ethan/my repo" }, repos);
    }

    [Fact]
    public void Missing_key_yields_empty()
    {
        var lines = new[] { "lastupdatecheck: 1", "startuppopupversion: 5" };

        Assert.Empty(LazygitRecentRepos.ParseRecentRepos(lines));
    }
}
