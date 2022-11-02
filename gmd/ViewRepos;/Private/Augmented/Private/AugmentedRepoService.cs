
using gmd.Utils.Git;

namespace gmd.ViewRepos.Private.Augmented.Private;

class AugmentedRepoService : IAugmentedRepoService
{
    const int maxCommitCount = 30000;

    private readonly IGitService gitService;

    public AugmentedRepoService(IGitService gitService)
    {
        this.gitService = gitService;
    }

    public event EventHandler? RepoChange;

    public async Task<R<Repo>> GetRepoAsync(string path)
    {
        var git = gitService.GetGit(path);

        var gitLog = await git.GetLogAsync(maxCommitCount);
        if (gitLog.IsError)
        {
            return gitLog.Error;
        }

        GitRepo gitRepo = new GitRepo(gitLog.Value);

        return await GetAugmentedRepo(gitRepo);
    }

    protected virtual void OnRepoChange(EventArgs e)
    {
        EventHandler? handler = RepoChange;
        handler?.Invoke(this, e);
    }

    async Task<R<Repo>> GetAugmentedRepo(GitRepo gitRepo)
    {
        return new Repo(gitRepo.Commits);
    }
}
