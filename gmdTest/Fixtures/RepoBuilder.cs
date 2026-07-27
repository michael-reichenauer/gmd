using gmd.Server.Private.Augmented.Private;
using GitBranch = gmd.Git.Branch;
using GitCommit = gmd.Git.Commit;
using GitStash = gmd.Git.Stash;
using GitStatus = gmd.Git.Status;
using GitTag = gmd.Git.Tag;

namespace gmdTest.Fixtures;

// RepoBuilder builds a GitRepo, i.e. the raw facts git would report, so the augmentation
// pipeline can be tested without running git.
//
// Commits are declared newest first, the same order as 'git log --date-order' returns them,
// and are referred to by short names ('c1', 'c2', ...) which are expanded to full 40 character
// commit ids. Use hex characters in names so the ids stay realistic, and keep the first 6
// characters unique since that is what Sid() shortens to.
//
// Use like e.g.:
//     var workRepo = await new RepoBuilder()
//         .Commit("c3", "Merge branch 'dev' into main", "c2", "b1")
//         .Commit("b1", "Feature", "c1")
//         .Commit("c2", "Work", "c1")
//         .Commit("c1", "Initial")
//         .BranchWithRemote("main", "c3", isCurrent: true)
//         .AugmentAsync();
//     Assert.AreEqual("origin/main", workRepo.CommitsById[RepoBuilder.Sha("c2")].Branch!.Name);
class RepoBuilder
{
    // Fixed so that repeated runs produce identical repos. Branch view naming orders branches
    // by the author time of their bottom commit, so the times must be distinct and ordered.
    static readonly DateTime BaseTime = new DateTime(2024, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    readonly List<GitCommit> commits = new List<GitCommit>();
    readonly List<GitBranch> branches = new List<GitBranch>();
    readonly List<GitTag> tags = new List<GitTag>();
    readonly List<GitStash> stashes = new List<GitStash>();
    readonly MetaData metaData = new MetaData();

    GitStatus status = NoChanges;
    bool isTruncated = false;
    string path = "/test/repo";

    // Expands a short commit name to a full 40 character commit id
    public static string Sha(string name)
    {
        if (name == "")
            throw new ArgumentException("Commit name cannot be empty", nameof(name));
        if (name.Length > 40)
            throw new ArgumentException($"Commit name '{name}' is longer than a commit id", nameof(name));

        return name.PadRight(40, '0');
    }

    // Short id, as shown in the UI, of the commit with the given short name
    public static string Sid(string name) => Sha(name).Sid();

    public static GitStatus NoChanges => new GitStatus(0, 0, 0, 0, 0, false, "", "", [], [], [], [], [], []);

    // Adds a commit. Declare newest first. The first line of 'message' becomes the subject,
    // which is what branch names are recovered from for merge commits.
    public RepoBuilder Commit(string name, string message, params string[] parents)
    {
        var id = Sha(name);
        var subject = message.Split('\n')[0].TrimEnd();

        // Newest declared gets the latest time, so times decrease as commits are declared
        var authorTime = BaseTime.AddMinutes(-commits.Count);

        commits.Add(
            new GitCommit(
                id,
                id.Sid(),
                parents.Select(Sha).ToArray(),
                subject,
                message,
                "Test Author",
                authorTime,
                authorTime
            )
        );
        return this;
    }

    // A local branch. 'remoteName' is the name of its corresponding remote branch, if any.
    public RepoBuilder LocalBranch(
        string name,
        string tipCommit,
        bool isCurrent = false,
        string remoteName = "",
        int ahead = 0,
        int behind = 0
    )
    {
        branches.Add(new GitBranch(name, Sha(tipCommit), isCurrent, false, remoteName, false, ahead, behind));
        return this;
    }

    // A remote branch, the name is expected to include the remote, e.g. "origin/main"
    public RepoBuilder RemoteBranch(string name, string tipCommit)
    {
        branches.Add(new GitBranch(name, Sha(tipCommit), false, true, "", false, 0, 0));
        return this;
    }

    // The common case: a local branch and its corresponding remote branch. Both point at the
    // same tip unless 'remoteTipCommit' is given (i.e. local and remote have diverged).
    public RepoBuilder BranchWithRemote(
        string name,
        string tipCommit,
        bool isCurrent = false,
        string remoteTipCommit = "",
        int ahead = 0,
        int behind = 0
    )
    {
        var remoteName = $"origin/{name}";
        LocalBranch(name, tipCommit, isCurrent, remoteName, ahead, behind);
        RemoteBranch(remoteName, remoteTipCommit == "" ? tipCommit : remoteTipCommit);
        return this;
    }

    // A detached HEAD, i.e. a commit is checked out rather than a branch. Named "DETACHED" to
    // match what the real BranchService reports for '(HEAD detached at ...)'.
    public RepoBuilder DetachedHead(string tipCommit)
    {
        branches.Add(new GitBranch("DETACHED", Sha(tipCommit), true, false, "", true, 0, 0));
        return this;
    }

    public RepoBuilder Tag(string name, string commitName)
    {
        tags.Add(new GitTag(name, Sha(commitName)));
        return this;
    }

    public RepoBuilder Stash(string name, string parentCommit, string message = "stash message")
    {
        var id = Sha($"5{stashes.Count}a5h");
        stashes.Add(new GitStash(id, name, "main", Sha(parentCommit), Sha($"5{stashes.Count}1nd"), message));
        return this;
    }

    // Records that the user manually set the branch of a commit, as stored in the repo metadata
    public RepoBuilder UserSetBranch(string commitName, string branchName)
    {
        metaData.SetCommitBranch(Sid(commitName), branchName);
        return this;
    }

    // Records that a commit is known to be the branch out point of a branch
    public RepoBuilder Branched(string commitName, string branchName)
    {
        metaData.SetBranched(Sid(commitName), branchName);
        return this;
    }

    // Records that the user removed a previously set branch, as UnresolveAmbiguityAsync does.
    // The entry is kept but emptied, so the removal itself can be shared with other users.
    public RepoBuilder UnsetBranch(string commitName)
    {
        metaData.RemoveCommitBranch(Sid(commitName));
        return this;
    }

    // A repo with no commits, i.e. the virtual commit and branch that the AugmentedService
    // substitutes for a repo where 'git log' returned nothing.
    public RepoBuilder EmptyRepo()
    {
        var id = gmd.Server.Repo.EmptyRepoCommitId;
        var msg = "<... empty repo ...>";
        commits.Add(new GitCommit(id, id.Sid(), [], msg, msg, "", BaseTime, BaseTime));
        branches.Add(new GitBranch("main", id, true, false, "", false, 0, 0));
        return this;
    }

    // Marks the log as truncated, as done for repos with very many commits. Commits whose
    // parents are missing then get a virtual truncated parent commit.
    public RepoBuilder Truncated()
    {
        isTruncated = true;
        return this;
    }

    public RepoBuilder WithStatus(
        int modified = 0,
        int added = 0,
        int deleted = 0,
        int conflicted = 0,
        bool isMerging = false,
        string mergeMessage = "",
        string mergeHeadCommit = ""
    )
    {
        status = new GitStatus(
            modified,
            added,
            deleted,
            conflicted,
            0,
            isMerging,
            mergeMessage,
            mergeHeadCommit == "" ? "" : Sha(mergeHeadCommit),
            [],
            [],
            [],
            [],
            [],
            []
        );
        return this;
    }

    public RepoBuilder AtPath(string repoPath)
    {
        path = repoPath;
        return this;
    }

    public GitRepo ToGitRepo() =>
        new GitRepo(BaseTime, path, commits, branches, tags, status, metaData, stashes, isTruncated);

    // Runs the real augmentation pipeline (branch inference, hierarchy, view names)
    public Task<WorkRepo> AugmentAsync() => NewAugmenter().GetAugRepoAsync(ToGitRepo());

    // The augmenter with its real collaborators, none of which touch git, disk or the terminal
    public static IAugmenter NewAugmenter() => new Augmenter(new BranchStructureService(new BranchNameService()));
}
