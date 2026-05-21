using System.Collections.Immutable;

namespace QuestJournal.Core.ChangeTracking;

public sealed record TaskKey(
    string Day,
    string Category,
    ImmutableArray<string> Ancestors,
    string Text)
{
    public bool Equals(TaskKey? other)
    {
        if (other is null) return false;
        if (Day != other.Day) return false;
        if (Category != other.Category) return false;
        if (Text != other.Text) return false;
        if (Ancestors.Length != other.Ancestors.Length) return false;
        for (int i = 0; i < Ancestors.Length; i++)
        {
            if (Ancestors[i] != other.Ancestors[i]) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Day);
        hash.Add(Category);
        hash.Add(Text);
        foreach (var a in Ancestors) hash.Add(a);
        return hash.ToHashCode();
    }
}
