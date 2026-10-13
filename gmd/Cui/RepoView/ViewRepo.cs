using gmd.Cui.Graphs;
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

    // The shows and hides Backspace can undo, and what the last search found, both of which
    // outlive this view repo, see IRepoView
    ShownHistory ShownHistory { get; }
    SearchMatches SearchMatches { get; }

    // The hidden branches with something not yet seen, see HiddenNews
    IReadOnlyList<HiddenBranchNews> HiddenNews { get; }
}

class ViewRepo : IViewRepo
{
    readonly IRepoView repoView;
    readonly IServer server;
    readonly CommandContext commands;
    readonly Repo serverRepo;

    internal ViewRepo(
        IRepoView repoView,
        Repo serverRepo,
        CommandContext commands,
        IGraphCreater graphService,
        IServer server
    )
    {
        this.repoView = repoView;
        this.serverRepo = serverRepo;
        this.commands = commands;
        this.server = server;

        this.Graph = graphService.Create(serverRepo);
    }

    public string Path => serverRepo.Path;
    public Status Status => serverRepo.Status;

    public IRepoView RepoView => repoView;
    public IRepoCommands Cmds => commands.RepoCmds;
    public ICommitCommands CommitCmds => commands.CommitCmds;
    public IBranchCommands BranchCmds => commands.BranchCmds;

    public Repo Repo => serverRepo;
    public Graph Graph { get; init; }

    public Commit RowCommit => serverRepo.ViewCommits[CurrentIndex];
    public Branch RowBranch => serverRepo.BranchByName[RowCommit.BranchName];

    public int CurrentIndex => Math.Min(repoView.CurrentIndex, serverRepo.ViewCommits.Count - 1);

    public IReadOnlyList<Branch> GetCommitBranches(bool isAll) => server.GetCommitBranches(Repo, RowCommit.Id, isAll);

    public string CurrentAuthor => serverRepo.CurrentAuthor;

    public ShownHistory ShownHistory => repoView.ShownHistory;
    public SearchMatches SearchMatches => repoView.SearchMatches;
    public IReadOnlyList<HiddenBranchNews> HiddenNews => repoView.HiddenNews;
}
