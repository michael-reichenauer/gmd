using static System.Environment;

namespace gmd.Common;


public class RepoState
{
    public List<string> Branches { get; set; } = new List<string>();
}


interface IRepoState
{
    RepoState GetRepo(string rootPath);
    void SetRepo(string rootPath, Action<RepoState> setState);
}

class RepoStateImpl : IRepoState
{
    static readonly string StateFileName = ".gmdstate.json";

    private readonly IFileStore store;

    public RepoStateImpl(IFileStore store) => this.store = store;


    public RepoState GetRepo(string path) => store.Get<RepoState>(RepoPath(path));

    public void SetRepo(string path, Action<RepoState> setState) =>
        store.Set(RepoPath(path), setState);

    string RepoPath(string path) => Path.Join(path, ".git", StateFileName);
}