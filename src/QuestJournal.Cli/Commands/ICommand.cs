namespace QuestJournal.Cli.Commands;

public interface ICommand
{
    int Run(string[] args);
}
