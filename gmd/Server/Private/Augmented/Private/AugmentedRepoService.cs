using gmd.Git;
using GitStatus = gmd.Git.Status;


namespace gmd.Server.Private.Augmented.Private;


// AugmentedRepoService returns augmented repos of git repo information, The augmentations 
// adds information not available in git directly, but can be inferred by parsing the 
// git information. 
// Examples of augmentation is which branch a commits belongs to and the hierarchical structure
// of branches. 
[SingleInstance]
class AugmentedRepoService : IAugmentedRepoService
{
    const int maxCommitCount = 30000; // Increase performance in case of very large repos

    private readonly IGit git;
    private readonly IAugmenter augmenter;
    private readonly IConverter converter;
    private readonly IFileMonitor fileMonitor;

    public AugmentedRepoService(
        IGit git,
        IAugmenter augmenter,
        IConverter converter,
        IFileMonitor fileMonitor)
    {
        this.git = git;
        this.augmenter = augmenter;
        this.converter = converter;
        this.fileMonitor = fileMonitor;

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
    public async Task<R<Repo>> UpdateStatusRepoAsync(Repo repo)
    {
        // Get latest git status
        if (!Try(out var gitStatus, out var e, await GetGitStatusAsync(repo.Path))) return e;

        // Returns the augmented repo with the new status
        return GetUpdatedAugmentedRepoStatus(repo, gitStatus);
    }


    // GetGitRepoAsync returns a fresh git repo info object with commits, branches, ...
    async Task<R<GitRepo>> GetGitRepoAsync(string path)
    {
        Timing t = Timing.Start;

        // Start some git commands in parallel to get commits, branches, status, ...
        var logTask = git.GetLogAsync(maxCommitCount, path);
        var branchesTask = git.GetBranchesAsync(path);
        var statusTask = git.GetStatusAsync(path);

        await Task.WhenAll(logTask, branchesTask, statusTask);

        if (!Try(out var log, out var e, logTask.Result)) return e;
        if (!Try(out var branches, out e, branchesTask.Result)) return e;
        if (!Try(out var status, out e, statusTask.Result)) return e;

        // Combine all git info into one git repo info object
        var gitRepo = new GitRepo(DateTime.UtcNow, path, log, branches, status);

        Log.Info($"{t} {gitRepo}");
        return gitRepo;
    }


    // GetGitStatusAsync returns a fresh git status
    async Task<R<GitStatus>> GetGitStatusAsync(string path)
    {
        Timing t = Timing.Start;

        if (!Try(out var gitStatus, out var e, await git.GetStatusAsync(path))) return e;

        Log.Info($"{t} S:{gitStatus}");
        return gitStatus;
    }


    // GetAugmentedRepoAsync returns an augmented git repo, and monitors working folder changes
    async Task<R<Repo>> GetAugmentedRepoAsync(GitRepo gitRepo)
    {
        fileMonitor.Monitor(gitRepo.Path);

        Timing t = Timing.Start;
        WorkRepo augRepo = await augmenter.GetAugRepoAsync(gitRepo, maxCommitCount);
        Threading.AssertMainThread();

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
