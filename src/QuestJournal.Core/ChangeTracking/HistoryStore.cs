using System.Text.Json;
using System.Text.Json.Serialization;
using QuestJournal.Core.IO;

namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// Append-only, durable log of change batches at
/// <c>~/.local/share/quest-journal/history.jsonl</c> (respects <c>XDG_DATA_HOME</c>).
/// One <see cref="HistoryEntry"/> per line. Detail is kept for the
/// <see cref="DetailMonths"/> most recent calendar months; older months are compacted into
/// the <see cref="HistoryArchiveStore"/> and dropped on each append.
/// </summary>
public sealed class HistoryStore : IHistoryStore
{
    /// <summary>
    /// Number of recent calendar months kept as raw per-batch detail (3 months plus the
    /// overflow 4th). A batch landing in a 5th distinct month evicts the oldest month.
    /// </summary>
    public const int DetailMonths = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HistoryArchiveStore _archive;

    public string HistoryPath { get; }

    public HistoryStore(string? historyPath = null, HistoryArchiveStore? archive = null)
    {
        HistoryPath = historyPath ?? DefaultPath();
        _archive = archive ?? new HistoryArchiveStore();
    }

    public static string DefaultPath() =>
        Path.Combine(XdgPaths.DataHome(), XdgPaths.AppDirectory, "history.jsonl");

    public IReadOnlyList<HistoryEntry> LoadAll()
    {
        if (!File.Exists(HistoryPath))
        {
            return Array.Empty<HistoryEntry>();
        }

        var entries = new List<HistoryEntry>();
        try
        {
            foreach (var line in File.ReadLines(HistoryPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<HistoryEntry>(line, JsonOptions);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed lines, keep the rest.
                }
            }
        }
        catch (IOException)
        {
            return Array.Empty<HistoryEntry>();
        }

        return entries;
    }

    /// <summary>
    /// Appends <paramref name="entry"/> and keeps only the <see cref="DetailMonths"/> most
    /// recent calendar months of detail. Any older months are compacted into the
    /// <see cref="HistoryArchiveStore"/> and dropped here. The whole file is rewritten
    /// atomically (temp-file-then-rename).
    /// </summary>
    public void Append(HistoryEntry entry)
    {
        var all = LoadAll().ToList();
        all.Add(entry);

        var byMonth = all
            .GroupBy(e => MonthCompactor.MonthKey(e.Timestamp))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // Newest DetailMonths months stay as detail; everything older is archived.
        var recentMonths = byMonth.Keys
            .OrderByDescending(m => m, StringComparer.Ordinal)
            .Take(DetailMonths)
            .ToHashSet(StringComparer.Ordinal);

        var evicted = byMonth
            .Where(kv => !recentMonths.Contains(kv.Key))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();

        if (evicted.Count > 0)
        {
            _archive.AppendMonths(evicted.Select(kv => MonthCompactor.Compact(kv.Key, kv.Value)));
        }

        var kept = byMonth
            .Where(kv => recentMonths.Contains(kv.Key))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .SelectMany(kv => kv.Value.OrderBy(e => e.Timestamp))
            .ToList();

        var dir = Path.GetDirectoryName(HistoryPath)!;
        Directory.CreateDirectory(dir);

        var lines = kept.Select(e => JsonSerializer.Serialize(e, JsonOptions));
        var content = string.Join('\n', lines) + '\n';

        var tmp = HistoryPath + ".tmp";
        File.WriteAllText(tmp, content);
        try
        {
            File.Replace(tmp, HistoryPath, null);
        }
        catch (IOException) when (!File.Exists(HistoryPath))
        {
            File.Move(tmp, HistoryPath);
        }
    }
}
