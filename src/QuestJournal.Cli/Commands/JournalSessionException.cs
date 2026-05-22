namespace QuestJournal.Cli.Commands;

public sealed class JournalSessionException : Exception
{
    public int ExitCode { get; }
    public bool Reported { get; }

    public JournalSessionException(string message, int exitCode = 1)
        : base(message)
    {
        ExitCode = exitCode;
        Reported = false;
    }

    public JournalSessionException(bool reported, int exitCode = 1)
        : base(string.Empty)
    {
        ExitCode = exitCode;
        Reported = reported;
    }
}
