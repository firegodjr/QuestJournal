namespace QuestJournal.Core.IO;

/// <summary>
/// Reads the list of repositories lazygit has recently opened from its <c>state.yml</c>. Only the
/// top-level <c>recentrepos:</c> list is needed, so the file is parsed line-by-line rather than
/// pulling in a YAML dependency. The state file lives under the XDG state dir on modern lazygit
/// (<c>~/.local/state/lazygit/state.yml</c>); the older config-dir location is tried as a fallback.
/// </summary>
public static class LazygitRecentRepos
{
    /// <summary>
    /// Loads recent repos from the on-disk state file (state dir, then config dir), keeping only
    /// paths that exist and look like a git working tree (a <c>.git</c> directory or, for
    /// worktrees, a <c>.git</c> file). Returns empty if no state file is found.
    /// </summary>
    public static IReadOnlyList<string> Load()
    {
        var path = LocateStateFile();
        if (path is null)
        {
            return Array.Empty<string>();
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }

        return ParseRecentRepos(lines)
            .Where(IsGitWorkTree)
            .ToList();
    }

    /// <summary>
    /// Extracts and normalizes the <c>recentrepos:</c> list from raw YAML lines. Pure (no I/O):
    /// trims trailing slashes, resolves to full paths, and de-duplicates while preserving order
    /// (so e.g. <c>/x</c> and <c>/x/</c> collapse to one). Unknown/missing key → empty.
    /// </summary>
    public static IReadOnlyList<string> ParseRecentRepos(IEnumerable<string> lines)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var inList = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!inList)
            {
                if (trimmed == "recentrepos:")
                {
                    inList = true;
                }
                continue;
            }

            if (trimmed.Length == 0)
            {
                continue; // tolerate blank lines inside the block
            }
            if (!trimmed.StartsWith('-'))
            {
                break; // dedented to the next top-level key → end of list
            }

            var raw = trimmed.TrimStart('-').Trim().Trim('"', '\'');
            if (raw.Length == 0)
            {
                continue;
            }

            var normalized = Normalize(raw);
            if (seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static string Normalize(string path)
    {
        var trimmed = path.TrimEnd('/');
        if (trimmed.Length == 0)
        {
            trimmed = "/";
        }
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception)
        {
            return trimmed;
        }
    }

    private static bool IsGitWorkTree(string repo)
    {
        if (!Directory.Exists(repo))
        {
            return false;
        }
        var dotGit = Path.Combine(repo, ".git");
        return Directory.Exists(dotGit) || File.Exists(dotGit);
    }

    private static string? LocateStateFile()
    {
        var candidates = new[]
        {
            Path.Combine(XdgPaths.StateHome(), "lazygit", "state.yml"),
            Path.Combine(XdgPaths.ConfigHome(), "lazygit", "state.yml"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
