
using gmd.Utils.Git;

namespace gmd.ViewRepos.Private.Augmented.Private;

class AugmentedRepoService : IAugmentedRepoService
{
    const int maxCommitCount = 30000;

    private readonly IGitService gitService;
    private readonly IAugmenter augmenter;
    private readonly IConverter converter;

    public AugmentedRepoService(
        IGitService gitService,
        IAugmenter augmenter,
        IConverter converter)
    {
        this.gitService = gitService;
        this.augmenter = augmenter;
        this.converter = converter;
    }

    public event EventHandler? RepoChange;

    public async Task<R<Repo>> GetRepoAsync(string path)
    {
        var gitRepo = await GetGitRepoAsync(path);
        if (gitRepo.IsError)
        {
            return gitRepo.Error;
        }

        return await GetAugmentedRepo(gitRepo.Value);
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


    protected virtual void OnRepoChange(EventArgs e)
    {
        EventHandler? handler = RepoChange;
        handler?.Invoke(this, e);
    }

    async Task<R<Repo>> GetAugmentedRepo(GitRepo gitRepo)
    {
        Timing t = Timing.Start();
        WorkRepo augRepo = await this.augmenter.GetAugRepoAsync(gitRepo, maxCommitCount);

        var repo = this.converter.ToRepo(augRepo);
        Log.Info($"{t} B:{repo.Branches.Count}, C:{repo.Commits.Count}, S:{repo.Status}");
        return repo;
    }
}
