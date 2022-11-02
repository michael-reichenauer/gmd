namespace gmd.ViewRepos.Private.Augmented;

interface IAugmentedRepoService
{
    public event EventHandler RepoChange;
    Task<R<Repo>> GetRepoAsync(string path);
}
