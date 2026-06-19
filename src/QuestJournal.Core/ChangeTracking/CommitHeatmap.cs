namespace QuestJournal.Core.ChangeTracking;

/// <summary>One repository's commit timestamps, fed into <see cref="CommitHistoryGraph.Build"/>.</summary>
public sealed class RepoCommits
{
    /// <summary>Display name (typically the repo's directory basename).</summary>
    public required string Name { get; init; }

    /// <summary>Absolute path, used to disambiguate repos that share a basename.</summary>
    public required string FullPath { get; init; }

    /// <summary>Committer timestamps of the user's commits.</summary>
    public required IReadOnlyList<DateTimeOffset> Commits { get; init; }
}

/// <summary>
/// One repository's contribution to a <see cref="CommitHeatmap"/>: a per-cell commit count and
/// the repo's own peak, so each repo's color gradient self-normalizes independently.
/// </summary>
public sealed class RepoLayer
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }

    /// <summary>Commit count per cell, indexed <c>[row, col]</c>.</summary>
    public required long[,] Counts { get; init; }

    /// <summary>This repo's peak in-period cell count, used to normalize its gradient.</summary>
    public required long Max { get; init; }
}

/// <summary>
/// A commit heatmap: the shared grid skeleton plus one <see cref="RepoLayer"/> per repository
/// that committed in the window. The renderer overlays the layers as vertical color stripes,
/// giving each repo a distinct hue. Repos with no in-window commits are absent.
/// </summary>
public sealed class CommitHeatmap
{
    public required GraphScope Scope { get; init; }

    /// <summary>x-axis labels, one per column, oldest first.</summary>
    public required IReadOnlyList<string> ColumnLabels { get; init; }

    /// <summary>y-axis labels, one per row, top to bottom.</summary>
    public required IReadOnlyList<string> RowLabels { get; init; }

    /// <summary>Whether each cell falls inside the period; <c>false</c> cells render as background.</summary>
    public required bool[,] HasData { get; init; }

    /// <summary>One layer per contributing repo, in stable order (drives hue assignment).</summary>
    public required IReadOnlyList<RepoLayer> Layers { get; init; }

    public int Rows => RowLabels.Count;
    public int Columns => ColumnLabels.Count;
    public bool IsEmpty => Layers.Count == 0;
}
