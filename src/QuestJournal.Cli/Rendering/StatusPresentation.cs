using QuestJournal.Core.Model;

namespace QuestJournal.Cli.Rendering;

public sealed record StatusPresentation(
    string AsciiGlyph,
    string NerdGlyph,
    string GlyphStyle,
    string? TextStyle,
    string Label);

public static class StatusPresentations
{
    private static readonly StatusPresentation Fallback =
        new(AsciiGlyph: " ", NerdGlyph: " ", GlyphStyle: "", TextStyle: null, Label: "None");

    private static readonly Dictionary<QuestStatus, StatusPresentation> Table = new()
    {
        [QuestStatus.None] = Fallback,
        [QuestStatus.Open] = new(
            AsciiGlyph: "[ ]",
            NerdGlyph:  "󰄱",
            GlyphStyle: "orange1",
            TextStyle:  null,
            Label:      "Open"),
        [QuestStatus.Active] = new(
            AsciiGlyph: "[>]",
            NerdGlyph:  "",
            GlyphStyle: "orange1",
            TextStyle:  null,
            Label:      "Active"),
        [QuestStatus.Cancelled] = new(
            AsciiGlyph: "[~]",
            NerdGlyph:  "󰰱",
            GlyphStyle: "grey strikethrough",
            TextStyle:  "grey strikethrough",
            Label:      "Cancelled"),
        [QuestStatus.Warning] = new(
            AsciiGlyph: "[!]",
            NerdGlyph:  "",
            GlyphStyle: "red",
            TextStyle:  "red",
            Label:      "Warning"),
        [QuestStatus.Completed] = new(
            AsciiGlyph: "[x]",
            NerdGlyph:  "",
            GlyphStyle: "lightskyblue1",
            TextStyle:  "lightskyblue1",
            Label:      "Completed"),
        [QuestStatus.Comment] = new(
            AsciiGlyph: "•",
            NerdGlyph:  "•",
            GlyphStyle: "lightskyblue1",
            TextStyle:  "lightskyblue1",
            Label:      "Comment"),
    };

    public static StatusPresentation For(QuestStatus status)
        => Table.TryGetValue(status, out var p) ? p : Fallback;
}
