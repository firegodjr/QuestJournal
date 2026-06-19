using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

public sealed class DiffRenderer
{
    private readonly QuestTheme _theme;
    private readonly IAnsiConsole _console;

    public DiffRenderer(QuestTheme theme, IAnsiConsole console)
    {
        _theme = theme;
        _console = console;
    }

    public void RenderDiffTree(ChangeSet changeSet)
    {
        if (changeSet.IsEmpty) return;

        var grouped = new Dictionary<(string Day, string Category), List<Change>>();
        foreach (var change in changeSet.Changes)
        {
            var key = (change.Key.Day, change.Key.Category);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<Change>();
                grouped[key] = list;
            }
            list.Add(change);
        }

        var root = new Tree("[bold]Changes[/]");

        foreach (var dayGroup in grouped.GroupBy(g => g.Key.Day))
        {
            var dayNode = root.AddNode(QuestTheme.DayHeader(dayGroup.Key));
            foreach (var entry in dayGroup)
            {
                var catNode = dayNode.AddNode(QuestTheme.CategoryHeader(entry.Key.Category));
                BuildAndEmit(catNode, entry.Value);
            }
        }

        _console.Write(root);
        _console.WriteLine();
    }

    public void RenderXpFooter(long xpAwarded, long todayXp, long totalXp)
    {
        var sparkle = $"[yellow1]{Markup.Escape(_theme.XpGlyph)}[/]";
        var tally = $"[yellow1]{todayXp} today[/]  [yellow1]{totalXp} lifetime[/]";
        if (xpAwarded > 0)
        {
            _console.MarkupLine($"[green]+{xpAwarded}XP earned[/]! {sparkle} {tally}");
        }
        else
        {
            _console.MarkupLine($"{sparkle} {tally}");
        }
    }

    private void BuildAndEmit(TreeNode parent, List<Change> changes)
    {
        var trieRoot = new TrieNode();
        foreach (var change in changes)
        {
            var node = trieRoot;
            foreach (var ancestor in change.Key.Ancestors)
            {
                if (!node.Children.TryGetValue(ancestor, out var next))
                {
                    next = new TrieNode { Name = ancestor };
                    node.Children[ancestor] = next;
                }
                node = next;
            }
            if (!node.Children.TryGetValue(change.Key.Text, out var leaf))
            {
                leaf = new TrieNode { Name = change.Key.Text };
                node.Children[change.Key.Text] = leaf;
            }
            leaf.Change = change;
        }

        Walk(trieRoot, parent);
    }

    private void Walk(TrieNode node, TreeNode treeParent)
    {
        foreach (var child in node.Children.Values)
        {
            var label = LabelFor(child);
            var treeChild = treeParent.AddNode(label);
            Walk(child, treeChild);
        }
    }

    private string LabelFor(TrieNode node)
    {
        if (node.Change is null)
        {
            return $"[dim]{Markup.Escape(node.Name)}[/]";
        }

        var name = node.Change.Key.Text;
        var xp = XpCalculator.Award(node.Change);
        var xpSuffix = xp > 0 ? $"  [green]+{xp} XP[/]" : string.Empty;

        return node.Change switch
        {
            Change.Added a =>
                $"[green]+[/] {_theme.StyledGlyph(a.Status)} " +
                $"{_theme.StyledText(a.Status, name)}{xpSuffix}",

            Change.Removed =>
                $"[grey strikethrough]- {Markup.Escape(name)}[/] [dim](removed)[/]",

            Change.StatusChanged sc =>
                $"{_theme.StyledGlyph(sc.NewStatus)} " +
                $"{_theme.StyledText(sc.NewStatus, name)} " +
                $"[dim]({_theme.Label(sc.OldStatus)} → {_theme.Label(sc.NewStatus)})[/]{xpSuffix}",

            _ => throw new InvalidOperationException(
                $"Unexpected change type: {node.Change.GetType().Name}"),
        };
    }

    private sealed class TrieNode
    {
        public string Name { get; set; } = string.Empty;
        public Change? Change { get; set; }
        public Dictionary<string, TrieNode> Children { get; } = new();
    }
}
