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
// branch, whether the inference put it on a branch of that name. Merge subjects that name the branch
// merged into are scored the same way, which works for a clone too, where there is no reflog.
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

        var git = TempRepo.NewGit(new Cmd());
        var reflog = AssertOk(await git.GetReflogAsync(RepoPath));

        var repo = await RepoBuilder.NewAugmenter().GetAugRepoAsync(await ReadGitRepoAsync(git, reflog));

        File.WriteAllText(OutPath, Dump(repo, ReflogWitness.MadeOn(reflog)));
    }

    static async Task<GitRepo> ReadGitRepoAsync(IGit git, IReadOnlyList<ReflogEntry> reflog)
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
            log.Count == MaxCommitCount,
            reflog: reflog
        );
    }

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
        AppendSubjectScore(text, commits);

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

    // A merge subject that names the branch merged into, "Merge branch 'a' into b", says which branch
    // the merge commit was made on, which is a score for any repository, reflog or not. A name matches
    // with or without an owner in front ('owner/b'), as a branch recovered from a pull request has it.
    static void AppendSubjectScore(StringBuilder text, IReadOnlyList<WorkCommit> commits)
    {
        var parser = new BranchNameService();
        var named = commits
            .Where(c => c.ParentIds.Count == 2)
            .Select(c => (commit: c, into: parser.ParseSubject(c.Subject).Into))
            .Where(m => m.into != "")
            .ToList();
        var disagreeing = named
            .Where(m => !(m.commit.Branch?.NiceName is string name && (name == m.into || name.EndsWith("/" + m.into))))
            .ToList();

        text.AppendLine("#");
        text.AppendLine(
            $"# Merge subjects: {named.Count} name the branch merged into, "
                + $"{named.Count - disagreeing.Count} are on a branch of that name"
        );
        foreach (var (commit, into) in disagreeing)
        {
            text.AppendLine($"#   {commit.Sid} {Flags(commit)} made on {into}, inferred {commit.Branch?.Name}");
        }
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
