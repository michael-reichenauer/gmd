using gmd.Git;
using gmd.Server;
using gmd.Server.Private;
using gmd.Server.Private.Augmented.Private;
using GitBranch = gmd.Git.Branch;
using GitCommit = gmd.Git.Commit;
using GitOp = gmd.Git.GitOperation;
using GitStash = gmd.Git.Stash;
using GitStatus = gmd.Git.Status;
using GitTag = gmd.Git.Tag;
using ServerImpl = gmd.Server.Private.Server;

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
//
// AugmentAsync() stops at the WorkRepo the augmentation pipeline produces. ViewRepoAsync() goes
// all the way to the Server.Repo the UI renders, i.e. it also adds the uncommitted commit and
// applies the user's choice of which branches to show. Use GraphText to draw that as a picture.
class RepoBuilder
{
    // Fixed so that repeated runs produce identical repos. Branch view naming orders branches
    // by the author time of their bottom commit, so the times must be distinct and ordered.
    static readonly DateTime BaseTime = new DateTime(2024, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    readonly List<GitCommit> commits = [];
    readonly List<GitBranch> branches = [];
    readonly List<GitTag> tags = [];
    readonly List<GitStash> stashes = [];
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

    public static GitStatus NoChanges =>
        new GitStatus(0, 0, 0, 0, 0, GitOp.None, "", "", "", 0, 0, true, [], [], [], [], [], []);

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
        GitOp operation = GitOp.None,
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
            operation,
            mergeMessage,
            mergeHeadCommit == "" ? "" : Sha(mergeHeadCommit),
            "",
            0,
            0,
            true,
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

    // The per repo user choices (branch colors and branch order) the view repo and the branch
    // colors are based on. In-memory, so a test can set them without touching disk.
    public FakeRepoConfig Config { get; } = new FakeRepoConfig();

    // The augmented repo the server layer works with: all commits and branches with the inferred
    // branch structure, plus the virtual uncommitted commit if the status has changes. No branch
    // is in view yet, that is what ViewRepoAsync adds.
    public async Task<Repo> AugmentedRepoAsync()
    {
        var repo = new WorkRepoConverter().ToRepo(await AugmentAsync());

        // The uncommitted commit is added by the augmented service, i.e. after the converter
        var result = await NewAugmentedService().UpdateRepoStatusAsync(repo);
        if (!Try(out var repoWithUncommitted, out var e, result))
            throw new InvalidOperationException($"Failed to add the uncommitted commit: {e}");

        return repoWithUncommitted;
    }

    // The view repo the UI renders, i.e. only the branches the user chose to show (with their
    // ancestors and related branches) and the commits on them. With no branches given, the
    // current branch is shown, and the main branch is always included.
    public async Task<Repo> ViewRepoAsync(params string[] showBranches) =>
        NewViewRepoCreater().GetViewRepoAsync(await AugmentedRepoAsync(), showBranches);

    // The view repo for one of the show-all modes, e.g. all active branches
    public async Task<Repo> ViewRepoAsync(ShowBranches show, int count = 1, params string[] showBranches) =>
        NewViewRepoCreater().GetViewRepoAsync(await AugmentedRepoAsync(), showBranches, show, count);

    // The view repo the filter dialog renders, i.e. only the commits matching the filter, on the
    // branches those commits are on. The default count is what FilterDlg passes.
    public async Task<Repo> FilteredViewRepoAsync(string filter, int maxCount = 5000) =>
        NewViewRepoCreater().GetFilteredViewRepoAsync(await AugmentedRepoAsync(), filter, maxCount);

    // The real server layer, with git, the file monitor and the config faked out. Needed for the
    // branch show/hide commands, which work on an already created view repo.
    public IServer NewServer() =>
        new ServerImpl(NewGit(), NewAugmentedService(), new ViewRepoConverter(), NewViewRepoCreater());

    // The augmenter with its real collaborators, none of which touch git, disk or the terminal.
    // The wiring mirrors the branch structure pipeline: the commit graph, the branch of every
    // commit, and then the hierarchy of the branches.
    public static IAugmenter NewAugmenter()
    {
        var branchNameService = new BranchNameService();
        return new Augmenter(
            new BranchStructureService(
                new CommitGraphService(branchNameService),
                new CommitBranchService(branchNameService, new CommitBranchRules(branchNameService)),
                new BranchHierarchyService()
            )
        );
    }

    FakeGit NewGit() => new FakeGit(status);

    ViewRepoCreater NewViewRepoCreater() => new ViewRepoCreater(new ViewRepoConverter(), Config);

    // The real augmented service, which only git, the file monitor and the shared meta data are
    // faked out of. The repo is augmented from the built GitRepo rather than read via git, so
    // this is used just for the steps the service itself adds.
    AugmentedService NewAugmentedService() => NewAugmentedService(NewGit(), new FakeMetaDataService(metaData));

    public static AugmentedService NewAugmentedService(IGit git, IMetaDataService metaDataService)
    {
        var fileMonitor = new FakeFileMonitor();
        return new AugmentedService(
            git,
            NewAugmenter(),
            new WorkRepoConverter(),
            fileMonitor,
            metaDataService,
            new BranchWriteService(git, fileMonitor, metaDataService)
        );
    }
}
