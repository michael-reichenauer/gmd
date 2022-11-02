using gmd.ViewRepos.Augmented;

namespace gmd.ViewRepos;

interface IViewRepoService
{
    public event EventHandler RepoChange;
    Task<R<ViewRepo>> GetRepoAsync(string path);
}


class ViewRepoService : IViewRepoService
{
    private readonly IAugmentedRepoService augmentedRepoService;

    public ViewRepoService(IAugmentedRepoService augmentedRepoService)
    {
        this.augmentedRepoService = augmentedRepoService;
    }

    public event EventHandler? RepoChange;


    public async Task<R<ViewRepo>> GetRepoAsync(string path)
    {
        var augmentedRepo = await augmentedRepoService.GetRepoAsync(path);
        if (augmentedRepo.IsError)
        {
            return augmentedRepo.Error;
        }

        return await GetViewRepoAsync(augmentedRepo.Value);
    }

    protected virtual void OnRepoChange(EventArgs e)
    {
        EventHandler? handler = RepoChange;
        handler?.Invoke(this, e);
    }

    async Task<R<ViewRepo>> GetViewRepoAsync(AugmentedRepo augmentedRepo)
    {
        return new ViewRepo(augmentedRepo.Commits);
    }
}