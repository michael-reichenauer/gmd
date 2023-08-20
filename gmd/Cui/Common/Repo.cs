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
    Branch Branch(string branchName);
    Commit Commit(string commitId);

    Graph Graph { get; }

    int TotalRows { get; }
    int CurrentIndex { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    Commit RowCommit { get; }
    Branch RowBranch { get; }
    Branch? CurrentBranch { get; }

    IReadOnlyList<Branch> GetAllBranches();
    Commit GetCommit(string id);
    Branch GetCurrentBranch();
    Commit GetCurrentCommit();
    bool IsBranchShown(string branchName);
    IReadOnlyList<Branch> GetCommitBranches(bool isAll);
    Task<R<IReadOnlyList<string>>> GetFilesAsync();
    IReadOnlyList<string> GetUncommittedFiles();
    Branch AllBranchByName(string name);
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

    public bool IsBranchShown(string branchName) => serverRepo.BranchByName.ContainsKey(branchName);

    public IRepoCommands Cmd => repoCommands;

    public Repo Repo => serverRepo;
    public string RepoPath => serverRepo.Path;
    public Status Status => serverRepo.Status;
    public IReadOnlyList<Branch> Branches => serverRepo.Branches;
    public IReadOnlyList<Commit> Commits => serverRepo.Commits;
    public Branch Branch(string branchName) => serverRepo.BranchByName[branchName];
    public Commit Commit(string commitId) => serverRepo.CommitById[commitId];

    public Commit RowCommit => Commits[CurrentIndex];
    public Branch RowBranch => Branch(RowCommit.BranchName);
    public Branch? CurrentBranch => Branches.FirstOrDefault(b => b.IsCurrent);
    public Branch AllBranchByName(string name) => server.AllBranchByName(Repo, name);

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
    public Commit GetCurrentCommit() => GetCommit(GetCurrentBranch().TipId);

    public IReadOnlyList<Branch> GetAllBranches() => server.GetAllBranches(Repo);
    public Commit GetCommit(string commitId)
    {
        if (Repo.CommitById.TryGetValue(commitId, out var commit))
        {
            return commit;
        }

        return server.GetCommit(Repo, commitId); ;
    }

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

