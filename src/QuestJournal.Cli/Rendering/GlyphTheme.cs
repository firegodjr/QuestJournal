using QuestJournal.Core.Model;

namespace QuestJournal.Cli.Rendering;

public sealed record GlyphTheme(
    string Open,
    string Active,
    string Cancelled,
    string Warning,
    string Completed,
    string Comment)
{
    public static GlyphTheme Ascii { get; } = new(
        Open: "[ ]",
        Active: "[>]",
        Cancelled: "[~]",
        Warning: "[!]",
        Completed: "[x]",
        Comment: "•");

    public static GlyphTheme NerdFont { get; } = new(
        Open: "󰄱",   // 󰄱
        Active: "",     //
        Cancelled: "󰰱", // 󰰱
        Warning: "",    //
        Completed: "",  //
        Comment: "•");

    public string GlyphFor(QuestStatus status) => status switch
    {
        QuestStatus.Open => Open,
        QuestStatus.Active => Active,
        QuestStatus.Cancelled => Cancelled,
        QuestStatus.Warning => Warning,
        QuestStatus.Completed => Completed,
        QuestStatus.Comment => Comment,
        _ => " ",
    };
}
