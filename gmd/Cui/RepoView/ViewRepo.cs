using gmd.Cui.Graphs;
using gmd.Server;

namespace gmd.Cui.RepoView;

// The repo as the log view shows it at one moment, with the graph drawn from it and the cursor's
// row: a snapshot, which every refresh, show and hide replaces, and which the menus are made with.
// The command classes, made once, read the one shown when they run, see CommandContext.
interface IViewRepo
{
    Repo Repo { get; }
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
    readonly Repo serverRepo;

    internal ViewRepo(IRepoView repoView, Repo serverRepo, IGraphCreater graphService, IServer server)
    {
        this.repoView = repoView;
        this.serverRepo = serverRepo;
        this.server = server;

        this.Graph = graphService.Create(serverRepo);
    }

    public string Path => serverRepo.Path;
    public Status Status => serverRepo.Status;

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
