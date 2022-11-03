namespace gmd.ViewRepos;

interface IViewRepoService
{
    public event EventHandler RepoChange;
    Task<R<Repo>> GetRepoAsync(string path);
    Task<R<Repo>> GetRepoAsync(string path, string[] showBranches);
}
