using System.Text.Json;
using QuestJournal.Core.IO;

namespace QuestJournal.Core.Configuration;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public string ConfigPath { get; }

    public ConfigStore(string? configPath = null)
    {
        ConfigPath = configPath ?? DefaultPath();
    }

    public static string DefaultPath() =>
        Path.Combine(XdgPaths.ConfigHome(), XdgPaths.AppDirectory, "config.json");

    public Config Load()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new ConfigMissingException(
                $"No config found at {ConfigPath}. Create it with at least: {{\"filePath\": \"/path/to/Tasks.md\"}}");
        }

        var json = File.ReadAllText(ConfigPath);
        var config = JsonSerializer.Deserialize<Config>(json, JsonOptions)
                     ?? throw new ConfigMissingException(
                         $"Config at {ConfigPath} parsed to null. Ensure it contains valid JSON with a filePath key.");

        if (string.IsNullOrWhiteSpace(config.FilePath))
        {
            throw new ConfigMissingException(
                $"Config at {ConfigPath} is missing the required \"filePath\" key.");
        }

        return config;
    }

    public void Save(Config config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}

public sealed class ConfigMissingException : Exception
{
    public ConfigMissingException(string message) : base(message) { }
}
