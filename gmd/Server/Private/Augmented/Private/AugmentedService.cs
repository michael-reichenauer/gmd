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
    readonly IConverter converter;
    readonly IFileMonitor fileMonitor;
    readonly IMetaDataService metaDataService;

    internal AugmentedService(
        IGit git,
        IAugmenter augmenter,
        IConverter converter,
        IFileMonitor fileMonitor,
        IMetaDataService metaDataService)
    {
        this.git = git;
        this.augmenter = augmenter;
        this.converter = converter;
        this.fileMonitor = fileMonitor;
        this.metaDataService = metaDataService;
        fileMonitor.FileChanged += e => StatusChange?.Invoke(e);
        fileMonitor.RepoChanged += e => RepoChange?.Invoke(e);
    }

    public event Action<ChangeEvent>? RepoChange;
    public event Action<ChangeEvent>? StatusChange;


    // GetRepoAsync returns an augmented repo based on new git info like branches, commits, ...
    public async Task<R<Repo>> GetRepoAsync(string path)
    {
        if (!Try(out var rootPath, out var e, git.RootPath(path))) return e;

        // Get a fresh new git repo (branches, commits, tags, status, ...)
        if (!Try(out var gitRepo, out e, await GetGitRepoAsync(rootPath))) return e;

        // Return an augmented repo
        return await GetAugmentedRepoAsync(gitRepo);
    }


    // GetRepoAsync returns the updated augmented repo with git status .
    public async Task<R<Repo>> UpdateRepoStatusAsync(Repo repo)
    {
        // Get latest git status
        if (!Try(out var gitStatus, out var e, await GetGitStatusAsync(repo.Path))) return e;

        // Returns the augmented repo with the new status
        return GetUpdatedAugmentedRepoStatus(repo, gitStatus);
    }

    public async Task<R> FetchAsync(string path)
    {
        using (Timing.Start("Fetched"))
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


    // GetGitRepoAsync returns a fresh git repo info object with commits, branches, ...
    async Task<R<GitRepo>> GetGitRepoAsync(string path)
    {
        Timing t = Timing.Start();

        // Start some git commands in parallel to get commits, branches, status, ...
        var logTask = git.GetLogAsync(maxCommitCount, path);
        var branchesTask = git.GetBranchesAsync(path);
        var tagsTask = git.GetTagsAsync(path);
        var statusTask = git.GetStatusAsync(path);
        var metaDataTask = metaDataService.GetMetaDataAsync(path);

        await Task.WhenAll(logTask, branchesTask, statusTask, metaDataTask);

        if (!Try(out var log, out var e, logTask.Result)) return e;
        if (!Try(out var branches, out e, branchesTask.Result)) return e;
        if (!Try(out var tags, out e, tagsTask.Result)) return e;
        if (!Try(out var status, out e, statusTask.Result)) return e;
        if (!Try(out var metaData, out e, metaDataTask.Result)) return e;

        // Combine all git info into one git repo info object
        var gitRepo = new GitRepo(DateTime.UtcNow, path, log, branches, tags, status, metaData);

        Log.Info($"{t} {gitRepo}");
        return gitRepo;
    }


    public async Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd)
    {
        Log.Info($"Create branch {newBranchName} ...");
        var branch = repo.Branches.FirstOrDefault(b => b.IsCurrent);
        Commit? commit = null;
        if (branch != null)
        {
            commit = repo.CommitById[branch.TipId];
        }

        if (!Try(out var e, await git.CreateBranchAsync(newBranchName, isCheckout, wd))) return e;

        if (commit == null || branch == null)
        {
            return R.Ok;
        }

        // Get the latest meta data
        if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd))) return e;

        metaData.SetBranched(commit.Sid, branch.DisplayName);
        return await metaDataService.SetMetaDataAsync(wd, metaData);
    }

    public async Task<R> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd)
    {
        Log.Info($"Create branch {newBranchName} from {sha} ...");
        if (!Try(out var e, await git.CreateBranchFromCommitAsync(newBranchName, sha, isCheckout, wd))) return e;

        Commit commit = repo.CommitById[sha];
        var branch = repo.BranchByName[commit.BranchName];

        // Get the latest meta data
        if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd))) return e;

        metaData.SetBranched(commit.Sid, branch.DisplayName);
        return await metaDataService.SetMetaDataAsync(wd, metaData);
    }


    public async Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setDisplayName)
    {
        var branch = repo.BranchByName[branchName];
        var ambiguousTip = branch.AmbiguousTipId;
        Log.Info($"Resolve {ambiguousTip.Substring(0, 6)} of {branchName} to {setDisplayName} ...");

        // Get the latest meta data
        if (!Try(out var metaData, out var e, await metaDataService.GetMetaDataAsync(repo.Path))) return e;

        metaData.SetCommitBranch(ambiguousTip.Substring(0, 6), setDisplayName);
        return await metaDataService.SetMetaDataAsync(repo.Path, metaData);
    }


    public async Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId)
    {
        // Get the latest meta data
        if (!Try(out var metaData, out var e, await metaDataService.GetMetaDataAsync(repo.Path))) return e;

        metaData.Remove(commitId.Substring(0, 6));

        return await metaDataService.SetMetaDataAsync(repo.Path, metaData);
    }


    public Task<R> PushMetaDataAsync(string wd) =>
        metaDataService.PushMetaDataAsync(wd);

    // GetGitStatusAsync returns a fresh git status
    async Task<R<GitStatus>> GetGitStatusAsync(string path)
    {
        Timing t = Timing.Start();

        if (!Try(out var gitStatus, out var e, await git.GetStatusAsync(path))) return e;

        Log.Info($"{t} S:{gitStatus}");
        return gitStatus;
    }


    // GetAugmentedRepoAsync returns an augmented git repo, and monitors working folder changes
    async Task<R<Repo>> GetAugmentedRepoAsync(GitRepo gitRepo)
    {
        fileMonitor.Monitor(gitRepo.Path);

        Timing t = Timing.Start();
        WorkRepo augRepo = await augmenter.GetAugRepoAsync(gitRepo, maxCommitCount);
        Threading.AssertIsMainThread();

        var repo = converter.ToRepo(augRepo);
        Log.Info($"{t} {repo}");
        return repo;
    }


    // GetUpdatedAugmentedRepoStatus an updated augmented repo with new status
    Repo GetUpdatedAugmentedRepoStatus(Repo repo, GitStatus gitStatus)
    {
        var status = converter.ToStatus(gitStatus);
        return repo with { Status = status };
    }
}
