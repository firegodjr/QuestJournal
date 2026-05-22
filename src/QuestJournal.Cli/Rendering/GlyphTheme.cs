using QuestJournal.Core.Model;

namespace QuestJournal.Cli.Rendering;

public sealed record GlyphTheme(bool UseNerdFont, string Xp)
{
    public static GlyphTheme Ascii { get; } = new(UseNerdFont: false, Xp: "❈");
    public static GlyphTheme NerdFont { get; } = new(UseNerdFont: true, Xp: "");

    public string GlyphFor(QuestStatus status)
    {
        var presentation = StatusPresentations.For(status);
        return UseNerdFont ? presentation.NerdGlyph : presentation.AsciiGlyph;
    }
}
