using gmd.Common;
using gmd.Git;
using GitStatus = gmd.Git.Status;

namespace gmd.Server.Private.Augmented.Private;

// AugmentedRepoService returns augmented repos of git repo information, The augmentations
// adds information not available in git directly, but can be inferred by parsing the
// git information.
// Examples of augmentation is which branch a commits belongs to and the hierarchical structure
// of branches.
[SingleInstance]
class AugmentedService : IAugmentedService
{
    const int maxCommitCount = 30000; // Increase performance in case of very large repos

    readonly IGit git;
    readonly IAugmenter augmenter;
    readonly IWorkRepoConverter converter;
    readonly IFileMonitor fileMonitor;
    readonly IMetaDataService metaDataService;
    readonly IBranchWriteService branchWriteService;
    readonly IRepoConfig repoConfig;

    internal AugmentedService(
        IGit git,
        IAugmenter augmenter,
        IWorkRepoConverter converter,
        IFileMonitor fileMonitor,
        IMetaDataService metaDataService,
        IBranchWriteService branchWriteService,
        IRepoConfig repoConfig
    )
    {
        this.repoConfig = repoConfig;
        this.git = git;
        this.augmenter = augmenter;
        this.converter = converter;
        this.fileMonitor = fileMonitor;
        this.metaDataService = metaDataService;
        this.branchWriteService = branchWriteService;
        fileMonitor.FileChanged += e => StatusChange?.Invoke(e);
        fileMonitor.RepoChanged += e => RepoChange?.Invoke(e);
    }

    public event Action<ChangeEvent>? RepoChange;
    public event Action<ChangeEvent>? StatusChange;

    // GetRepoAsync returns an augmented repo based on new git info like branches, commits, ...
    public async Task<Result<Repo>> GetRepoAsync(string path)
    {
        var rootPathResult = git.RootPath(path);
        if (rootPathResult is not string rootPath)
            return rootPathResult.Error;

        // Get a fresh new git repo (branches, commits, tags, status, ...)
        var gitRepoResult = await GetGitRepoAsync(rootPath);
        if (gitRepoResult is not GitRepo gitRepo)
            return gitRepoResult.Error;

        // Return an augmented repo
        return await GetAugmentedRepoAsync(gitRepo);
    }

    // GetRepoAsync returns the updated augmented repo with git status .
    public async Task<Result<Repo>> UpdateRepoStatusAsync(Repo repo)
    {
        // Get latest git status
        var statusResult = await GetGitStatusAsync(repo.Path);
        if (statusResult is not GitStatus gitStatus)
            return statusResult.Error;
        Log.Info($"Git status {gitStatus}");

        // Returns the augmented repo with the new status
        return GetUpdatedAugmentedRepoStatus(repo, gitStatus);
    }

