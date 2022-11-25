using gmd.Common;
using gmd.Git;
using gmd.Installation;
using gmd.Server;
using Terminal.Gui;


namespace gmd.Cui.Common;


interface IRepo
{
    IRepoCommands Cmd { get; }
    Server.Repo Repo { get; }
    Graph Graph { get; }
    int TotalRows { get; }
    int CurrentIndex { get; }
    Server.Commit CurrentIndexCommit { get; }
    Server.Branch? CurrentBranch { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    bool HasUncommittedChanges { get; }



    // void ShowBranch(string name, bool includeAmbiguous);
    // void HideBranch(string name);
    IReadOnlyList<Server.Branch> GetAllBranches();
    IReadOnlyList<Server.Branch> GetShownBranches();
    Server.Branch GetCurrentBranch();
    IReadOnlyList<Server.Branch> GetCommitBranches();
    Task<R<IReadOnlyList<string>>> GetFilesAsync();
    IReadOnlyList<string> GetUncommittedFiles();

    Server.Branch ViewedBranchByName(string name);
    Server.Branch AllBranchByName(string name);
}

class RepoImpl : IRepo
{
    readonly IRepoView repoView;
    readonly IGraphService graphService;
    readonly Server.IServer server;
    readonly ICommitDlg commitDlg;
    readonly IDiffView diffView;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IProgress progress;
    readonly IFilterDlg filterDlg;
    readonly IStates states;
    readonly IGit git;
    readonly IUpdater updater;
    readonly IRepoCommands repoCommands;

    internal RepoImpl(
        Func<IRepo, Server.Repo, IRepoView, IRepoCommands> newRepoCommands,
        IRepoView repoView,
        Server.Repo serverRepo,
        IGraphService graphService,
        Server.IServer server,
        ICommitDlg commitDlg,
        IDiffView diffView,
        ICreateBranchDlg createBranchDlg,
        IProgress progress,
        IFilterDlg filterDlg,
        IStates states,
        IGit git,
        IUpdater updater)
    {
        this.repoView = repoView;
        Repo = serverRepo;
        repoCommands = newRepoCommands(this, serverRepo, repoView);
        this.graphService = graphService;
        this.server = server;
        this.commitDlg = commitDlg;
        this.diffView = diffView;
        this.createBranchDlg = createBranchDlg;
        this.progress = progress;
        this.filterDlg = filterDlg;
        this.states = states;
        this.git = git;
        this.updater = updater;
        Graph = graphService.CreateGraph(serverRepo);
    }

    public IRepoCommands Cmd => repoCommands;

    public Server.Repo Repo { get; init; }
    public Server.Commit CurrentIndexCommit => Repo.Commits[CurrentIndex];
    public bool HasUncommittedChanges => !Repo.Status.IsOk;
    public Server.Branch? CurrentBranch => Repo.Branches.FirstOrDefault(b => b.IsCurrent);
    public Server.Branch ViewedBranchByName(string name) => Repo.BranchByName[name];
    public Server.Branch AllBranchByName(string name) => server.AllBanchByName(Repo, name);


    public Graph Graph { get; init; }

    public int TotalRows => Repo.Commits.Count;
    public int CurrentIndex => repoView.CurrentIndex;
    public int ContentWidth => repoView.ContentWidth;
    public Point CurrentPoint => repoView.CurrentPoint;


    public void UpdateRepoTo(Server.Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);
    public void ToggleDetails() => repoView.ToggleDetails();


    public async Task<R<IReadOnlyList<string>>> GetFilesAsync()
    {
        var commit = CurrentIndexCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;
        return await server.GetFileAsync(reference, Repo.Path);
    }

    public Server.Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);

    public IReadOnlyList<Server.Branch> GetAllBranches() => server.GetAllBranches(Repo);
    public IReadOnlyList<Server.Branch> GetShownBranches() => Repo.Branches;

    public IReadOnlyList<Server.Branch> GetCommitBranches() =>
        server.GetCommitBranches(Repo, CurrentIndexCommit.Id);


    public IReadOnlyList<string> GetUncommittedFiles() =>
        Repo.Status.ModifiedFiles
        .Concat(Repo.Status.AddedFiles)
        .Concat(Repo.Status.DeleteddFiles)
        .Concat(Repo.Status.ConflictsFiles)
        .OrderBy(f => f)
        .ToList();


    void Do(Func<Task<R>> action)
    {
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                if (!Try(out var e, await action()))
                {
                    UI.ErrorMessage($"{e.AllErrorMessages()}");
                }
            }
        });
    }

}

