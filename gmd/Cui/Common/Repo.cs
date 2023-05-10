using gmd.Server;
using Terminal.Gui;


namespace gmd.Cui.Common;


interface IRepo
{
    IRepoCommands Cmd { get; }

    Server.Repo Repo { get; }
    string RepoPath { get; }
    Server.Status Status { get; }
    IReadOnlyList<Server.Branch> Branches { get; }
    IReadOnlyList<Server.Commit> Commits { get; }
    Server.Branch Branch(string branchName);
    Server.Commit Commit(string commitId);

    Graph Graph { get; }

    int TotalRows { get; }
    int CurrentRow { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    Server.Commit RowCommit { get; }
    Server.Branch RowBranch { get; }
    Server.Branch? CurrentBranch { get; }

    IReadOnlyList<Server.Branch> GetAllBranches();
    Server.Commit GetCommit(string id);
    Server.Branch GetCurrentBranch();
    Server.Commit GetCurrentCommit();
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

    public Server.Repo Repo => serverRepo;
    public string RepoPath => serverRepo.Path;
    public Server.Status Status => serverRepo.Status;
    public IReadOnlyList<Server.Branch> Branches => serverRepo.Branches;
    public IReadOnlyList<Server.Commit> Commits => serverRepo.Commits;
    public Server.Branch Branch(string branchName) => serverRepo.BranchByName[branchName];
    public Server.Commit Commit(string commitId) => serverRepo.CommitById[commitId];

    public Server.Commit RowCommit => Commits[CurrentRow];
    public Server.Branch RowBranch => Branch(RowCommit.BranchName);
    public Server.Branch? CurrentBranch => Branches.FirstOrDefault(b => b.IsCurrent);
    public Server.Branch AllBranchByName(string name) => server.AllBanchByName(Repo, name);

    public Graph Graph { get; init; }

    public int TotalRows => Commits.Count;
    public int CurrentRow => repoView.CurrentIndex;
    public int ContentWidth => repoView.ContentWidth;
    public Point CurrentPoint => repoView.CurrentPoint;

    public async Task<R<IReadOnlyList<string>>> GetFilesAsync()
    {
        var commit = RowCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;
        return await server.GetFileAsync(reference, RepoPath);
    }

    public Server.Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);
    public Server.Commit GetCurrentCommit() => GetCommit(GetCurrentBranch().TipId);

    public IReadOnlyList<Server.Branch> GetAllBranches() => server.GetAllBranches(Repo);
    public Server.Commit GetCommit(string commitId)
    {
        if (Repo.CommitById.TryGetValue(commitId, out var commit))
        {
            return commit;
        }

        return server.GetCommit(Repo, commitId); ;
    }

    public IReadOnlyList<Server.Branch> GetCommitBranches() =>
        server.GetCommitBranches(Repo, RowCommit.Id);

    public IReadOnlyList<string> GetUncommittedFiles() =>
        Status.ModifiedFiles
        .Concat(Status.AddedFiles)
        .Concat(Status.DeletedFiles)
        .Concat(Status.ConflictsFiles)
        .OrderBy(f => f)
        .ToList();
}

