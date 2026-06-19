using QuestJournal.Core.ChangeTracking;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

/// <summary>
/// Renders durable <see cref="HistoryEntry"/> records. Status styling is routed through
/// <see cref="QuestTheme"/> so history matches the status and diff views.
/// </summary>
public sealed class HistoryRenderer
{
    private readonly QuestTheme _theme;
    private readonly IAnsiConsole _console;

    public HistoryRenderer(QuestTheme theme, IAnsiConsole console)
    {
        _theme = theme;
        _console = console;
    }

    /// <summary>
    /// Standup view: change batches grouped by timestamp, oldest first.
    /// </summary>
    public void RenderRecent(IReadOnlyList<HistoryEntry> entries)
    {
        bool first = true;
        foreach (var entry in entries.OrderBy(e => e.Timestamp))
        {
            if (!first) _console.WriteLine();
            first = false;

            var local = entry.Timestamp.ToLocalTime();
            var xp = entry.XpAwarded > 0 ? $"  [green]+{entry.XpAwarded} XP[/]" : string.Empty;
            _console.MarkupLine(
                $"[bold]{local:ddd HH:mm}[/] [dim]({Relative(entry.Timestamp)})[/]{xp}");

            foreach (var change in entry.Changes)
            {
                _console.MarkupLine($"  {ChangeLine(change)}");
            }
            foreach (var move in entry.Moves)
            {
                _console.MarkupLine($"  {MoveLine(move)}");
            }
        }
    }

    /// <summary>
    /// Timeline view for a single quest: one timestamped line per event, oldest first.
    /// </summary>
    public void RenderTimeline(string entryText, IReadOnlyList<(DateTimeOffset Timestamp, string Line)> events)
    {
        _console.MarkupLine($"[bold]Timeline for[/] [dim]{Markup.Escape(entryText)}[/]");
        _console.WriteLine();

        foreach (var (timestamp, line) in events.OrderBy(e => e.Timestamp))
        {
            var local = timestamp.ToLocalTime();
            _console.MarkupLine($"[dim]{local:yyyy-MM-dd HH:mm}[/]  {line}");
        }
    }

    public string ChangeLine(HistoryChange change)
    {
        var text = change.Text;
        return change.Kind switch
        {
            nameof(Change.Added) =>
                $"[green]+[/] {_theme.StyledGlyph(change.Status)} " +
                $"{_theme.StyledText(change.Status, text)}",

            nameof(Change.Removed) =>
                $"[grey strikethrough]- {Markup.Escape(text)}[/] [dim](removed)[/]",

            nameof(Change.StatusChanged) =>
                $"{_theme.StyledGlyph(change.NewStatus)} " +
                $"{_theme.StyledText(change.NewStatus, text)} " +
                $"[dim]({_theme.Label(change.OldStatus)} → {_theme.Label(change.NewStatus)})[/]",

            _ => throw new InvalidOperationException(
                $"Unexpected change kind: {change.Kind}"),
        };
    }

    public string MoveLine(HistoryMove move)
    {
        var from = $"{Markup.Escape(move.FromDay)}/{Markup.Escape(move.FromCategory)}";
        var to = $"{Markup.Escape(move.ToDay)}/{Markup.Escape(move.ToCategory)}";
        return $"{_theme.StyledGlyph(move.Status)} " +
               $"{_theme.StyledText(move.Status, move.Text)} " +
               $"[dim](moved: {from} → {to})[/]";
    }

    private static string Relative(DateTimeOffset timestamp)
    {
        var delta = DateTimeOffset.Now - timestamp;
        if (delta < TimeSpan.Zero) return "just now";
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        return $"{(int)delta.TotalDays}d ago";
    }
}
