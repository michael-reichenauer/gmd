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

    internal AugmentedService(
        IGit git,
        IAugmenter augmenter,
        IWorkRepoConverter converter,
        IFileMonitor fileMonitor,
        IMetaDataService metaDataService,
        IBranchWriteService branchWriteService
    )
    {
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
    public async Task<R<Repo>> GetRepoAsync(string path)
    {
        if (!Try(out var rootPath, out var e, git.RootPath(path)))
            return e;

        // Get a fresh new git repo (branches, commits, tags, status, ...)
        if (!Try(out var gitRepo, out e, await GetGitRepoAsync(rootPath)))
            return e;

        // Return an augmented repo
        return await GetAugmentedRepoAsync(gitRepo);
    }

    // GetRepoAsync returns the updated augmented repo with git status .
    public async Task<R<Repo>> UpdateRepoStatusAsync(Repo repo)
    {
        // Get latest git status
        if (!Try(out var gitStatus, out var e, await GetGitStatusAsync(repo.Path)))
            return e;
        Log.Info($"Git status {gitStatus}");

        // Returns the augmented repo with the new status
        return GetUpdatedAugmentedRepoStatus(repo, gitStatus);
    }

    public async Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.CommitAllChangesAsync(message, isAmend, wd);
        }
    }

    public async Task<R> FetchAsync(string path)
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

    public Task<R> FetchMetaDataAsync(string path) => metaDataService.FetchMetaDataAsync(path);

    // The branch write operations, which need the augmented repo to work out what git to run
    public Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd) =>
        branchWriteService.CreateBranchAsync(repo, newBranchName, isCheckout, wd);

    public Task<R> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout,
        string wd
    ) => branchWriteService.CreateBranchFromBranchAsync(repo, newBranchName, sourceBranch, isCheckout, wd);

    public Task<R> CreateBranchFromCommitAsync(
        Repo repo,
        string newBranchName,
        string sha,
        bool isCheckout,
        string wd
    ) => branchWriteService.CreateBranchFromCommitAsync(repo, newBranchName, sha, isCheckout, wd);

    public Task<R> RenameBranchAsync(string oldName, string newName, string wd) =>
        branchWriteService.RenameBranchAsync(oldName, newName, wd);

    public Task<R> SwitchToAsync(Repo repo, string branchName) => branchWriteService.SwitchToAsync(repo, branchName);

    public Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name) =>
        branchWriteService.MergeBranchAsync(repo, name);

    public Task<R<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName) =>
        branchWriteService.MergeToBranchAsync(repo, targetName);

    public Task<R> RebaseBranchAsync(Repo repo, string name) => branchWriteService.RebaseBranchAsync(repo, name);

    // GetGitRepoAsync returns a fresh git repo info object with commits, branches, ...
    async Task<R<GitRepo>> GetGitRepoAsync(string path)
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
        await Task.WhenAll(logTask, branchesTask, tagsTask, statusTask, metaDataTask, stashesTask);

        // Check all tasks for errors
        if (!Try(out var log, out var e, logTask.Result))
            return e;
        if (!Try(out var branches, out e, branchesTask.Result))
            return e;
        if (!Try(out var tags, out e, tagsTask.Result))
            return e;
        if (!Try(out var status, out e, statusTask.Result))
            return e;
        if (!Try(out var metaData, out e, metaDataTask.Result))
            return e;
        if (!Try(out var stashes, out e, stashesTask.Result))
            return e;

        var isTruncated = log.Count == maxCommitCount;
        if (log.Count == 0)
            return EmptyGitRepo(path, tags, status, metaData);

        // Combine all git info into one git repo info object
        var gitRepo = new GitRepo(timeStamp, path, log, branches, tags, status, metaData, stashes, isTruncated);
        Log.Info($"GitRepo {t} {gitRepo}");

        return gitRepo;
    }

    public async Task<R> SquashCommits(Repo repo, string id1, string id2, string message)
    {
        using (fileMonitor.Pause())
        {
            var c1 = repo.CommitById[id1];
            var c2 = repo.CommitById[id2];
            if (!c2.ParentIds.Any())
                return R.Error("Last commit does not have a parent");
            if (c1.BranchName != c2.BranchName)
                return R.Error("Commits are not on the same branch");
            var branch = repo.BranchByName[c1.BranchName];

            // Both flags: IsLocalCurrent is only ever set on a *remote* branch whose local branch
            // is current, so asking for it alone refused every commit that had not been pushed yet.
            // The same check is made before the dialog is shown, in CommitCommands.SquashCommits.
            if (!branch.IsCurrent && !branch.IsLocalCurrent)
                return R.Error("Commits not on current branch");

            // Create a backup branch (in case of errors)
            var tmpName = $"squash-backup-{Guid.NewGuid().ToString()[..6]}";
            if (!Try(out var e, await git.CreateBranchAsync(tmpName, false, repo.Path)))
                return R.Error("Failed to create backup branch", e);

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
                if (!Try(out e, await git.ResetHardUntilCommitAsync(id1, repo.Path)))
                    return R.Error("Failed to prepare for squash", e);
            }

            // Squash commits and commit
            if (!Try(out e, await git.UncommitUntilCommitAsync(c2.ParentIds[0], repo.Path)))
                return R.Error("Failed to squash commits", e);
            if (!Try(out e, await git.CommitAllChangesAsync(message, false, repo.Path)))
                return R.Error("Failed to commit squashed commits", e);

            // Cherry pick prefix commits back to current branch
            foreach (var commit in preCommits.AsEnumerable().Reverse())
            {
                if (!Try(out e, await git.CherryPickAsync(commit.Id, repo.Path)))
                    return R.Error($"Failed to cherry pick {commit.Sid}", e);
                if (!Try(out e, await git.CommitAllChangesAsync(commit.Message, false, repo.Path)))
                    return R.Error($"Failed to commit cherry pick {commit.Sid}", e);
            }

            // Remove temp backup branch
            if (!Try(out e, await git.DeleteLocalBranchAsync(tmpName, true, repo.Path)))
                return R.Error("Failed to delete backup branch", e);
        }

        return R.Ok;
    }

    public async Task<R> SetBranchManuallyAsync(Repo repo, string commitId, string setNiceName)
    {
        Log.Info($"Set {commitId.Sid()} to {setNiceName} ...");

        using (fileMonitor.Pause())
        {
            // Get the latest meta data
            if (!Try(out var metaData, out var e, await metaDataService.GetMetaDataAsync(repo.Path)))
                return e;

            metaData.SetCommitBranch(commitId.Sid(), setNiceName);
            return await metaDataService.SetMetaDataAsync(repo.Path, metaData);
        }
    }

    public async Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName)
    {
        var branch = repo.BranchByName[branchName];
        var ambiguousTip = branch.AmbiguousTipId;
        Log.Info($"Resolve {ambiguousTip.Sid()} of {branchName} to {setHumanName} ...");

        using (fileMonitor.Pause())
        {
            // Get the latest meta data
            if (!Try(out var metaData, out var e, await metaDataService.GetMetaDataAsync(repo.Path)))
                return e;

            metaData.SetCommitBranch(ambiguousTip.Sid(), setHumanName);
            return await metaDataService.SetMetaDataAsync(repo.Path, metaData);
        }
    }

    public async Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId)
    {
        using (fileMonitor.Pause())
        {
            // Get the latest meta data
            if (!Try(out var metaData, out var e, await metaDataService.GetMetaDataAsync(repo.Path)))
                return e;

            metaData.RemoveCommitBranch(commitId.Sid());

            return await metaDataService.SetMetaDataAsync(repo.Path, metaData);
        }
    }

    public Task<R> PushMetaDataAsync(string wd) => metaDataService.PushMetaDataAsync(wd);

    public async Task<R> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.AddTagAsync(name, commitId, wd)))
                return e;
            if (!hasRemoteBranch)
                return R.Ok;
            return await git.PushTagAsync(name, wd);
        }
    }

    public async Task<R> AddAnnotatedTagAsync(
        string name,
        string message,
        string commitId,
        bool hasRemoteBranch,
        string wd
    )
    {
        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.AddAnnotatedTagAsync(name, message, commitId, wd)))
                return e;
            if (!hasRemoteBranch)
                return R.Ok;
            return await git.PushTagAsync(name, wd);
        }
    }

    public async Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.RemoveTagAsync(name, wd)))
                return e;
            if (!hasRemoteBranch)
                return R.Ok;
            return await git.DeleteRemoteTagAsync(name, wd);
        }
    }

    // GetGitStatusAsync returns a fresh git status
    async Task<R<GitStatus>> GetGitStatusAsync(string path)
    {
        fileMonitor.SetReadStatusTime(DateTime.UtcNow);
        if (!Try(out var gitStatus, out var e, await git.GetStatusAsync(path)))
            return e;
        return gitStatus;
    }

    // GetAugmentedRepoAsync returns an augmented git repo, and monitors working folder changes
    async Task<R<Repo>> GetAugmentedRepoAsync(GitRepo gitRepo)
    {
        fileMonitor.Monitor(gitRepo.Path);

        Timing t = Timing.Start();
        WorkRepo augRepo = await augmenter.GetAugRepoAsync(gitRepo);

        var repo = converter.ToRepo(augRepo);
        repo = Uncommitted.Adjust(repo);
        Log.Info($"Augmented {t} {repo}");
        return repo;
    }

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

    R<GitRepo> EmptyGitRepo(string path, IReadOnlyList<Git.Tag> tags, GitStatus status, MetaData metaData)
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
