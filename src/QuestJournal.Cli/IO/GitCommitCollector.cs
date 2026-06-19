using System.Diagnostics;
using System.Globalization;
using QuestJournal.Core.ChangeTracking;

namespace QuestJournal.Cli.IO;

/// <summary>
/// Collects the current user's commit timestamps from a set of git repositories by shelling out
/// to <c>git log</c> (the only external dependency, like <see cref="Commands.EditCommand"/>).
/// "The current user" is resolved per-repo from that repo's configured <c>user.email</c>
/// (falling back to <c>user.name</c>), so work and personal identities are each matched where
/// they apply. Repos that aren't git, error out, or have no matching commits are skipped.
/// </summary>
public sealed class GitCommitCollector
{
    /// <summary>
    /// Returns one <see cref="RepoCommits"/> per repo that has at least one matching commit.
    /// <paramref name="since"/> bounds the query (git <c>--since</c>); pass null to fetch all
    /// history (used by the "all time" scope).
    /// </summary>
    public IReadOnlyList<RepoCommits> Collect(IReadOnlyList<string> repoPaths, DateOnly? since)
    {
        var results = new List<RepoCommits>(repoPaths.Count);

        foreach (var repo in repoPaths)
        {
            var identity = ResolveIdentity(repo);
            if (identity is null)
            {
                continue;
            }

            var args = new List<string> { "-C", repo, "log", "--all", $"--author={identity}", "--pretty=format:%cI" };
            if (since is { } s)
            {
                args.Add($"--since={s:yyyy-MM-dd}");
            }

            if (!TryRunGit(repo, args, out var stdout))
            {
                continue;
            }

            var commits = new List<DateTimeOffset>();
            foreach (var line in stdout.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 &&
                    DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts))
                {
                    commits.Add(ts);
                }
            }

            if (commits.Count == 0)
            {
                continue;
            }

            results.Add(new RepoCommits
            {
                Name = RepoName(repo),
                FullPath = repo,
                Commits = commits,
            });
        }

        return results;
    }

    private static string RepoName(string repo) =>
        Path.GetFileName(repo.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : repo;

    /// <summary>The repo's configured commit identity: <c>user.email</c>, else <c>user.name</c>.</summary>
    private static string? ResolveIdentity(string repo)
    {
        if (TryRunGit(repo, new[] { "-C", repo, "config", "user.email" }, out var email) &&
            email.Trim() is { Length: > 0 } e)
        {
            return e;
        }
        if (TryRunGit(repo, new[] { "-C", repo, "config", "user.name" }, out var name) &&
            name.Trim() is { Length: > 0 } n)
        {
            return n;
        }
        return null;
    }

    /// <summary>
    /// Runs git with the given arguments, returning stdout. Returns false (and tolerates) any
    /// launch failure or non-zero exit — e.g. a directory that isn't a repo, or a repo with no
    /// commits yet, both of which git reports via a non-zero exit.
    /// </summary>
    private static bool TryRunGit(string workingDir, IReadOnlyList<string> args, out string stdout)
    {
        stdout = string.Empty;
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return false;
            }
            stdout = proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
