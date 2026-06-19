namespace QuestJournal.Cli.IO;

/// <summary>
/// Lightweight argument parser that flattens <c>--flag</c>, <c>--flag=value</c>, <c>--flag value</c>,
/// and positional arguments into flags and positional lists. Unrecognised bare tokens that
/// don't start with <c>-</c> become positional in encounter order.
/// </summary>
public sealed class ArgsParser
{
    private readonly Dictionary<string, string?> _flags = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positional = new();

    public ArgsParser(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];

            // --flag=value
            int eq = a.IndexOf('=');
            if (eq > 1 && a.StartsWith("--"))
            {
                _flags[a[..eq]] = a[(eq + 1)..];
                continue;
            }

            // --flag value
            if (a.StartsWith("-"))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    _flags[a] = args[++i];
                }
                else
                {
                    _flags[a] = null; // boolean flag
                }
                continue;
            }

            _positional.Add(a);
        }
    }

    public IReadOnlyDictionary<string, string?> Flags => _flags;
    public IReadOnlyList<string> Positional => _positional;

    public bool HasFlag(string name) => _flags.ContainsKey(name);
    public string? GetFlagValue(string name) => _flags.TryGetValue(name, out var v) ? v : null;
}
