using QuestJournal.Core.Model;
using QuestJournal.Core.Parsing;

namespace QuestJournal.Core.Tests.Helpers;

public static class TestJournal
{
    public const string Today = "TODAY";
    public const string Tomorrow = "TOMORROW";
    public const string Yesterday = "YESTERDAY";
    public const string MainQuests = "MAINQUESTS";
    public const string SideQuests = "SIDEQUESTS";

    public static JournalDocument Parse(string markdown) =>
        new JournalParser().Parse(markdown);

    public static string Day(string name, params string[] categoryBlocks) =>
        $"# {name}\n" + string.Concat(categoryBlocks);

    public static string Category(string name, params string[] questLines) =>
        $"## {name}\n" + string.Concat(questLines);

    public static string Open(string text, int depth = 0) => Bullet(depth, "[ ] ", text);
    public static string Active(string text, int depth = 0) => Bullet(depth, "[>] ", text);
    public static string Cancelled(string text, int depth = 0) => Bullet(depth, "[~] ", text);
    public static string Warning(string text, int depth = 0) => Bullet(depth, "[!] ", text);
    public static string Completed(string text, int depth = 0) => Bullet(depth, "[x] ", text);
    public static string Comment(string text, int depth = 0) => Bullet(depth, "", text);

    private static string Bullet(int depth, string marker, string text) =>
        new string('\t', depth) + "- " + marker + text + "\n";
}
