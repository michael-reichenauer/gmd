using gmd.ViewRepos.Private.Augmented;

namespace gmd.ViewRepos.Private;


class ViewRepoService : IViewRepoService
{
    private readonly IAugmentedRepoService augmentedRepoService;

    public ViewRepoService(IAugmentedRepoService augmentedRepoService)
    {
        this.augmentedRepoService = augmentedRepoService;
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
        return new Repo(
            augmentedRepo.Commits.Select(c =>
                new Commit(c.Id, c.Sid, c.ParentIds, c.Subject, c.Message,
                    c.Author, c.AuthorTime, c.CommitTime)).ToList(),
            augmentedRepo.Branches.Select(b =>
                new Branch(b.Name, b.DisplayName, b.TipID, b.IsCurrent, b.IsRemote, b.RemoteName,
                    b.IsDetached, b.AheadCount, b.BehindCount, b.IsRemoteMissing)).ToList());
    }
}