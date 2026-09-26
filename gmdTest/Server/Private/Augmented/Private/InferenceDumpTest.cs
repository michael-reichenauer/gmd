using System.Text;
using gmd.Git;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// Dumps what the branch inference makes of a real repository, one line per commit, so that a change
// to the rules can be judged by the diff of two dumps over real history rather than by a green suite
// alone. Only run when asked, by naming the repository to read and the file to write:
//
//   GMD_INFER_REPO=<repo> GMD_INFER_OUT=<file> dotnet test gmdTest/gmdTest.csproj --filter InferenceDump
//
// The repository is only read: the log, the branches, the tags, the stashes, gmd's metadata and the
// reflog. Not the status, since 'git status' may rewrite the index, and the inference has no use for
// it. The dump holds no times and no paths, so dumps of the same repository state are identical, and
// any difference between two of them is a difference in what the inference decided. Dump a frozen
// copy of a repository (its '.git' copied as it is, which keeps the reflog a clone would lose), not
// one that commits and fetches move on between the two dumps.
//
// The reflog is the one record of which branch a commit was made on, as far back as it still goes,
// so the dump also scores the inference against it: for every commit the reflog says was made on a
// branch, whether the inference put it on a branch of that name.
[TestClass]
[TestCategory("Integration")]
public class InferenceDumpTest
{
    const int MaxCommitCount = 30000; // As many as AugmentedService reads

    static string RepoPath => Environment.GetEnvironmentVariable("GMD_INFER_REPO") ?? "";
    static string OutPath => Environment.GetEnvironmentVariable("GMD_INFER_OUT") ?? "";

    [TestMethod]
    public async Task DumpInference()
    {
        if (RepoPath == "" || OutPath == "")
            Assert.Inconclusive("Only run when GMD_INFER_REPO and GMD_INFER_OUT name a repository and a file");

        var cmd = new Cmd();
        var git = TempRepo.NewGit(cmd);

        var repo = await RepoBuilder.NewAugmenter().GetAugRepoAsync(await ReadGitRepoAsync(git));
        var madeOn = await ReadMadeOnBranchAsync(cmd);

        File.WriteAllText(OutPath, Dump(repo, madeOn));
    }

    static async Task<GitRepo> ReadGitRepoAsync(IGit git)
    {
        var log = AssertOk(await git.GetLogAsync(MaxCommitCount, RepoPath));
        var branches = AssertOk(await git.GetBranchesAsync(RepoPath));
        var tags = AssertOk(await git.GetTagsAsync(RepoPath));
        var stashes = AssertOk(await git.GetStashesAsync(RepoPath));
        var metaData = AssertOk(await new MetaDataService(git, new FakeRepoConfig()).GetMetaDataAsync(RepoPath));

        return new GitRepo(
            DateTime.UtcNow,
            RepoPath,
            log,
            branches,
            tags,
            RepoBuilder.NoChanges,
            metaData,
            stashes,
            log.Count == MaxCommitCount
        );
    }

    // The branch each commit was made on, by commit id, as far as the reflog still tells. A local
    // branch's own reflog says it for the commits made on it ('commit: ...', 'commit (merge): ...');
    // HEAD's reflog says it for the branches since deleted, whose own reflogs went with them, by
    // following which branch was checked out ('checkout: moving from a to b') when each commit was
    // made. Moves that made no commit (a fast-forward, a reset, a rebase) say nothing about where a
    // commit was made, and neither do the commits a rebase makes, on a detached HEAD.
    static async Task<IReadOnlyDictionary<string, string>> ReadMadeOnBranchAsync(ICmd cmd)
    {
        Dictionary<string, string> madeOn = [];

        var branchesLog = AssertOk(await cmd.RunAsync("git", "reflog show --all --format=%H%x00%gD%x00%gs", RepoPath));
        foreach (var (id, reference, subject) in ReflogEntries(branchesLog))
        {
            if (!reference.StartsWith("refs/heads/") || !IsMadeHere(subject))
                continue;
            var branch = reference.TrimPrefix("refs/heads/");
            branch = branch[..branch.LastIndexOf("@{")];
            madeOn.TryAdd(id, branch);
        }

        // HEAD's reflog is newest first, so it is followed from the end to know the checked out branch
        var headLog = AssertOk(await cmd.RunAsync("git", "reflog show --format=%H%x00%gD%x00%gs HEAD", RepoPath));
        string? current = null;
        foreach (var (id, _, subject) in ReflogEntries(headLog).Reverse())
        {
            if (subject.StartsWith("checkout: moving from "))
            {
                var to = subject[(subject.LastIndexOf(" to ") + 4)..];
                current = IsCommitId(to) ? null : to;
            }
            else if (subject.StartsWith("rebase") && subject.Contains("returning to refs/heads/"))
            {
                current = subject[(subject.IndexOf("returning to refs/heads/") + 24)..];
            }
            else if (subject.StartsWith("rebase"))
            {
                current = null; // A rebase runs on a detached HEAD
            }
            else if (current != null && IsMadeHere(subject))
            {
                madeOn.TryAdd(id, current);
            }
        }

        return madeOn;
    }

