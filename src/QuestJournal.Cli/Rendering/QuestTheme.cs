using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

public sealed class QuestTheme
{
    public static QuestTheme Ascii { get; } = new(useNerdFont: false, xpGlyph: "❈");
    public static QuestTheme NerdFont { get; } = new(useNerdFont: true, xpGlyph: "");

    private readonly bool _useNerdFont;

    public string XpGlyph { get; }

    private QuestTheme(bool useNerdFont, string xpGlyph)
    {
        _useNerdFont = useNerdFont;
        XpGlyph = xpGlyph;
    }

    public string Glyph(QuestStatus status)
    {
        var p = Presentation.For(status);
        return _useNerdFont ? p.Nerd : p.Ascii;
    }

    public string StyledGlyph(QuestStatus status)
    {
        var p = Presentation.For(status);
        var glyph = _useNerdFont ? p.Nerd : p.Ascii;
        var escaped = Markup.Escape(glyph);
        return string.IsNullOrEmpty(p.GlyphStyle) ? escaped : $"[{p.GlyphStyle}]{escaped}[/]";
    }

    public string StyledText(QuestStatus status, string text)
    {
        var p = Presentation.For(status);
        var escaped = Markup.Escape(text);
        return p.TextStyle is null ? escaped : $"[{p.TextStyle}]{escaped}[/]";
    }

    public string Label(QuestStatus status) => Presentation.For(status).Label;

    public static string DayHeader(string name) =>
        $"[bold]# {Markup.Escape(name)}[/]";

    public static string CategoryHeader(string name) =>
        $"[bold dim]## {Markup.Escape(name)}[/]";

    private sealed record Presentation(
        string Ascii,
        string Nerd,
        string GlyphStyle,
        string? TextStyle,
        string Label)
    {
        private static readonly Dictionary<QuestStatus, Presentation> Table = new()
        {
            [QuestStatus.Open] = new(
                Ascii: "[ ]",
                Nerd:  "\U000F0131",
                GlyphStyle: "orange1",
                TextStyle:  null,
                Label:      "Open"),
            [QuestStatus.Active] = new(
                Ascii: "[>]",
                Nerd:  "",
                GlyphStyle: "orange1",
                TextStyle:  null,
                Label:      "Active"),
            [QuestStatus.Cancelled] = new(
                Ascii: "[~]",
                Nerd:  "\U000F0C31",
                GlyphStyle: "grey strikethrough",
                TextStyle:  "grey strikethrough",
                Label:      "Cancelled"),
            [QuestStatus.Warning] = new(
                Ascii: "[!]",
                Nerd:  "",
                GlyphStyle: "red",
                TextStyle:  "red",
                Label:      "Warning"),
            [QuestStatus.Completed] = new(
                Ascii: "[x]",
                Nerd:  "",
                GlyphStyle: "lightskyblue1",
                TextStyle:  "lightskyblue1",
                Label:      "Completed"),
            [QuestStatus.Comment] = new(
                Ascii: "•",
                Nerd:  "•",
                GlyphStyle: "lightskyblue1",
                TextStyle:  "lightskyblue1",
                Label:      "Comment"),
        };

        public static Presentation For(QuestStatus status)
        {
            if (!Table.TryGetValue(status, out var p))
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unhandled QuestStatus value.");
            }
            return p;
        }
    }
}
