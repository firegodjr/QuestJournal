namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// A 2D grid of metric values for a GitHub-style contribution heatmap. The scope's primary
/// subdivision runs along the x-axis (<see cref="ColumnLabels"/>) and the next-finer
/// subdivision down the y-axis (<see cref="RowLabels"/>). <see cref="Values"/> is indexed
/// <c>[row, col]</c>; <see cref="HasData"/> is <c>false</c> for cells outside the period
/// (e.g. day 31 of a 30-day month) so they render as background, distinct from a real zero.
/// </summary>
public sealed class Heatmap
{
    public required GraphScope Scope { get; init; }
    public required GraphMetric Metric { get; init; }

    /// <summary>x-axis labels, one per column, oldest first.</summary>
    public required IReadOnlyList<string> ColumnLabels { get; init; }

    /// <summary>y-axis labels, one per row, top to bottom.</summary>
    public required IReadOnlyList<string> RowLabels { get; init; }

    /// <summary>Metric value per cell, indexed <c>[row, col]</c>.</summary>
    public required long[,] Values { get; init; }

    /// <summary>Whether a cell falls inside the period; <c>false</c> cells render as background.</summary>
    public required bool[,] HasData { get; init; }

    /// <summary>Peak in-period cell value, used to normalize the gradient. Zero when empty.</summary>
    public required long Max { get; init; }

    public int Rows => RowLabels.Count;
    public int Columns => ColumnLabels.Count;
}
