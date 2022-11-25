using gmd.Server;
using Terminal.Gui;


namespace gmd.Cui.Common;


interface IRepo
{
    IRepoCommands Cmd { get; }
    Server.Repo Repo { get; }
    Server.Status Status { get; }
    IReadOnlyList<Server.Branch> Branches { get; }
    Server.Branch Branch(string branchName);
    Server.Commit Commit(string commitId);
    IReadOnlyList<Server.Commit> Commits { get; }
    Graph Graph { get; }

    int TotalRows { get; }
    int CurrentRow { get; }
    Server.Commit RowCommit { get; }
    Server.Branch? CurrentBranch { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    string RepoPath { get; }

    IReadOnlyList<Server.Branch> GetAllBranches();
    Server.Branch GetCurrentBranch();
    IReadOnlyList<Server.Branch> GetCommitBranches();
    Task<R<IReadOnlyList<string>>> GetFilesAsync();
    IReadOnlyList<string> GetUncommittedFiles();

    Server.Branch AllBranchByName(string name);
}

class RepoImpl : IRepo
{
    readonly IRepoView repoView;
    readonly Server.IServer server;
    readonly IRepoCommands repoCommands;
    readonly Server.Repo serverRepo;

    internal RepoImpl(
        IRepoView repoView,
        Server.Repo serverRepo,
        Func<IRepo, Server.Repo, IRepoView, IRepoCommands> newRepoCommands,
        IGraphService graphService,
        Server.IServer server)
    {
        this.repoView = repoView;
        this.serverRepo = serverRepo;
        this.repoCommands = newRepoCommands(this, serverRepo, repoView);
        this.server = server;
        this.Graph = graphService.CreateGraph(serverRepo);
    }

    public IRepoCommands Cmd => repoCommands;

    public Server.Repo Repo => serverRepo;
    public string RepoPath => serverRepo.Path;
    public Server.Status Status => serverRepo.Status;
    public IReadOnlyList<Server.Branch> Branches => serverRepo.Branches;
    public IReadOnlyList<Server.Commit> Commits => serverRepo.Commits;
    public Server.Branch Branch(string branchName) => serverRepo.BranchByName[branchName];
    public Server.Commit Commit(string commitId) => serverRepo.CommitById[commitId];

    public Server.Commit RowCommit => Commits[CurrentRow];
    public Server.Branch? CurrentBranch => Branches.FirstOrDefault(b => b.IsCurrent);
    public Server.Branch AllBranchByName(string name) => server.AllBanchByName(Repo, name);


    public Graph Graph { get; init; }

    public int TotalRows => Commits.Count;
    public int CurrentRow => repoView.CurrentIndex;
    public int ContentWidth => repoView.ContentWidth;
    public Point CurrentPoint => repoView.CurrentPoint;


    public void UpdateRepoTo(Server.Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);
    public void ToggleDetails() => repoView.ToggleDetails();


    public async Task<R<IReadOnlyList<string>>> GetFilesAsync()
    {
        var commit = RowCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;
        return await server.GetFileAsync(reference, RepoPath);
    }

    public Server.Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);

    public IReadOnlyList<Server.Branch> GetAllBranches() => server.GetAllBranches(Repo);

    public IReadOnlyList<Server.Branch> GetCommitBranches() =>
        server.GetCommitBranches(Repo, RowCommit.Id);


    public IReadOnlyList<string> GetUncommittedFiles() =>
        Status.ModifiedFiles
        .Concat(Status.AddedFiles)
        .Concat(Status.DeleteddFiles)
        .Concat(Status.ConflictsFiles)
        .OrderBy(f => f)
        .ToList();
}

