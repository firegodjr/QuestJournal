using System.Text.Json;
using System.Text.Json.Serialization;
using QuestJournal.Core.IO;

namespace QuestJournal.Core.ChangeTracking;

/// <summary>
/// Durable long-term archive at <c>~/.local/share/quest-journal/history-archive.jsonl</c>
/// (respects <c>XDG_DATA_HOME</c>). One <see cref="HistoryArchiveMonth"/> per line. Months are
/// compacted here by <see cref="HistoryStore"/> as they age out of the detailed window.
/// </summary>
public sealed class HistoryArchiveStore : IHistoryArchiveStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ArchivePath { get; }

    public HistoryArchiveStore(string? archivePath = null)
    {
        ArchivePath = archivePath ?? DefaultPath();
    }

    public static string DefaultPath() =>
        Path.Combine(XdgPaths.DataHome(), XdgPaths.AppDirectory, "history-archive.jsonl");

    public IReadOnlyList<HistoryArchiveMonth> LoadAll()
    {
        if (!File.Exists(ArchivePath))
        {
            return Array.Empty<HistoryArchiveMonth>();
        }

        var months = new List<HistoryArchiveMonth>();
        try
        {
            foreach (var line in File.ReadLines(ArchivePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var month = JsonSerializer.Deserialize<HistoryArchiveMonth>(line, JsonOptions);
                    if (month is not null)
                    {
                        months.Add(month);
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
            return Array.Empty<HistoryArchiveMonth>();
        }

        return months;
    }

    /// <summary>
    /// Merges <paramref name="newMonths"/> into the archive, deduped by <see cref="HistoryArchiveMonth.Month"/>
    /// (a later month with the same key replaces the earlier one). The whole file is rewritten
    /// atomically (temp-file-then-rename), ordered oldest month first.
    /// </summary>
    public void AppendMonths(IEnumerable<HistoryArchiveMonth> newMonths)
    {
        var byMonth = new Dictionary<string, HistoryArchiveMonth>(StringComparer.Ordinal);
        foreach (var month in LoadAll())
        {
            byMonth[month.Month] = month;
        }
        foreach (var month in newMonths)
        {
            byMonth[month.Month] = month;
        }

        var dir = Path.GetDirectoryName(ArchivePath)!;
        Directory.CreateDirectory(dir);

        var ordered = byMonth.Values.OrderBy(m => m.Month, StringComparer.Ordinal);
        var lines = ordered.Select(m => JsonSerializer.Serialize(m, JsonOptions));
        var content = string.Join('\n', lines) + '\n';

        var tmp = ArchivePath + ".tmp";
        File.WriteAllText(tmp, content);
        try
        {
            File.Replace(tmp, ArchivePath, null);
        }
        catch (IOException) when (!File.Exists(ArchivePath))
        {
            File.Move(tmp, ArchivePath);
        }
    }
}
