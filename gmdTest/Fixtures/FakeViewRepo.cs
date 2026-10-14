using gmd.Common;
using gmd.Cui.Common;
using gmd.Cui.Graphs;
using gmd.Cui.RepoView;
using gmd.Server;

namespace gmdTest.Fixtures;

// Double for IViewRepo, the snapshot of the shown repo the log view's writer, menus and commands
// read. It carries the repo and the graph drawn from it, which is all RepoWriter.ToPage reads; the
// command classes and the menus reach it through a FakeRepoView.
class FakeViewRepo : IViewRepo
{
    readonly IServer? server;

    public FakeViewRepo(Repo repo, IRepoConfigService? repoConfig = null, IServer? server = null)
    {
        this.server = server;
        Repo = repo;
        Graph = new GraphCreater(new BranchColorService(repoConfig ?? new FakeRepoConfig())).Create(repo);
    }

    public Repo Repo { get; }
    public Graph Graph { get; }

    public string Path => Repo.Path;
    public Status Status => Repo.Status;

    // Which row the cursor is on, i.e. what RowCommit and RowBranch are taken from
    public int CurrentIndex { get; set; } = 0;

    public Commit RowCommit => Repo.ViewCommits[CurrentIndex];
    public Branch RowBranch => Repo.BranchByName[RowCommit.BranchName];

    public string CurrentAuthor { get; set; } = "Test Author";

    public IReadOnlyList<Branch> GetCommitBranches(bool isAll) =>
        server?.GetCommitBranches(Repo, RowCommit.Id, isAll)
        ?? throw new NotSupportedException("FakeViewRepo was given no server");

    // Its own, since there is no view to own it, for tests of what a show or hide makes undoable
    public ShownHistory ShownHistory { get; } = new();
    public SearchMatches SearchMatches { get; } = new();
    public IReadOnlyList<HiddenBranchNews> HiddenNews { get; set; } = [];
}
