using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

public sealed class StatusRenderer
{
    private readonly QuestTheme _theme;

    public StatusRenderer(QuestTheme theme)
    {
        _theme = theme;
    }

    public void RenderDay(DaySection day)
    {
        AnsiConsole.MarkupLine(QuestTheme.DayHeader(day.Name));
        foreach (var category in day.Categories)
        {
            AnsiConsole.MarkupLine($"  {QuestTheme.CategoryHeader(category.Name)}");
            foreach (var quest in category.TopLevelQuests)
            {
                RenderQuest(quest);
            }
        }
    }

    private void RenderQuest(Quest quest)
    {
        var styledGlyph = _theme.StyledGlyph(quest.Status);
        var styledText = _theme.StyledText(quest.Status, quest.Text);
        AnsiConsole.MarkupLine($"    {styledGlyph} {styledText} [grey](line {quest.LineNumber})[/]");
    }
}
