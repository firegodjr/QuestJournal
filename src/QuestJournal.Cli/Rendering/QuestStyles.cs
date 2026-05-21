using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

public static class QuestStyles
{
    public static string StyleGlyph(QuestStatus status, string glyph) =>
        status switch
        {
            QuestStatus.Open => $"[orange1]{Markup.Escape(glyph)}[/]",
            QuestStatus.Active => $"[orange1]{Markup.Escape(glyph)}[/]",
            QuestStatus.Cancelled => $"[grey strikethrough]{Markup.Escape(glyph)}[/]",
            QuestStatus.Warning => $"[red]{Markup.Escape(glyph)}[/]",
            QuestStatus.Completed => $"[lightskyblue1]{Markup.Escape(glyph)}[/]",
            QuestStatus.Comment => $"[lightskyblue1]{Markup.Escape(glyph)}[/]",
            _ => Markup.Escape(glyph),
        };

    public static string StyleText(QuestStatus status, string text)
    {
        var escaped = Markup.Escape(text);
        return status switch
        {
            QuestStatus.Cancelled => $"[grey strikethrough]{escaped}[/]",
            QuestStatus.Completed => $"[lightskyblue1]{escaped}[/]",
            QuestStatus.Comment => $"[lightskyblue1]{escaped}[/]",
            QuestStatus.Warning => $"[red]{escaped}[/]",
            _ => escaped,
        };
    }
}
