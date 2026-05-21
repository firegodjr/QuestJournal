using QuestJournal.Core.ChangeTracking;
using QuestJournal.Core.Model;
using Spectre.Console;

namespace QuestJournal.Cli.Rendering;

public sealed class DiffRenderer
{
    private readonly GlyphTheme _theme;

    public DiffRenderer(GlyphTheme theme)
    {
        _theme = theme;
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
            var dayNode = root.AddNode($"[bold]# {Markup.Escape(dayGroup.Key)}[/]");
            foreach (var entry in dayGroup)
            {
                var catNode = dayNode.AddNode($"[bold dim]## {Markup.Escape(entry.Key.Category)}[/]");
                BuildAndEmit(catNode, entry.Value);
            }
        }

        AnsiConsole.Write(root);
        AnsiConsole.WriteLine();
    }

    public void RenderXpFooter(long xpAwarded, long totalXp)
    {
        var sparkle = $"[yellow1]{Markup.Escape(_theme.Xp)}[/]";
        if (xpAwarded > 0)
        {
            AnsiConsole.MarkupLine($"{sparkle} [green]+{xpAwarded}earned[/] · [yellow1]{totalXp} total[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"{sparkle} [yellow1]{totalXp} total[/]");
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
                $"[green]+[/] {QuestStyles.StyleGlyph(a.Status, _theme.GlyphFor(a.Status))} " +
                $"{QuestStyles.StyleText(a.Status, name)}{xpSuffix}",

            Change.Removed =>
                $"[grey strikethrough]- {Markup.Escape(name)}[/] [dim](removed)[/]",

            Change.StatusChanged sc =>
                $"{QuestStyles.StyleGlyph(sc.NewStatus, _theme.GlyphFor(sc.NewStatus))} " +
                $"{QuestStyles.StyleText(sc.NewStatus, name)} " +
                $"[dim]({StatusLabel(sc.OldStatus)} → {StatusLabel(sc.NewStatus)})[/]{xpSuffix}",

            _ => Markup.Escape(name),
        };
    }

    private static string StatusLabel(QuestStatus status) => status switch
    {
        QuestStatus.None => "None",
        QuestStatus.Open => "Open",
        QuestStatus.Active => "Active",
        QuestStatus.Cancelled => "Cancelled",
        QuestStatus.Warning => "Warning",
        QuestStatus.Completed => "Completed",
        QuestStatus.Comment => "Comment",
        _ => status.ToString(),
    };

    private sealed class TrieNode
    {
        public string Name { get; set; } = string.Empty;
        public Change? Change { get; set; }
        public Dictionary<string, TrieNode> Children { get; } = new();
    }
}
