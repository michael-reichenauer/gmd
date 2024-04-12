using gmd.Server;

namespace gmd.Cui.RepoView;

interface IViewRepo
{
    IRepoCommands Cmds { get; }
    ICommitCommands CommitCmds { get; }
    IBranchCommands BranchCmds { get; }

    Repo Repo { get; }
    IRepoView RepoView { get; }
    string Path { get; }
    Status Status { get; }

    Graph Graph { get; }

    int CurrentIndex { get; }
    Commit RowCommit { get; }
    Branch RowBranch { get; }

    string CurrentAuthor { get; }
    IReadOnlyList<Branch> GetCommitBranches(bool isAll);
}

class ViewRepo : IViewRepo
{
    readonly IRepoView repoView;
    readonly IServer server;
    readonly IRepoCommands repoCommands;
    readonly ICommitCommands commitCommands;
    readonly IBranchCommands branchCommands;
    readonly Repo serverRepo;

    internal ViewRepo(
        IRepoView repoView,
        Repo serverRepo,
        Func<IViewRepo, IRepoView, IRepoCommands> newRepoCommands,
        Func<IViewRepo, IRepoView, ICommitCommands> newCommitCommands,
        Func<IViewRepo, IRepoView, IBranchCommands> newBranchCommands,
        IGraphCreater graphService,
        IServer server)
    {
        this.repoView = repoView;
        this.serverRepo = serverRepo;
        this.repoCommands = newRepoCommands(this, repoView);
        this.commitCommands = newCommitCommands(this, repoView);
        this.branchCommands = newBranchCommands(this, repoView);
        this.server = server;

        this.Graph = graphService.Create(serverRepo);
    }

    public string Path => serverRepo.Path;
    public Status Status => serverRepo.Status;

    public IRepoView RepoView => repoView;
    public IRepoCommands Cmds => repoCommands;
    public ICommitCommands CommitCmds => commitCommands;
    public IBranchCommands BranchCmds => branchCommands;

    public Repo Repo => serverRepo;
    public Graph Graph { get; init; }

    public Commit RowCommit => serverRepo.ViewCommits[CurrentIndex];
    public Branch RowBranch => serverRepo.BranchByName[RowCommit.BranchName];


    public int CurrentIndex => Math.Min(repoView.CurrentIndex, serverRepo.ViewCommits.Count - 1);

    public IReadOnlyList<Branch> GetCommitBranches(bool isAll) =>
        server.GetCommitBranches(Repo, RowCommit.Id, isAll);

    public string CurrentAuthor => server.CurrentAuthor;
}

