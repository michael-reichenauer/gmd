namespace gmd.ViewRepos;

interface IViewRepoService
{
    event EventHandler<ChangeEventArgs> RepoChange;
    event EventHandler<ChangeEventArgs> StatusChange;

    Task<R<Repo>> GetRepoAsync(string path);
    Task<R<Repo>> GetRepoAsync(string path, string[] showBranches);
    Task<R<Repo>> GetFreshRepoAsync(Repo repo);
    Task<R<Repo>> GetNewStatusRepoAsync(Repo repo);

    IReadOnlyList<Branch> GetAllBranches(Repo repo);
    IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId);
    Repo ShowBranch(Repo repo, string branchName);
    Repo HideBranch(Repo repo, string name);

    // Git commands
    Task<R> CommitAllChangesAsync(Repo repo, string message);
}

internal class ChangeEventArgs : EventArgs
{
    public DateTime TimeStamp { get; }

    public ChangeEventArgs(DateTime timeStamp)
    {
        TimeStamp = timeStamp;
    }
}
