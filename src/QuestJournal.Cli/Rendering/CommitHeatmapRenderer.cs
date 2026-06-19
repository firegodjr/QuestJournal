using System.Globalization;
using System.Text;
using QuestJournal.Core.ChangeTracking;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

/// <summary>
/// Renders a <see cref="CommitHeatmap"/> as a contribution grid where each repository is a
/// distinct hue and a single cell can show several repos at once as vertical color stripes.
/// Stripes are packed two-per-character with the left-half block <c>▌</c>: its foreground paints
/// the left stripe and its background the right (so an <c>n</c>-char cell holds up to <c>2n</c>
/// stripes). Each repo's gradient self-normalizes to its own busiest cell; a legend maps every
/// repo's swatch to its name.
/// </summary>
public sealed class CommitHeatmapRenderer
{
    private const char HalfBlock = '▌'; // ▌ LEFT HALF BLOCK
    private const int Levels = 5;
    private const int MaxCharsPerCell = 4; // up to 8 stripe slots per cell
    private static readonly (int R, int G, int B) Empty = (38, 38, 38);

    private readonly IAnsiConsole _console;

    public CommitHeatmapRenderer(IAnsiConsole console)
    {
        _console = console;
    }

    public void Render(CommitHeatmap grid)
    {
        _console.MarkupLine($"[bold]commits — {Window(grid.Scope)}[/]");

        if (grid.IsEmpty || grid.Columns == 0 || grid.Rows == 0)
        {
            _console.MarkupLine("[dim]No commits to graph.[/]");
            return;
        }

        var colors = AssignColors(grid.Layers.Count);
        var charsPerCell = CharsPerCell(grid);
        var stride = charsPerCell + 1; // cell + one-space column gap

        var rowLabelWidth = grid.RowLabels.Max(l => l.Length);
        var leftMargin = rowLabelWidth + 1;

        for (int r = 0; r < grid.Rows; r++)
        {
            var line = new StringBuilder();
            line.Append(grid.RowLabels[r].PadLeft(rowLabelWidth)).Append(' ');
            for (int c = 0; c < grid.Columns; c++)
            {
                line.Append(Cell(grid, colors, r, c, charsPerCell));
                if (c < grid.Columns - 1)
                {
                    line.Append(' ');
                }
            }
            _console.MarkupLine(line.ToString());
        }

        _console.WriteLine(ColumnHeader(grid.ColumnLabels, leftMargin, stride));
        foreach (var legendLine in LegendLines(grid.Layers, colors, _console.Profile.Width))
        {
            _console.MarkupLine(legendLine);
        }
    }

    /// <summary>One char per cell per two repos that overlap there, clamped for sane width.</summary>
    private static int CharsPerCell(CommitHeatmap grid)
    {
        int activeMax = 0;
        for (int r = 0; r < grid.Rows; r++)
        {
            for (int c = 0; c < grid.Columns; c++)
            {
                if (!grid.HasData[r, c])
                {
                    continue;
                }
                int active = 0;
                foreach (var layer in grid.Layers)
                {
                    if (layer.Counts[r, c] > 0)
                    {
                        active++;
                    }
                }
                if (active > activeMax)
                {
                    activeMax = active;
                }
            }
        }
        var chars = (int)Math.Ceiling(Math.Max(activeMax, 1) / 2.0);
        return Math.Clamp(chars, 1, MaxCharsPerCell);
    }

    /// <summary>
    /// One cell: <paramref name="charsPerCell"/> half-block chars (blank when out-of-period).
    /// Active repos fill stripe slots left to right at their own gradient intensity; unused
    /// slots are background grey. Repos beyond the slot budget are truncated (rare).
    /// </summary>
    private static string Cell(
        CommitHeatmap grid, IReadOnlyList<(int R, int G, int B)> colors, int r, int c, int charsPerCell)
    {
        if (!grid.HasData[r, c])
        {
            return new string(' ', charsPerCell);
        }

        var slots = charsPerCell * 2;
        var slotColors = new (int R, int G, int B)[slots];
        Array.Fill(slotColors, Empty);

        int next = 0;
        for (int i = 0; i < grid.Layers.Count && next < slots; i++)
        {
            var count = grid.Layers[i].Counts[r, c];
            if (count > 0)
            {
                slotColors[next++] = Shade(Level(count, grid.Layers[i].Max), colors[i]);
            }
        }

        var sb = new StringBuilder();
        for (int k = 0; k < slots; k += 2)
        {
            var fg = slotColors[k];
            var bg = slotColors[k + 1];
            sb.Append($"[#{Hex(fg)} on #{Hex(bg)}]{HalfBlock}[/]");
        }
        return sb.ToString();
    }

    /// <summary>Evenly-spaced hues around the color wheel, one per repo layer.</summary>
    private static (int R, int G, int B)[] AssignColors(int count)
    {
        var colors = new (int, int, int)[count];
        for (int i = 0; i < count; i++)
        {
            var hue = count == 0 ? 0 : i * 360.0 / count;
            colors[i] = HsvToRgb(hue, 0.65, 0.95);
        }
        return colors;
    }

