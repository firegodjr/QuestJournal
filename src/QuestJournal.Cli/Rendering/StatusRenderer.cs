using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

public sealed class StatusRenderer
{
    private readonly QuestTheme _theme;
    private readonly IAnsiConsole _console;

    public StatusRenderer(QuestTheme theme, IAnsiConsole console)
    {
        _theme = theme;
        _console = console;
    }

    public void RenderDay(DaySection day)
    {
        _console.MarkupLine(QuestTheme.DayHeader(day.Name));
        foreach (var category in day.Categories)
        {
            _console.MarkupLine($"  {QuestTheme.CategoryHeader(category.Name)}");
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
        _console.MarkupLine($"    {styledGlyph} {styledText} [grey](line {quest.LineNumber})[/]");
    }
}
