using QuestJournal.Cli.ChangeTracking;
using QuestJournal.Cli.Commands;
using QuestJournal.Cli.IO;
using QuestJournal.Cli.Rendering;
using QuestJournal.Core.Configuration;
using QuestJournal.Core.Model;
using QuestJournal.Core.Parsing;

namespace QuestJournal.Cli;

public sealed class JournalSession
{
    public Config Config { get; }
    public string FilePath { get; }
    public bool FileOverridden { get; }
    public JournalDocument Document { get; }
    public QuestTheme Theme { get; }
    public ChangeTrackingPipeline Pipeline { get; }

    private JournalSession(
        Config config,
        string filePath,
        bool fileOverridden,
        JournalDocument document,
        QuestTheme theme,
        ChangeTrackingPipeline pipeline)
    {
        Config = config;
        FilePath = filePath;
        FileOverridden = fileOverridden;
        Document = document;
        Theme = theme;
        Pipeline = pipeline;
    }

    public static JournalSession Open(string? fileOverride, bool requireConfig)
    {
        var config = LoadConfig(requireConfig);
        var filePath = fileOverride ?? config.FilePath;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            ConsoleReporter.Error("File not found", filePath ?? string.Empty);
            throw new JournalSessionException(reported: true);
        }

        var document = new JournalParser().ParseFile(filePath);
        var theme = config.NerdFontGlyphs ? QuestTheme.NerdFont : QuestTheme.Ascii;
        var pipeline = new ChangeTrackingPipeline(theme);

        return new JournalSession(config, filePath, fileOverride is not null, document, theme, pipeline);
    }

    private static Config LoadConfig(bool requireConfig)
    {
        try
        {
            return new ConfigStore().Load();
        }
        catch (ConfigMissingException ex)
        {
            if (requireConfig)
            {
                ConsoleReporter.Error("Config", ex.Message);
                throw new JournalSessionException(reported: true);
            }
            ConsoleReporter.Warn("Config", ex.Message);
            return new Config();
        }
    }
}
