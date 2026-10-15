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
// merged into are scored the same way, which works for a clone too, where there is no reflog, and so
// are the ordinary commits that came in through a merge whose subject names the branch merged in. The
// header also lists the branches each ambiguous tip could be on, which is what the user is asked.
[TestClass]
[TestCategory("Integration")]
public class InferenceDumpTest
{
    const int MaxCommitCount = AugmentedService.MaxCommitCount;

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
        AppendMergedInScore(text, repo, commits);
        AppendAmbiguousTips(text, commits);

        text.AppendLine("#");
        text.AppendLine(
            "# Columns: commit, flags (A ambiguous, T ambiguous tip, L likely, U set by the user, S parents"
        );
        text.AppendLine("# swapped), parents in the order gmd uses, deciding rule, branch, subject");
        foreach (var c in commits)
        {
            var parents = string.Join(",", c.ParentIds.Select(Id));
            text.AppendLine(
                $"{Id(c.Id)} {Flags(c)} {parents, -17} {c.DecidedBy, -40} {c.Branch?.Name, -40} {c.Subject}"
            );
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
            text.AppendLine($"#   {Id(commit.Id)} {Flags(commit)} made on {into}, inferred {commit.Branch?.Name}");
        }
    }

    // A merge subject that names the branch merged in, "Merge branch 'a' into b" or "Merge pull request
    // #1 from o/a", says the commits that came in through that merge were a's: the first-parent line of
    // the merged parent, down to where it meets the history of the branch merged into. That scores the
    // ordinary commits, which the two scores above do not reach. It is partly circular, since the
    // inference names a deleted branch by the same subject, so it shows where other evidence overruled
    // a subject rather than how often the subject is right. The parents are taken in git's order, as
    // the subject means them, not as gmd swaps a pull merge's. A commit two names claim is left out.
    //
    // One commit merged into two branches, e.g. a git-flow release merged into master and develop,
    // claims the shorter of the two lines: the longer runs on below where the branch was started, into
    // the commits of the branch it was started from that the other target did not have yet. A trunk's
    // name claims the trunk whatever it is called now, since 'master' renamed 'main' is no error.
    static void AppendMergedInScore(StringBuilder text, WorkRepo repo, IReadOnlyList<WorkCommit> commits)
    {
        var parser = new BranchNameService();
        Dictionary<(string name, string merged), List<WorkCommit>> lines = [];
        foreach (var merge in commits.Where(c => c.ParentIds.Count == 2))
        {
            var from = parser.ParseSubject(merge.Subject).From;
            if (
                from == ""
                || IsTagMerge(merge)
                || !repo.CommitsById.TryGetValue(GitParent(merge, 0), out var into)
                || !repo.CommitsById.TryGetValue(GitParent(merge, 1), out var merged)
            )
                continue;

            var line = LineDownTo(repo, merged, into);
            if (!lines.TryGetValue((from, merged.Id), out var other) || line.Count < other.Count)
                lines[(from, merged.Id)] = line;
        }

        Dictionary<string, string> claims = [];
        foreach (var ((from, _), line) in lines)
        {
            foreach (var c in line)
            {
                claims[c.Id] = claims.TryGetValue(c.Id, out var claim) && claim != from ? "" : from;
            }
        }

        var claimed = claims.Where(p => p.Value != "").Select(p => (commit: repo.CommitsById[p.Key], name: p.Value));
        var disagreeing = claimed.Where(m => !IsOnNamedBranch(m.commit, m.name)).ToList();

        text.AppendLine("#");
        text.AppendLine(
            $"# Merged in: {claimed.Count()} commits came in through a merge whose subject names the branch merged "
                + $"in, {claimed.Count() - disagreeing.Count} are on a branch of that name, "
                + $"{disagreeing.Count(m => m.commit.IsAmbiguous)} of the rest ambiguous "
                + $"({claims.Count(p => p.Value == "")} claimed by two names)"
        );
        foreach (
            var group in disagreeing
                .GroupBy(m => (m.name, branch: m.commit.Branch?.Name ?? ""))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key.name, StringComparer.Ordinal)
                .ThenBy(g => g.Key.branch, StringComparer.Ordinal)
        )
        {
            var ambiguous = group.Count(m => m.commit.IsAmbiguous);
            text.AppendLine(
                $"#   {group.Count(), 5} {(ambiguous > 0 ? $"({ambiguous} A)" : ""), -8} "
                    + $"merged in from {group.Key.name}, inferred {group.Key.branch}, e.g. {Id(group.First().commit.Id)}"
            );
        }
    }

    // A merged tag names no branch: git-flow merges a release's tag into develop, so the release
    // branch's only merge is into master, whose line would claim develop's commits below it
    static bool IsTagMerge(WorkCommit merge) => merge.Subject.StartsWith("Merge tag ");

    // Whether a commit is on the branch a subject names, also with an owner in front of either name,
    // and the trunk by any trunk's name, or by a remote's alone, as 'git merge upstream' writes it
    static bool IsOnNamedBranch(WorkCommit c, string name) =>
        c.Branch?.NiceName is string n
        && (
            n == name
            || n.EndsWith("/" + name)
            || name.EndsWith("/" + n)
            || (c.Branch.IsMainBranch && (WellKnownBranches.IsTrunkName(name) || name is "origin" or "upstream"))
        );

    // A merge's parent in git's order, which gmd has swapped for a pull merge or a foxtrot merge
    static string GitParent(WorkCommit merge, int index) => merge.ParentIds[merge.IsParentsSwapped ? 1 - index : index];

    // The first-parent line, in git's order, from a commit down to where it meets the history of
    // another. The log has every commit before its parents, so whether a commit is in that history is
    // known once the history is walked in log order as far down as the commit, and no further.
    static List<WorkCommit> LineDownTo(WorkRepo repo, WorkCommit from, WorkCommit other)
    {
        HashSet<string> reached = [other.Id];
        PriorityQueue<WorkCommit, int> toWalk = new();
        toWalk.Enqueue(other, other.GitIndex);

        List<WorkCommit> line = [];
        for (var c = from; c != null; )
        {
            while (toWalk.TryPeek(out var next, out var index) && index < c.GitIndex)
            {
                toWalk.Dequeue();
                foreach (var id in next.ParentIds)
                {
                    if (repo.CommitsById.TryGetValue(id, out var parent) && reached.Add(id))
                        toWalk.Enqueue(parent, parent.GitIndex);
                }
            }
            if (reached.Contains(c.Id) || c.IsTruncatedLogCommit)
                break;

            line.Add(c);
            c = c.ParentIds.Count > 0 ? repo.CommitsById.GetValueOrDefault(GitParent(c, 0)) : null;
        }
        return line;
    }

    // The branches each ambiguous tip could be on, which is what the user is asked to choose between
    static void AppendAmbiguousTips(StringBuilder text, IReadOnlyList<WorkCommit> commits)
    {
        text.AppendLine("#");
        text.AppendLine("# Ambiguous tips, with the branches they could be on:");
        foreach (var c in commits.Where(c => c.IsAmbiguousTip))
        {
            var candidates = c.Branch!.AmbiguousBranches.Select(b => b.Name).Order(StringComparer.Ordinal);
            text.AppendLine($"#   {Id(c.Id)} {c.Branch.Name}: {string.Join(", ", candidates)}");
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
            text.AppendLine($"#   {Id(commit.Id)} {Flags(commit)} made on {branch}, inferred {commit.Branch?.Name}");
        }
    }

    // A commit id as the dump writes it, longer than gmd's 6 characters, of which 30,000 commits have
    // dozens in common, which would make a dump ambiguous to read
    static string Id(string id) => id[..Math.Min(8, id.Length)];

    static string Flags(WorkCommit c) =>
        $"{(c.IsAmbiguous ? 'A' : '-')}{(c.IsAmbiguousTip ? 'T' : '-')}"
        + $"{(c.IsLikely ? 'L' : '-')}{(c.IsBranchSetByUser ? 'U' : '-')}{(c.IsParentsSwapped ? 'S' : '-')}";
}
