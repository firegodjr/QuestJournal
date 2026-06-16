using System.Text.Json;
using System.Text.Json.Serialization;
using QuestJournal.Core.IO;

namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// Append-only, durable log of change batches at
/// <c>~/.local/share/quest-journal/history.jsonl</c> (respects <c>XDG_DATA_HOME</c>).
/// One <see cref="HistoryEntry"/> per line. Old entries are pruned on each append.
/// </summary>
public sealed class HistoryStore
{
    public const int RetentionDays = 90;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public string HistoryPath { get; }

    public HistoryStore(string? historyPath = null)
    {
        HistoryPath = historyPath ?? DefaultPath();
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
    /// Appends <paramref name="entry"/>, dropping any existing entries older than
    /// <paramref name="retention"/> relative to the new entry's timestamp. The whole
    /// file is rewritten atomically (temp-file-then-rename).
    /// </summary>
    public void Append(HistoryEntry entry, TimeSpan retention)
    {
        var cutoff = entry.Timestamp - retention;
        var kept = LoadAll().Where(e => e.Timestamp >= cutoff).ToList();
        kept.Add(entry);

        var dir = Path.GetDirectoryName(HistoryPath)!;
        Directory.CreateDirectory(dir);

        var lines = kept.Select(e => JsonSerializer.Serialize(e, JsonOptions));
        var content = string.Join('\n', lines) + '\n';

        var tmp = HistoryPath + ".tmp";
        File.WriteAllText(tmp, content);
        if (File.Exists(HistoryPath))
        {
            File.Replace(tmp, HistoryPath, null);
        }
        else
        {
            File.Move(tmp, HistoryPath);
        }
    }
}
