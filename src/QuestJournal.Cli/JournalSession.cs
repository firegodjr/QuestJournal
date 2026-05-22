using QuestJournal.Cli.ChangeTracking;
using QuestJournal.Cli.Commands;
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
    public GlyphTheme Theme { get; }
    public ChangeTrackingPipeline Pipeline { get; }

    private JournalSession(
        Config config,
        string filePath,
        bool fileOverridden,
        JournalDocument document,
        GlyphTheme theme,
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
            AnsiConsole.MarkupLine($"[red]File not found:[/] {Markup.Escape(filePath ?? string.Empty)}");
            throw new JournalSessionException(reported: true);
        }

        var document = new JournalParser().ParseFile(filePath);
        var theme = config.NerdFontGlyphs ? GlyphTheme.NerdFont : GlyphTheme.Ascii;
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
                AnsiConsole.MarkupLine($"[red]Config:[/] {Markup.Escape(ex.Message)}");
                throw new JournalSessionException(reported: true);
            }
            AnsiConsole.MarkupLine($"[yellow]Config:[/] {Markup.Escape(ex.Message)}");
            return new Config();
        }
    }
}
