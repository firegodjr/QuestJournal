namespace QuestJournal.Core.IO;

internal static class XdgPaths
{
    public const string AppDirectory = "quest-journal";

    public static string ConfigHome() => Resolve("XDG_CONFIG_HOME", ".config");

    public static string DataHome() => Resolve("XDG_DATA_HOME", Path.Combine(".local", "share"));

    public static string StateHome() => Resolve("XDG_STATE_HOME", Path.Combine(".local", "state"));

    private static string Resolve(string envVar, string fallbackUnderHome)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            fallbackUnderHome);
    }
}
