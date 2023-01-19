namespace gmd.Common;

public class RepoState
{
    public List<string> Branches { get; set; } = new List<string>();
}

interface IRepoState
{
    RepoState GetRepo(string rootPath);
    void SetRepo(string rootPath, Action<RepoState> set);
}

class RepoStateImpl : IRepoState
{
    static readonly string FileName = ".gmdstate.json";
    private readonly IFileStore store;

    public RepoStateImpl(IFileStore store) => this.store = store;

    public RepoState GetRepo(string path) => store.Get<RepoState>(RepoPath(path));

    public void SetRepo(string path, Action<RepoState> set) => store.Set(RepoPath(path), set);

    string RepoPath(string path) => Path.Join(path, ".git", FileName);
}