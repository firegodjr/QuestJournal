using QuestJournal.Core.ChangeTracking;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

/// <summary>
/// Renders a history metric as a Spectre <see cref="BarChart"/> (one bar per time bucket).
/// Spectre 0.55.2 has no line-chart widget, so the bar chart stands in.
/// </summary>
public sealed class HistoryGraphRenderer
{
    private readonly QuestTheme _theme;

    public HistoryGraphRenderer(QuestTheme theme)
    {
        _theme = theme;
    }

    public void Render(GraphScope scope, GraphMetric metric, IReadOnlyList<GraphBar> bars)
    {
        var (heading, color) = metric switch
        {
            GraphMetric.Completed => ($"✓ Completed — {Window(scope)}", Color.LightSkyBlue1),
            _ => ($"{_theme.XpGlyph} XP — {Window(scope)}", Color.Green),
        };

        var chart = new BarChart()
            .Width(60)
            .Label($"[bold]{heading}[/]")
            .CenterLabel();

        foreach (var bar in bars)
        {
            chart.AddItem(Markup.Escape(bar.Label), bar.Value, color);
        }

        AnsiConsole.Write(chart);
    }

    private static string Window(GraphScope scope) => scope switch
    {
        GraphScope.Week => "past week",
        GraphScope.Month => "past 30 days",
        GraphScope.Year => "past 12 months",
        GraphScope.All => "all time",
        _ => string.Empty,
    };
}
