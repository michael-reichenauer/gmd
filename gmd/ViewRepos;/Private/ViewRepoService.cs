using gmd.ViewRepos.Private.Augmented;

namespace gmd.ViewRepos.Private;


class ViewRepoService : IViewRepoService
{
    private readonly IAugmentedRepoService augmentedRepoService;
    private readonly IConverter converter;

    public ViewRepoService(
        IAugmentedRepoService augmentedRepoService,
        IConverter converter)
    {
        this.augmentedRepoService = augmentedRepoService;
        this.converter = converter;
    }

    public event EventHandler? RepoChange;


    public async Task<R<Repo>> GetRepoAsync(string path)
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

    async Task<R<Repo>> GetViewRepoAsync(Augmented.Repo augmentedRepo)
    {
        return converter.ToRepo(augmentedRepo);
    }


}

