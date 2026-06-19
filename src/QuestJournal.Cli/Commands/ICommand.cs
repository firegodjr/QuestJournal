namespace QuestJournal.Cli.Commands;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
    int Run(string[] args);
}
