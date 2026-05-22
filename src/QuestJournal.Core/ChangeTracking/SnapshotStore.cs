using System.Text.Json;
using System.Text.Json.Serialization;
using QuestJournal.Core.IO;

namespace QuestJournal.Core.ChangeTracking;

public sealed class SnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string SnapshotPath { get; }

    public SnapshotStore(string? snapshotPath = null)
    {
        SnapshotPath = snapshotPath ?? DefaultPath();
    }

    public static string DefaultPath() =>
        Path.Combine(XdgPaths.DataHome(), XdgPaths.AppDirectory, "state.json");

    public JournalSnapshot? Load()
    {
        if (!File.Exists(SnapshotPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(SnapshotPath);
            return JsonSerializer.Deserialize<JournalSnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Save(JournalSnapshot snapshot)
    {
        var dir = Path.GetDirectoryName(SnapshotPath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        var tmp = SnapshotPath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(SnapshotPath))
        {
            File.Replace(tmp, SnapshotPath, null);
        }
        else
        {
            File.Move(tmp, SnapshotPath);
        }
    }
}
