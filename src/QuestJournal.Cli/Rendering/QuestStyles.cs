using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

public static class QuestStyles
{
    public static string StyleGlyph(QuestStatus status, string glyph)
    {
        var escaped = Markup.Escape(glyph);
        var style = StatusPresentations.For(status).GlyphStyle;
        return string.IsNullOrEmpty(style) ? escaped : $"[{style}]{escaped}[/]";
    }

    public static string StyleText(QuestStatus status, string text)
    {
        var escaped = Markup.Escape(text);
        var style = StatusPresentations.For(status).TextStyle;
        return style is null ? escaped : $"[{style}]{escaped}[/]";
    }
}
