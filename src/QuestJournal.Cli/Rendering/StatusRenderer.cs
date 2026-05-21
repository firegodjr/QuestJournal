using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

public sealed class StatusRenderer
{
    private readonly GlyphTheme _theme;

    public StatusRenderer(GlyphTheme theme)
    {
        _theme = theme;
    }

    public void RenderDay(DaySection day)
    {
        AnsiConsole.MarkupLine($"[bold]# {Markup.Escape(day.Name)}[/]");
        foreach (var category in day.Categories)
        {
            AnsiConsole.MarkupLine($"  [bold dim]## {Markup.Escape(category.Name)}[/]");
            foreach (var quest in category.TopLevelQuests)
            {
                RenderQuest(quest);
            }
        }
    }

    private void RenderQuest(Quest quest)
    {
        var glyph = _theme.GlyphFor(quest.Status);
        var styledGlyph = QuestStyles.StyleGlyph(quest.Status, glyph);
        var styledText = QuestStyles.StyleText(quest.Status, quest.Text);
        AnsiConsole.MarkupLine($"    {styledGlyph} {styledText} [grey](line {quest.LineNumber})[/]");
    }
}