    private static (int R, int G, int B) HsvToRgb(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        var m = v - c;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return (
            (int)Math.Round((r + m) * 255),
            (int)Math.Round((g + m) * 255),
            (int)Math.Round((b + m) * 255));
    }

    private static int Level(long value, long max)
    {
        if (value <= 0 || max <= 0)
        {
            return 0;
        }
        return Math.Clamp((int)Math.Ceiling((double)value / max * Levels), 1, Levels);
    }

    private static (int, int, int) Shade(int level, (int R, int G, int B) full)
    {
        if (level <= 0)
        {
            return Empty;
        }
        // Same 0.6→1.0 brightness range as the XP/Completed heatmap (HeatmapRenderer.Shade), so the
        // dimmest step stays readable rather than fading toward empty. That renderer flattens its
        // bottom levels to one color and uses partial-coverage shade glyphs (░░, ▒▒) to carry the
        // low-end gradient; the half-block packing here has no room for glyph density, so we keep
        // five distinct brightness steps spread across the shared range instead.
        var factor = 0.6 + 0.4 * (level - 1) / (Levels - 1);
        return (
            (int)Math.Round(full.R * factor),
            (int)Math.Round(full.G * factor),
            (int)Math.Round(full.B * factor));
    }

    private static string Hex((int R, int G, int B) c) => $"{c.R:x2}{c.G:x2}{c.B:x2}";

    /// <summary>Sparse x-axis labels, placed at each column start, skipping overlaps (as in the XP grid).</summary>
    private static string ColumnHeader(IReadOnlyList<string> labels, int leftMargin, int stride)
    {
        var width = leftMargin + labels.Count * stride;
        var buffer = new char[width];
        Array.Fill(buffer, ' ');

        int nextFree = 0;
        for (int c = 0; c < labels.Count; c++)
        {
            var x = leftMargin + c * stride;
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

    /// <summary>
    /// Swatch + repo name per layer (duplicate basenames disambiguated by path), packed onto as
    /// few lines as fit <paramref name="maxWidth"/> without ever splitting a swatch from its name.
    /// </summary>
    private static List<string> LegendLines(
        IReadOnlyList<RepoLayer> layers, IReadOnlyList<(int R, int G, int B)> colors, int maxWidth)
    {
        const int SwatchWidth = 2; // "██"
        const int SepWidth = 2;    // two spaces between entries
        var width = Math.Max(maxWidth, 20);

        var labels = DisambiguateNames(layers);
        var legendLines = new List<string>();
        var current = new StringBuilder();
        var currentWidth = 0;

        for (int i = 0; i < layers.Count; i++)
        {
            var entry = $"[#{Hex(colors[i])}]██[/] {Markup.Escape(labels[i])}";
            var entryWidth = SwatchWidth + 1 + labels[i].Length;
            var sep = current.Length == 0 ? 0 : SepWidth;

            if (current.Length > 0 && currentWidth + sep + entryWidth > width)
            {
                legendLines.Add(current.ToString());
                current.Clear();
                currentWidth = 0;
                sep = 0;
            }

            if (sep > 0)
            {
                current.Append("  ");
                currentWidth += SepWidth;
            }
            current.Append(entry);
            currentWidth += entryWidth;
        }

        if (current.Length > 0)
        {
            legendLines.Add(current.ToString());
        }
        return legendLines;
    }

    /// <summary>
    /// Repo basenames, extended with the shortest unique trailing path segments when two repos
    /// share a basename (e.g. three different <c>Portal</c> dirs → <c>dev/Portal</c>, … ).
    /// </summary>
    private static string[] DisambiguateNames(IReadOnlyList<RepoLayer> layers)
    {
        var labels = new string[layers.Count];
        var byName = layers
            .Select((l, i) => (l, i))
            .GroupBy(t => t.l.Name, StringComparer.Ordinal);

        foreach (var group in byName)
        {
            var members = group.ToList();
            if (members.Count == 1)
            {
                labels[members[0].i] = members[0].l.Name;
                continue;
            }

            var paths = members
                .Select(m => m.l.FullPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                .ToList();
            foreach (var (layer, index) in members)
            {
                var segs = layer.FullPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                labels[index] = ShortestUniqueSuffix(segs, paths);
            }
        }

        return labels;
    }

    private static string ShortestUniqueSuffix(string[] target, IReadOnlyList<string[]> all)
    {
        for (int depth = 2; depth <= target.Length; depth++)
        {
            var suffix = string.Join('/', target[^depth..]);
            var collisions = all.Count(p => p.Length >= depth && string.Join('/', p[^depth..]) == suffix);
            if (collisions == 1)
            {
                return suffix;
            }
        }
        return string.Join('/', target);
    }

    private static string Window(GraphScope scope) => scope switch
    {
        GraphScope.Week => "past week",
        GraphScope.Month => "past 3 months",
        GraphScope.Year => "past 12 months",
        GraphScope.All => "all time",
        _ => string.Empty,
    };
}