    static IEnumerable<(string id, string reference, string subject)> ReflogEntries(string output) =>
        output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\0'))
            .Where(parts => parts.Length == 3)
            .Select(parts => (parts[0], parts[1], parts[2]));

    // Whether a reflog entry is a commit being made, rather than the branch moving to an existing one
    static bool IsMadeHere(string subject) =>
        subject.StartsWith("commit")
        || subject.StartsWith("cherry-pick")
        || subject.StartsWith("revert")
        || subject.Contains(": Merge made by");

    static bool IsCommitId(string text) => text.Length >= 7 && text.All(Uri.IsHexDigit);

    static string Dump(WorkRepo repo, IReadOnlyDictionary<string, string> madeOn)
    {
        var commits = repo.Commits.Where(c => !c.IsTruncatedLogCommit).ToList();
        var text = new StringBuilder();

        text.AppendLine($"# Inference dump of {Path.GetFileName(Path.GetFullPath(RepoPath).TrimEnd('/'))}");
        text.AppendLine(
            $"# {commits.Count} commits, {commits.Count(c => c.IsAmbiguous)} ambiguous "
                + $"({commits.Count(c => c.IsAmbiguousTip)} ambiguous tips), "
                + $"{repo.Branches.Values.Count(b => b.IsGitBranch)} git branches, "
                + $"{repo.Branches.Values.Count(b => !b.IsGitBranch)} recovered branches"
        );

        text.AppendLine("#");
        text.AppendLine("# Decided by:");
        foreach (var group in commits.GroupBy(c => c.DecidedBy).OrderByDescending(g => g.Count()).ThenBy(g => g.Key))
        {
            text.AppendLine($"#   {group.Key, -40} {group.Count(), 6}");
        }

        AppendReflogScore(text, repo, madeOn);

        text.AppendLine("#");
        text.AppendLine("# Columns: commit, flags (A ambiguous, T ambiguous tip, L likely, U set by the user),");
        text.AppendLine("# parents in the order gmd uses (a pull merge's are swapped), deciding rule, branch, subject");
        foreach (var c in commits)
        {
            var parents = string.Join(",", c.ParentIds.Select(id => id.Sid()));
            text.AppendLine($"{c.Sid} {Flags(c)} {parents, -13} {c.DecidedBy, -40} {c.Branch?.Name, -40} {c.Subject}");
        }

        return text.ToString();
    }

    static void AppendReflogScore(StringBuilder text, WorkRepo repo, IReadOnlyDictionary<string, string> madeOn)
    {
        var known = madeOn.Where(pair => repo.CommitsById.ContainsKey(pair.Key)).ToList();
        var disagreeing = known
            .Select(pair => (commit: repo.CommitsById[pair.Key], branch: pair.Value))
            .Where(fact => fact.commit.Branch?.NiceName != fact.branch)
            .OrderBy(fact => fact.commit.GitIndex)
            .ToList();

        text.AppendLine("#");
        text.AppendLine(
            $"# Reflog: {known.Count} commits in the log were made on a known branch, "
                + $"{known.Count - disagreeing.Count} agree, {disagreeing.Count} disagree "
                + $"({madeOn.Count - known.Count} more no longer in the log)"
        );
        foreach (var (commit, branch) in disagreeing)
        {
            text.AppendLine($"#   {commit.Sid} {Flags(commit)} made on {branch}, inferred {commit.Branch?.Name}");
        }
    }

    static string Flags(WorkCommit c) =>
        $"{(c.IsAmbiguous ? 'A' : '-')}{(c.IsAmbiguousTip ? 'T' : '-')}"
        + $"{(c.IsLikely ? 'L' : '-')}{(c.IsBranchSetByUser ? 'U' : '-')}";
}
