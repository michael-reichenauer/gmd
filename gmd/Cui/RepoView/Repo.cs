using gmd.Server;

namespace gmd.Cui.RepoView;


interface IRepo
{
    IRepoCommands Cmd { get; }

    Repo Repo { get; }

    Graph Graph { get; }

    int CurrentIndex { get; }
    Commit RowCommit { get; }
    Branch RowBranch { get; }

    IReadOnlyList<Branch> GetCommitBranches(bool isAll);
}

class RepoImpl : IRepo
{
    readonly IRepoView repoView;
    readonly IServer server;
    readonly IRepoCommands repoCommands;
    readonly Repo serverRepo;

    internal RepoImpl(
        IRepoView repoView,
        Repo serverRepo,
        Func<IRepo, Repo, IRepoView, IRepoCommands> newRepoCommands,
        IGraphCreater graphService,
        IServer server)
    {
        this.repoView = repoView;
        this.serverRepo = serverRepo;
        this.repoCommands = newRepoCommands(this, serverRepo, repoView);
        this.server = server;
        this.Graph = graphService.Create(serverRepo);
    }


    public IRepoCommands Cmd => repoCommands;

    public Repo Repo => serverRepo;

    public Commit RowCommit => serverRepo.ViewCommits[CurrentIndex];
    public Branch RowBranch => serverRepo.BranchByName[RowCommit.BranchName];


    public Graph Graph { get; init; }


    public int CurrentIndex => Math.Min(repoView.CurrentIndex, serverRepo.ViewCommits.Count - 1);


    public IReadOnlyList<Branch> GetCommitBranches(bool isAll) =>
        server.GetCommitBranches(Repo, RowCommit.Id, isAll);
}

