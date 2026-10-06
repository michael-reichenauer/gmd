using gmd.Common.Private;
using gmd.Git;

namespace gmd.Common;

class RepoConfig
{
    public bool SyncMetaData { get; set; } = false;

    // The names of this repo's integration branches, e.g. 'staging', which the branch inference takes
    // like develop and dev: as the branches others are started from (see WellKnownBranches)
    public List<string> IntegrationBranches { get; set; } = [];

    public List<string> Branches { get; set; } = [];
    public Dictionary<string, int> BranchColors { get; set; } = [];
    public List<BranchOrder> BranchOrders { get; set; } = [];

    // The tip each branch had when it was last shown, by name, for telling what is new on the hidden
    // ones, see HiddenNews
    public Dictionary<string, string> SeenTips { get; set; } = [];

    // False for SeenTips as an older gmd left it, which held only the remote branches, see HiddenNews.Seen
    public bool IsSeenTipsForAll { get; set; } = false;

    // The last change gmd made to a branch, by branch name, where the branch's reflog alone would name
    // it wrong: a squash, which is several moves, and an undo, which is a reset that Undo again redoes.
    // Taken only while the reflog still lists it as the latest, see ReflogSteps.StepsByBranch.
    public Dictionary<string, RecordedStep> UndoSteps { get; set; } = [];
}

// A change gmd made to a branch: the branch was at BeforeId, the change wrote Moves entries to the
// branch's reflog, and left it at AfterId, or wherever its last entry says when that is empty. Kind
// is a StepKind name, and Name what the change was of.
public class RecordedStep
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string BeforeId { get; set; } = "";
    public string AfterId { get; set; } = "";
    public int Moves { get; set; } = 1;
    public bool IsRedo { get; set; } = false;
}

public class BranchOrder
{
    public string Branch { get; set; } = "";
    public string Other { get; set; } = "";
    public int Order { get; set; } = 0;
}

interface IRepoConfig
{
    RepoConfig Get(string rootPath);
    void Set(string rootPath, Action<RepoConfig> set);
}

// cSpell:ignore gmdconfig
class RepoConfigImpl : IRepoConfig
{
    static readonly string FileName = ".gmdconfig";
    private readonly IFileStore store;

    public RepoConfigImpl(IFileStore store) => this.store = store;

    public RepoConfig Get(string path) => store.Get<RepoConfig>(RepoPath(path));

    public void Set(string path, Action<RepoConfig> set) => store.Set(RepoPath(path), set);

    // The file lives in the *common* git dir, so every worktree of a repository shares one: which
    // branches are shown, their colors and order are properties of the history, not of a checkout,
    // and nothing is lost when a linked worktree is removed. In a linked worktree '<root>/.git' is
    // a file, so joining onto it would try to write inside a file — and a failed write is fatal.
    static string RepoPath(string path)
    {
        var gitDir = GitDir.Resolve(path) is GitDirInfo info ? info.CommonDirPath : Path.Join(path, ".git");
        return Path.Join(gitDir, FileName);
    }
}
