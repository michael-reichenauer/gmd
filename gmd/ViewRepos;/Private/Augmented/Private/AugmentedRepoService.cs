
using gmd.Utils.Git;

namespace gmd.ViewRepos.Private.Augmented.Private;

[SingleInstance]
class AugmentedRepoService : IAugmentedRepoService
{
    const int maxCommitCount = 30000;

    private readonly IGitService gitService;
    private readonly IAugmenter augmenter;
    private readonly IConverter converter;
    private readonly IFileMonitor fileMonitor;

    public AugmentedRepoService(
        IGitService gitService,
        IAugmenter augmenter,
        IConverter converter,
        IFileMonitor fileMonitor)
    {
        this.gitService = gitService;
        this.augmenter = augmenter;
        this.converter = converter;
        this.fileMonitor = fileMonitor;

        fileMonitor.FileChanged += (s, e) => OnStatusChange(e);
        fileMonitor.RepoChanged += (s, e) => OnRepoChange(e);
    }

    public event EventHandler<ChangeEventArgs>? RepoChange;
    public event EventHandler<ChangeEventArgs>? StatusChange;

    public async Task<R<Repo>> GetRepoAsync(string path)
    {
        var gitRepo = await GetGitRepoAsync(path);
        if (gitRepo.IsError)
        {
            return gitRepo.Error;
        }

        return await GetAugmentedRepo(gitRepo.Value);
    }

    public async Task<R<Repo>> UpdateStatusRepoAsync(Repo augRepo)
    {
        var git = gitService.Git(augRepo.Path);

        var gitStatus = await git.GetStatusAsync();
        if (gitStatus.IsError)
        {
            return gitStatus.Error;
        }

        var s = gitStatus.Value;
        Status status = new Status(s.Modified, s.Added, s.Deleted, s.Conflicted,
          s.IsMerging, s.MergeMessage, s.AddedFiles, s.ConflictsFiles);

        return augRepo with { Status = status };
    }

    async Task<R<GitRepo>> GetGitRepoAsync(string path)
    {
        Timing t = Timing.Start();
        var git = gitService.Git(path);

        // Start some git commands in parallel
        var logTask = git.GetLogAsync(maxCommitCount);
        var branchesTask = git.GetBranchesAsync();
        var statusTask = git.GetStatusAsync();

        await Task.WhenAll(logTask, branchesTask, statusTask);

        if (logTask.Result.IsError)
        {
            return logTask.Result.Error;
        }
        else if (branchesTask.Result.IsError)
        {
            return branchesTask.Result.Error;
        }
        else if (statusTask.Result.IsError)
        {
            return statusTask.Result.Error;
        }

        var repo = new GitRepo(
            DateTime.UtcNow,
            git.Path,
            logTask.Result.Value,
            branchesTask.Result.Value,
            statusTask.Result.Value);
        Log.Info($"{t} B:{repo.Branches.Count}, C:{repo.Commits.Count}, S:{repo.Status}");
        return repo;
    }

    protected virtual void OnRepoChange(ChangeEventArgs e)
    {
        var handler = RepoChange;
        handler?.Invoke(this, e);
    }

    protected virtual void OnStatusChange(ChangeEventArgs e)
    {
        var handler = StatusChange;
        handler?.Invoke(this, e);
    }

    async Task<R<Repo>> GetAugmentedRepo(GitRepo gitRepo)
    {
        fileMonitor.Monitor(gitRepo.Path);

        Timing t = Timing.Start();
        WorkRepo augRepo = await this.augmenter.GetAugRepoAsync(gitRepo, maxCommitCount);

        var repo = this.converter.ToRepo(augRepo);
        Log.Info($"{t} B:{repo.Branches.Count}, C:{repo.Commits.Count}, S:{repo.Status}");
        return repo;
    }
}