    public async Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.CommitAllChangesAsync(message, isAmend, wd);
        }
    }

    // The worktrees read on their own: the list, and the status of each of the others, which a
    // freshly read repo does not have (its counts are unknown until this is called), so that a
    // status run in another worktree never delays showing this one. Only the worktrees change; the
    // branches keep the worktree paths they have, since a worktree appearing or going is a repo
    // change the file monitor reloads everything for. Which worktree is the current one does not
    // change between two reads of the same repo, so it is kept from the last one.
    public async Task<Result<Repo>> GetUpdatedWorktreesRepoAsync(Repo repo)
    {
        var worktreesResult = await git.GetWorktreesAsync(repo.Path);
        if (worktreesResult is not IReadOnlyList<Git.Worktree> gitWorktrees)
            return worktreesResult.Error;
        var changes = await GetWorktreeChangesAsync(gitWorktrees, repo.Path);

        var currentPath = repo.Worktrees.FirstOrDefault(w => w.IsCurrent)?.Path ?? repo.Path;
        var worktrees = gitWorktrees
            .Select(w =>
            {
                var isCurrent = Files.IsSamePath(w.Path, currentPath);
                var count =
                    isCurrent ? repo.Status.ChangesCount
                    : changes.TryGetValue(w.Path, out var n) ? n
                    : -1;
                return new Worktree(
                    w.Path,
                    w.Branch,
                    w.HeadId,
                    w.IsMain,
                    isCurrent,
                    w.IsDetached,
                    w.IsLocked,
                    w.LockReason,
                    w.IsPrunable,
                    w.PruneReason,
                    count
                );
            })
            .ToList();

        return repo with
        {
            Worktrees = worktrees,
        };
    }

    // The worktree writes pause the file monitor like the other writes do: adding one writes into
    // the common dir's 'worktrees/', which is watched, and that reload is made by the caller
    public async Task<Result> AddWorktreeAsync(
        string path,
        string branchName,
        bool isNewBranch,
        string startPoint,
        string wd
    )
    {
        using (fileMonitor.Pause())
        {
            return await git.AddWorktreeAsync(path, branchName, isNewBranch, startPoint, wd);
        }
    }

    public async Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.RemoveWorktreeAsync(path, isForce, wd);
        }
    }

    public async Task<Result> PruneWorktreesAsync(string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.PruneWorktreesAsync(wd);
        }
    }

    public async Task<Result> FetchAsync(string path)
    {
        // using (Timing.Start("Fetched"))
        {
            // pull meta data, but ignore error, if error is key not exist, it can be ignored,
            // if error is remote error, the following fetch will handle that
            // Start both tasks in parallel and await later
            var metaDataTask = metaDataService.FetchMetaDataAsync(path);
            var fetchTask = git.FetchAsync(path);

            await Task.WhenAll(metaDataTask, fetchTask);

            // Return the result of the fetch task (ignoring the metaData result)
            return fetchTask.Result;
        }
    }

    public Task<Result> FetchMetaDataAsync(string path) => metaDataService.FetchMetaDataAsync(path);

    // The branch write operations, which need the augmented repo to work out what git to run
    public Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd) =>
        branchWriteService.CreateBranchAsync(repo, newBranchName, isCheckout, wd);

    public Task<Result> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout,
        string wd
    ) => branchWriteService.CreateBranchFromBranchAsync(repo, newBranchName, sourceBranch, isCheckout, wd);

    public Task<Result> CreateBranchFromCommitAsync(
        Repo repo,
        string newBranchName,
        string sha,
        bool isCheckout,
        string wd
    ) => branchWriteService.CreateBranchFromCommitAsync(repo, newBranchName, sha, isCheckout, wd);

    public Task<Result> RenameBranchAsync(string oldName, string newName, string wd) =>
        branchWriteService.RenameBranchAsync(oldName, newName, wd);

    public Task<Result> SwitchToAsync(Repo repo, string branchName) =>
        branchWriteService.SwitchToAsync(repo, branchName);

    public Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name) =>
        branchWriteService.MergeBranchAsync(repo, name);

    public Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName) =>
        branchWriteService.MergeToBranchAsync(repo, targetName);

    public Task<Result> RebaseBranchAsync(Repo repo, string name) => branchWriteService.RebaseBranchAsync(repo, name);

    // GetGitRepoAsync returns a fresh git repo info object with commits, branches, ...
    async Task<Result<GitRepo>> GetGitRepoAsync(string path)
    {
        Timing t = Timing.Start();

        var timeStamp = DateTime.UtcNow;
        fileMonitor.SetReadRepoTime(timeStamp);

        // Start some git commands in parallel to get commits, branches, status, ...
        var logTask = git.GetLogAsync(maxCommitCount, path);
        var branchesTask = git.GetBranchesAsync(path);
        var tagsTask = git.GetTagsAsync(path);
        var statusTask = git.GetStatusAsync(path);
        var metaDataTask = metaDataService.GetMetaDataAsync(path);
        var stashesTask = git.GetStashesAsync(path);
        var worktreesTask = git.GetWorktreesAsync(path);
        var reflogTask = git.GetReflogAsync(path);
        await Task.WhenAll(
            logTask,
            branchesTask,
            tagsTask,
            statusTask,
            metaDataTask,
            stashesTask,
            worktreesTask,
            reflogTask
        );

        // Check all tasks for errors
        if (logTask.Result is not IReadOnlyList<Git.Commit> log)
            return logTask.Result.Error;
        if (branchesTask.Result is not IReadOnlyList<Git.Branch> branches)
            return branchesTask.Result.Error;
        if (tagsTask.Result is not IReadOnlyList<Git.Tag> tags)
            return tagsTask.Result.Error;
        if (statusTask.Result is not GitStatus status)
            return statusTask.Result.Error;
        if (metaDataTask.Result is not MetaData metaData)
            return metaDataTask.Result.Error;
        if (stashesTask.Result is not IReadOnlyList<Git.Stash> stashes)
            return stashesTask.Result.Error;

        // The worktrees are extra: a git too old to list them must not keep the repo from opening.
        // Only the list is read here. The changes of the other worktrees are a status run in each
        // of their folders, and that is read after the repo is shown (GetUpdatedWorktreesRepoAsync)
        // rather than before, so a cold index in a large worktree cannot delay showing this one.
        if (worktreesTask.Result is not IReadOnlyList<Git.Worktree> worktrees)
        {
            Log.Warn($"Failed to list worktrees, {worktreesTask.Result.Error}");
            worktrees = [];
        }

        // The reflog is extra as well: it only makes the branch inference surer, and a repo can have none
        if (reflogTask.Result is not IReadOnlyList<ReflogEntry> reflog)
        {
            Log.Warn($"Failed to read the reflog, {reflogTask.Result.Error}");
            reflog = [];
        }

        var isTruncated = log.Count == maxCommitCount;
        if (log.Count == 0)
            return EmptyGitRepo(path, tags, status, metaData);

        // Combine all git info into one git repo info object
        var gitRepo = new GitRepo(
            timeStamp,
            path,
            log,
            branches,
            tags,
            status,
            metaData,
            stashes,
            isTruncated,
            worktrees,
            reflog: reflog,
            integrationNames: repoConfig.Get(path).IntegrationBranches
        );
        Log.Info($"GitRepo {t} {gitRepo}");

        return gitRepo;
    }

    // The number of uncommitted changes in each of the other worktrees, read in parallel. Only
    // the ones that can be read: a worktree whose folder is gone has no status, and neither has
    // a bare one. A status that fails is left out, which the UI shows as unknown. Read without
    // locks: someone else is working in those folders, and a plain status would hold their index
    // lock while it rewrote their index, long enough for a commit there to fail on it.
    async Task<IReadOnlyDictionary<string, int>> GetWorktreeChangesAsync(
        IReadOnlyList<Git.Worktree> worktrees,
        string path
    )
    {
        var others = worktrees
            .Where(w => !w.IsPrunable && !w.IsBare && !Files.IsSamePath(w.Path, path) && Directory.Exists(w.Path))
            .ToList();
        var tasks = others.Select(w => git.GetStatusWithoutLocksAsync(w.Path)).ToList();
        await Task.WhenAll(tasks);

        var changes = new Dictionary<string, int>();
        others.ForEach(
            (w, i) =>
            {
                if (tasks[i].Result is GitStatus status)
                    changes[w.Path] =
                        status.Modified + status.Added + status.Deleted + status.Conflicted + status.Renamed;
                else
                    Log.Warn($"Failed to get status of worktree {w.Path}, {tasks[i].Result.Error}");
            }
        );
        return changes;
    }

    // The folders of the other worktrees, which the file monitor must not take changes in as
    // changes of this one — a worktree nested inside the working folder would otherwise refresh
    // the status here for every file written there
    static IReadOnlyList<string> OtherWorktreeFolders(GitRepo gitRepo) =>
        gitRepo.Worktrees.Where(w => !Files.IsSamePath(w.Path, gitRepo.Path)).Select(w => w.Path).ToList();

    public async Task<Result> SquashCommits(Repo repo, string id1, string id2, string message)
    {
        using (fileMonitor.Pause())
        {
            var c1 = repo.CommitById[id1];
            var c2 = repo.CommitById[id2];
            if (!c2.ParentIds.Any())
                return new Error("Last commit does not have a parent");
            if (c1.BranchName != c2.BranchName)
                return new Error("Commits are not on the same branch");
            var branch = repo.BranchByName[c1.BranchName];

            // Both flags: IsLocalCurrent is only ever set on a *remote* branch whose local branch
            // is current, so asking for it alone refused every commit that had not been pushed yet.
            // The same check is made before the dialog is shown, in CommitCommands.SquashCommits.
            if (!branch.IsCurrent && !branch.IsLocalCurrent)
                return new Error("Commits not on current branch");

            // Create a backup branch (in case of errors)
            var tmpName = $"squash-backup-{Guid.NewGuid().ToString()[..6]}";
            if (await git.CreateBranchAsync(tmpName, false, repo.Path) is Error backupError)
                return new Error("Failed to create backup branch", backupError);

            // Remember commits before squash to be cherry picked back
            var preCommits = new List<Commit>();
            var c = repo.CommitById[branch.TipId];
            while (c.Id != c1.Id)
            {
                preCommits.Add(c);
                if (!c.ParentIds.Any())
                    break;
                c = repo.CommitById[c.ParentIds[0]];
            }

            // Remove all prefix commits on current branch until the first commit to squash
            if (preCommits.Any())
            {
                if (await git.ResetHardUntilCommitAsync(id1, repo.Path) is Error resetError)
                    return new Error("Failed to prepare for squash", resetError);
            }

            // Squash commits and commit
            if (await git.UncommitUntilCommitAsync(c2.ParentIds[0], repo.Path) is Error uncommitError)
                return new Error("Failed to squash commits", uncommitError);
            if (await git.CommitAllChangesAsync(message, false, repo.Path) is Error commitError)
                return new Error("Failed to commit squashed commits", commitError);

            // Cherry pick prefix commits back to current branch
            foreach (var commit in preCommits.AsEnumerable().Reverse())
            {
                if (await git.CherryPickAsync(commit.Id, repo.Path) is Error pickError)
                    return new Error($"Failed to cherry pick {commit.Sid}", pickError);
                if (await git.CommitAllChangesAsync(commit.Message, false, repo.Path) is Error pickCommitError)
                    return new Error($"Failed to commit cherry pick {commit.Sid}", pickCommitError);
            }

            // Remove temp backup branch
            if (await git.DeleteLocalBranchAsync(tmpName, true, repo.Path) is Error deleteError)
                return new Error("Failed to delete backup branch", deleteError);
        }

        return Result.Ok;
    }

    public async Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setNiceName)
    {
        Log.Info($"Set {commitId.Sid()} to {setNiceName} ...");

        using (fileMonitor.Pause())
        {
            return await metaDataService.UpdateMetaDataAsync(
                repo.Path,
                m => m.SetCommitBranch(commitId.Sid(), setNiceName)
            );
        }
    }

    public async Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName)
    {
        var branch = repo.BranchByName[branchName];
        var ambiguousTip = branch.AmbiguousTipId;
        Log.Info($"Resolve {ambiguousTip.Sid()} of {branchName} to {setHumanName} ...");

        using (fileMonitor.Pause())
        {
            return await metaDataService.UpdateMetaDataAsync(
                repo.Path,
                m => m.SetCommitBranch(ambiguousTip.Sid(), setHumanName)
            );
        }
    }

    public async Task<Result> UnresolveAmbiguityAsync(Repo repo, string commitId)
    {
        using (fileMonitor.Pause())
        {
            return await metaDataService.UpdateMetaDataAsync(repo.Path, m => m.RemoveCommitBranch(commitId.Sid()));
        }
    }

    public Task<Result> PushMetaDataAsync(string wd) => metaDataService.PushMetaDataAsync(wd);

    public async Task<Result> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (await git.AddTagAsync(name, commitId, wd) is Error e)
                return e;
            if (!hasRemoteBranch)
                return Result.Ok;
            return await git.PushTagAsync(name, wd);
        }
    }

    public async Task<Result> AddAnnotatedTagAsync(
        string name,
        string message,
        string commitId,
        bool hasRemoteBranch,
        string wd
    )
    {
        using (fileMonitor.Pause())
        {
            if (await git.AddAnnotatedTagAsync(name, message, commitId, wd) is Error e)
                return e;
            if (!hasRemoteBranch)
                return Result.Ok;
            return await git.PushTagAsync(name, wd);
        }
    }

    public async Task<Result> RemoveTagAsync(string name, bool hasRemoteBranch, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (await git.RemoveTagAsync(name, wd) is Error e)
                return e;
            if (!hasRemoteBranch)
                return Result.Ok;
            return await git.DeleteRemoteTagAsync(name, wd);
        }
    }

    // GetGitStatusAsync returns a fresh git status
    async Task<Result<GitStatus>> GetGitStatusAsync(string path)
    {
        fileMonitor.SetReadStatusTime(DateTime.UtcNow);
        return await git.GetStatusAsync(path);
    }

    // GetAugmentedRepoAsync returns an augmented git repo, and monitors working folder changes
    async Task<Result<Repo>> GetAugmentedRepoAsync(GitRepo gitRepo)
    {
        fileMonitor.Monitor(gitRepo.Path, OtherWorktreeFolders(gitRepo));

        Timing t = Timing.Start();
        WorkRepo augRepo = await augmenter.GetAugRepoAsync(gitRepo);
        if (augRepo.WitnessedToKeep.Count > 0)
        { // Kept after the repo is shown, since nothing waits for it
            KeepWitnessedAsync(gitRepo.Path, augRepo.WitnessedToKeep).RunInBackground();
        }

        var repo = converter.ToRepo(augRepo);
        repo = Uncommitted.Adjust(repo);
        Log.Info($"Augmented {t} {repo}");
        return repo;
    }

    // What the reflog witnessed and decided is kept in the metadata, since the reflog expires. The
    // file monitor is not paused for it, since it takes no metadata write for a change of the repo.
    Task<Result> KeepWitnessedAsync(string path, IReadOnlyList<WitnessedBranch> witnessed) =>
        metaDataService.AddWitnessedAsync(path, witnessed);

    // GetUpdatedAugmentedRepoStatus an updated augmented repo with new status
    Repo GetUpdatedAugmentedRepoStatus(Repo repo, GitStatus gitStatus)
    {
        Timing t = Timing.Start();
        var status = converter.ToStatus(gitStatus);
        repo = repo with { Status = status, ViewBranches = new List<Branch>(), ViewCommits = new List<Commit>() };
        repo = Uncommitted.Adjust(repo);
        Log.Info($"Augmented {t} {repo}");
        return repo;
    }

    Result<GitRepo> EmptyGitRepo(string path, IReadOnlyList<Git.Tag> tags, GitStatus status, MetaData metaData)
    {
        Timing t = Timing.Start();
        var id = Repo.EmptyRepoCommitId;
        var msg = "<... empty repo ...>";
        var branchName = "main";
        var commit = new Git.Commit(id, id.Sid(), [], msg, msg, "", DateTime.UtcNow, DateTime.UtcNow);
        var branch = new Git.Branch(branchName, id, true, false, "", false, 0, 0);
        var commits = new List<Git.Commit>() { commit };
        var branches = new List<Git.Branch>() { branch };
        var stashes = new List<Git.Stash>();

        var gitRepo = new GitRepo(DateTime.UtcNow, path, commits, branches, tags, status, metaData, stashes, false);
        Log.Info($"GitRepo {t} {gitRepo}");
        return gitRepo;
    }
}
