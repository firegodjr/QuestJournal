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
        var styledGlyph = StyleGlyph(quest.Status, glyph);
        var styledText = StyleText(quest.Status, quest.Text);
        AnsiConsole.MarkupLine($"    {styledGlyph} {styledText} [grey](line {quest.LineNumber})[/]");
    }

    private static string StyleGlyph(QuestStatus status, string glyph) =>
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

    private static string StyleText(QuestStatus status, string text)
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
