using System.Globalization;
using System.Text;
using QuestJournal.Core.ChangeTracking;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

/// <summary>
/// Renders a history metric as a GitHub-style contribution heatmap: a 2D grid of colored
/// squares whose intensity (near-black → full color) encodes the cell value, normalized to
/// the period's peak. The scope's primary subdivision runs along the x-axis and the
/// next-finer one down the y-axis (see <see cref="Heatmap"/>). A legend maps each gradient
/// step to its value range.
/// </summary>
public sealed class HeatmapRenderer
{
    /// <summary>Number of gradient steps above empty.</summary>
    private const int Levels = 5;

    /// <summary>Visible width of one cell (two block chars) plus a one-space column gap.</summary>
    private const int CellStride = 3;

    private static readonly (int R, int G, int B) Empty = (38, 38, 38);

    private readonly QuestTheme _theme;

    public HeatmapRenderer(QuestTheme theme)
    {
        _theme = theme;
    }

    public void Render(Heatmap grid)
    {
        (int, int, int) full = grid.Metric == GraphMetric.Completed ? (56, 139, 253) : (38, 196, 94);
        var heading = grid.Metric == GraphMetric.Completed
            ? $"✓ Completed — {Window(grid.Scope)}"
            : $"{_theme.XpGlyph} XP — {Window(grid.Scope)}";

        AnsiConsole.MarkupLine($"[bold]{heading}[/]");

        if (grid.Columns == 0 || grid.Rows == 0)
        {
            return;
        }

        var rowLabelWidth = grid.RowLabels.Max(l => l.Length);
        var leftMargin = rowLabelWidth + 1; // row label + one space before the first cell

        for (int r = 0; r < grid.Rows; r++)
        {
            var line = new StringBuilder();
            line.Append(grid.RowLabels[r].PadLeft(rowLabelWidth)).Append(' ');
            for (int c = 0; c < grid.Columns; c++)
            {
                line.Append(Cell(grid, r, c, full));
                if (c < grid.Columns - 1)
                {
                    line.Append(' ');
                }
            }
            AnsiConsole.MarkupLine(line.ToString());
        }

        AnsiConsole.WriteLine(ColumnHeader(grid.ColumnLabels, leftMargin));
        AnsiConsole.MarkupLine(Legend(grid.Max, full));
    }

    /// <summary>Markup for one cell: a background gap when out-of-period, else a colored block.</summary>
    private static string Cell(Heatmap grid, int r, int c, (int R, int G, int B) full)
    {
        if (!grid.HasData[r, c])
        {
            return "  ";
        }

        return Swatch(Level(grid.Values[r, c], grid.Max), full);
    }

    /// <summary>0 (empty) through <see cref="Levels"/>, by ceil of the value's share of the peak.</summary>
    private static int Level(long value, long max)
    {
        if (value <= 0 || max <= 0)
        {
            return 0;
        }
        var level = (int)Math.Ceiling((double)value / max * Levels);
        return Math.Clamp(level, 1, Levels);
    }

    /// <summary>
    /// RGB for a level. Empty is a near-black grey. Levels 1–3 share one color (level 3's): the
    /// dim levels 1–2 render with partial-coverage shade glyphs (see <see cref="LevelGlyph"/>),
    /// so glyph density — not a hard-to-read color step — carries the gradient there. Levels 4–5
    /// then brighten that color toward the peak, giving a clean grey→shade→solid→bright ramp.
    /// </summary>
    private static (int, int, int) Shade(int level, (int R, int G, int B) full)
    {
        if (level <= 0)
        {
            return Empty;
        }
        var rampLevel = Math.Max(level, 3);
        var factor = 0.6 + 0.4 * (rampLevel - 3) / (Levels - 3);
        return (
            (int)Math.Round(full.R * factor),
            (int)Math.Round(full.G * factor),
            (int)Math.Round(full.B * factor));
    }

    /// <summary>The two-char block for a level: shaded glyphs for the dim levels 1–2, where color
    /// alone lacks resolution, and solid blocks for empty and the brighter levels 3–5.</summary>
    private static string LevelGlyph(int level) => level switch
    {
        1 => "░░", // ░░ light shade
        2 => "▒▒", // ▒▒ medium shade
        _ => "██", // ██ full block
    };

    /// <summary>Markup for a level's swatch: its glyph painted in its color.</summary>
    private static string Swatch(int level, (int R, int G, int B) full)
    {
        var (r, g, b) = Shade(level, full);
        return $"[#{r:x2}{g:x2}{b:x2}]{LevelGlyph(level)}[/]";
    }

    /// <summary>
    /// X-axis label row. Labels are placed at each column's start; a label that would overlap
    /// the previously placed one is skipped (GitHub-style sparse labelling for dense scopes).
    /// </summary>
    private static string ColumnHeader(IReadOnlyList<string> labels, int leftMargin)
    {
        var width = leftMargin + labels.Count * CellStride;
        var buffer = new char[width];
        Array.Fill(buffer, ' ');

        int nextFree = 0;
        for (int c = 0; c < labels.Count; c++)
        {
            var x = leftMargin + c * CellStride;
            if (x < nextFree)
            {
                continue;
            }
            var label = labels[c];
            for (int i = 0; i < label.Length && x + i < width; i++)
            {
                buffer[x + i] = label[i];
            }
            nextFree = x + label.Length + 1;
        }

        return new string(buffer).TrimEnd();
    }

    /// <summary>Swatch-and-range legend: empty (0) plus each gradient step's value range.</summary>
    private static string Legend(long max, (int R, int G, int B) full)
    {
        var parts = new List<string> { $"{Block(Empty)} 0" };

        long prevHi = 0;
        for (int level = 1; level <= Levels; level++)
        {
            var hi = (long)Math.Ceiling((double)max * level / Levels);
            if (level == Levels)
            {
                hi = max;
            }
            var lo = prevHi + 1;
            if (hi < lo)
            {
                hi = lo;
            }
            var range = lo == hi
                ? lo.ToString(CultureInfo.InvariantCulture)
                : $"{lo}–{hi}";
            parts.Add($"{Swatch(level, full)} {range}");
            prevHi = hi;
        }

        return "[dim]less[/]  " + string.Join("  ", parts) + "  [dim]more[/]";
    }

    private static string Block((int R, int G, int B) rgb) =>
        $"[#{rgb.R:x2}{rgb.G:x2}{rgb.B:x2}]██[/]";

    private static string Window(GraphScope scope) => scope switch
    {
        GraphScope.Week => "past week",
        GraphScope.Month => "past 3 months",
        GraphScope.Year => "past 12 months",
        GraphScope.All => "all time",
        _ => string.Empty,
    };
}
