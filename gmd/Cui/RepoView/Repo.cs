using gmd.Server;

namespace gmd.Cui.RepoView;


interface IRepo
{
    IRepoCommands Cmd { get; }

    Repo Repo { get; }
    //IReadOnlyList<Commit> Commits { get; }


    Graph Graph { get; }

    int TotalRows { get; }
    int CurrentIndex { get; }
    int ContentWidth { get; }
    Commit RowCommit { get; }
    Branch RowBranch { get; }
    Branch? CurrentBranch { get; }

    IReadOnlyList<Branch> GetAllBranches();
    Branch GetCurrentBranch();
    Commit GetCurrentCommit();
    IReadOnlyList<Branch> GetCommitBranches(bool isAll);
    Task<R<IReadOnlyList<string>>> GetFilesAsync();
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
        Server.IServer server)
    {
        this.repoView = repoView;
        this.serverRepo = serverRepo;
        this.repoCommands = newRepoCommands(this, serverRepo, repoView);
        this.server = server;
        this.Graph = graphService.Create(serverRepo);
    }


    public IRepoCommands Cmd => repoCommands;

    public Repo Repo => serverRepo;
    //public IReadOnlyList<Commit> Commits => serverRepo.ViewCommits;

    public Commit RowCommit => serverRepo.ViewCommits[CurrentIndex];
    public Branch RowBranch => serverRepo.BranchByName[RowCommit.BranchName];
    public Branch? CurrentBranch => serverRepo.ViewBranches.FirstOrDefault(b => b.IsCurrent);

    public Graph Graph { get; init; }

    public int TotalRows => serverRepo.ViewCommits.Count;
    public int CurrentIndex => Math.Min(repoView.CurrentIndex, TotalRows - 1);
    public int ContentWidth => repoView.ContentWidth;

    public async Task<R<IReadOnlyList<string>>> GetFilesAsync()
    {
        var commit = RowCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;
        return await server.GetFileAsync(reference, Repo.Path);
    }

    public Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);
    public Commit GetCurrentCommit()
    {
        var c = serverRepo.CommitById[GetCurrentBranch().TipId];
        if (c.IsUncommitted && c.ParentIds.Count > 0)
        {
            c = serverRepo.CommitById[c.ParentIds[0]];
        }
        return c;
    }

    public IReadOnlyList<Branch> GetAllBranches() => serverRepo.AllBranches.ToList();


    public IReadOnlyList<Branch> GetCommitBranches(bool isAll) =>
        server.GetCommitBranches(Repo, RowCommit.Id, isAll);
}

