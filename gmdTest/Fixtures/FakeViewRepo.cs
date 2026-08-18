using gmd.Common;
using gmd.Cui;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;

namespace gmdTest.Fixtures;

// Double for IViewRepo, the per view facade the log view's writer, menus and commands are given.
// It carries the shown repo and the graph drawn from it, which is all RepoWriter.ToPage reads.
//
// The command properties are null rather than throwing: the menus read them once in their
// constructors but only ever call them from the Action a user picks, which a test that asserts
// menu titles never invokes. Everything else throws, as FakeGit does, so a test that starts
// depending on more of the facade fails loudly rather than passing on an empty result.
class FakeViewRepo : IViewRepo
{
    readonly IServer? server;

    public FakeViewRepo(Repo repo, IRepoConfig? repoConfig = null, IServer? server = null)
    {
        this.server = server;
        Repo = repo;
        Graph = new GraphCreater(new BranchColorService(repoConfig ?? new FakeRepoConfig())).Create(repo);
    }

    public Repo Repo { get; }
    public Graph Graph { get; }

    public string Path => Repo.Path;
    public Status Status => Repo.Status;

    // Which row the cursor is on, i.e. what RowCommit and RowBranch are taken from
    public int CurrentIndex { get; set; } = 0;

    public Commit RowCommit => Repo.ViewCommits[CurrentIndex];
    public Branch RowBranch => Repo.BranchByName[RowCommit.BranchName];

    public string CurrentAuthor { get; set; } = "Test Author";

    public IReadOnlyList<Branch> GetCommitBranches(bool isAll) =>
        server?.GetCommitBranches(Repo, RowCommit.Id, isAll)
        ?? throw new NotSupportedException("FakeViewRepo was given no server");

    public IRepoCommands Cmds => null!;
    public ICommitCommands CommitCmds => null!;
    public IBranchCommands BranchCmds => null!;

    public IRepoView RepoView => throw new NotSupportedException("FakeViewRepo has no view");
}
