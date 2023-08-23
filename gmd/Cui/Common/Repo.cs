using gmd.Server;
using Terminal.Gui;


namespace gmd.Cui.Common;


interface IRepo
{
    IRepoCommands Cmd { get; }

    Repo Repo { get; }
    string RepoPath { get; }
    Status Status { get; }
    IReadOnlyList<Branch> Branches { get; }
    IReadOnlyList<Commit> Commits { get; }
    Branch BranchByName(string branchName);
    Commit CommitById(string commitId);

    Graph Graph { get; }

    int TotalRows { get; }
    int CurrentIndex { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    Commit RowCommit { get; }
    Branch RowBranch { get; }
    Branch? CurrentBranch { get; }

    IReadOnlyList<Branch> GetAllBranches();
    Branch GetCurrentBranch();
    Commit GetCurrentCommit();
    IReadOnlyList<Branch> GetCommitBranches(bool isAll);
    Task<R<IReadOnlyList<string>>> GetFilesAsync();
    IReadOnlyList<string> GetUncommittedFiles();
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
    public string RepoPath => serverRepo.Path;
    public Status Status => serverRepo.Status;
    public IReadOnlyList<Branch> Branches => serverRepo.ViewBranches;
    public IReadOnlyList<Commit> Commits => serverRepo.ViewCommits;
    public Branch BranchByName(string branchName) => serverRepo.BranchByName[branchName];
    public Commit CommitById(string commitId) => serverRepo.CommitById[commitId];

    public Commit RowCommit => Commits[CurrentIndex];
    public Branch RowBranch => BranchByName(RowCommit.BranchName);
    public Branch? CurrentBranch => Branches.FirstOrDefault(b => b.IsCurrent);

    public Graph Graph { get; init; }

    public int TotalRows => Commits.Count;
    public int CurrentIndex => Math.Min(repoView.CurrentIndex, TotalRows - 1);
    public int ContentWidth => repoView.ContentWidth;
    public Point CurrentPoint => repoView.CurrentPoint;

    public async Task<R<IReadOnlyList<string>>> GetFilesAsync()
    {
        var commit = RowCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;
        return await server.GetFileAsync(reference, RepoPath);
    }

    public Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);
    public Commit GetCurrentCommit() => serverRepo.CommitById[GetCurrentBranch().TipId];

    public IReadOnlyList<Branch> GetAllBranches() => serverRepo.AllBranches.ToList();


    public IReadOnlyList<Branch> GetCommitBranches(bool isAll) =>
        server.GetCommitBranches(Repo, RowCommit.Id, isAll);

    public IReadOnlyList<string> GetUncommittedFiles() =>
        Status.ModifiedFiles
        .Concat(Status.AddedFiles)
        .Concat(Status.DeletedFiles)
        .Concat(Status.ConflictsFiles)
        .Concat(Status.RenamedTargetFiles)
        .OrderBy(f => f)
        .ToList();
}

