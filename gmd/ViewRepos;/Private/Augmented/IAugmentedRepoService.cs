namespace gmd.ViewRepos.Private.Augmented;

interface IAugmentedRepoService
{
    public event EventHandler<ChangeEventArgs> RepoChange;
    public event EventHandler<ChangeEventArgs> StatusChange;
    Task<R<Repo>> GetRepoAsync(string path);
}
