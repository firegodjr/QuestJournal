using QuestJournal.Cli.ChangeTracking;
using QuestJournal.Cli.Commands;
using QuestJournal.Cli.IO;
using QuestJournal.Cli.Rendering;
using QuestJournal.Core.Configuration;
using QuestJournal.Core.Model;
using QuestJournal.Core.Parsing;
using Spectre.Console;

namespace QuestJournal.Cli;

public sealed class JournalSession
{
    public Config Config { get; }
    public string FilePath { get; }
    public bool FileOverridden { get; }
    public JournalDocument Document { get; }
    public QuestTheme Theme { get; }
    public ChangeTrackingPipeline Pipeline { get; }
    public IAnsiConsole Console { get; }
    public ConsoleReporter Reporter { get; }

    private JournalSession(
        Config config,
        string filePath,
        bool fileOverridden,
        JournalDocument document,
        QuestTheme theme,
        ChangeTrackingPipeline pipeline,
        IAnsiConsole console,
        ConsoleReporter reporter)
    {
        Config = config;
        FilePath = filePath;
        FileOverridden = fileOverridden;
        Document = document;
        Theme = theme;
        Pipeline = pipeline;
        Console = console;
        Reporter = reporter;
    }

    public static JournalSession Open(string? fileOverride, bool requireConfig, IAnsiConsole? console = null)
    {
        var c = console ?? AnsiConsole.Console;
        var reporter = new ConsoleReporter(c);
        var config = LoadConfig(requireConfig, reporter);
        var filePath = fileOverride ?? config.FilePath;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            reporter.Error("File not found", filePath ?? string.Empty);
            throw new JournalSessionException(reported: true);
        }

        var document = new JournalParser().ParseFile(filePath);
        var theme = config.NerdFontGlyphs ? QuestTheme.NerdFont : QuestTheme.Ascii;
        var pipeline = new ChangeTrackingPipeline(theme, c);

        return new JournalSession(config, filePath, fileOverride is not null, document, theme, pipeline, c, reporter);
    }

    private static Config LoadConfig(bool requireConfig, ConsoleReporter reporter)
    {
        try
        {
            return new ConfigStore().Load();
        }
        catch (ConfigMissingException ex)
        {
            if (requireConfig)
            {
                reporter.Error("Config", ex.Message);
                throw new JournalSessionException(reported: true);
            }
            reporter.Warn("Config", ex.Message);
            return new Config();
        }
    }
}
