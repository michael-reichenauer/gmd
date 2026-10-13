using gmd.Common;
using gmd.Git;
using GitBranch = gmd.Git.Branch;
using GitCommit = gmd.Git.Commit;
using GitStash = gmd.Git.Stash;
using GitStatus = gmd.Git.Status;
using GitTag = gmd.Git.Tag;
using GitWorktree = gmd.Git.Worktree;

namespace gmd.Server.Private.Augmented.Private;

class GitRepo
{
    internal GitRepo(
        DateTime timeStamp,
        string path,
        IReadOnlyList<GitCommit> commits,
        IReadOnlyList<GitBranch> branches,
        IReadOnlyList<GitTag> tags,
        GitStatus status,
        MetaData metaData,
        IReadOnlyList<GitStash> stashes,
        bool isTruncated,
        IReadOnlyList<GitWorktree>? worktrees = null,
        IReadOnlyList<ReflogEntry>? reflog = null,
        IReadOnlyCollection<string>? integrationNames = null,
        IReadOnlyDictionary<string, RecordedStep>? recordedSteps = null,
        IReadOnlyList<ReflogEntry>? remoteReflog = null,
        string currentAuthor = ""
    )
    {
        TimeStamp = timeStamp;
        Path = path;
        Commits = commits;
        Branches = branches;
        Tags = tags;
        Status = status;
        MetaData = metaData;
        Stashes = stashes;
        IsTruncated = isTruncated;
        Worktrees = worktrees ?? [];
        Reflog = reflog ?? [];
        IntegrationNames = integrationNames ?? [];
        RecordedSteps = recordedSteps ?? new Dictionary<string, RecordedStep>();
        RemoteReflog = remoteReflog ?? [];
        CurrentAuthor = currentAuthor;
    }

    // The name commits are made with in the repository, its user.name, or "" when none is set
    public string CurrentAuthor { get; }

    public DateTime TimeStamp { get; }
    public string Path { get; }
    internal IReadOnlyList<GitCommit> Commits { get; }
    public IReadOnlyList<GitBranch> Branches { get; }
    public IReadOnlyList<GitTag> Tags { get; }
    public GitStatus Status { get; }
    public MetaData MetaData { get; }
    public IReadOnlyList<GitStash> Stashes { get; }
    public bool IsTruncated { get; }

    // The worktrees of the repository. The uncommitted changes of the others are not read with the
    // repo but after it is shown (AugmentedService.GetUpdatedWorktreesRepoAsync).
    public IReadOnlyList<GitWorktree> Worktrees { get; }

    // The reflogs, which are local and expire, so often short or empty, and what they witnessed about
    // the branches of commits, see ReflogWitness
    public IReadOnlyList<ReflogEntry> Reflog { get; }
    public IReadOnlyDictionary<string, string> WitnessedBranchById =>
        witnessedBranchById ??= ReflogWitness.BranchByCommit(Reflog);
    IReadOnlyDictionary<string, string>? witnessedBranchById;
    public IReadOnlyDictionary<string, string> SourceByBranch =>
        sourceByBranch ??= ReflogWitness.SourceByBranch(Reflog);
    IReadOnlyDictionary<string, string>? sourceByBranch;

    // The names of the repo's own integration branches, as configured for it, see RepoConfig
    public IReadOnlyCollection<string> IntegrationNames { get; }

    // The last changes gmd made to branches that their reflogs alone would name wrong, see RepoConfig
    public IReadOnlyDictionary<string, RecordedStep> RecordedSteps { get; }

    // The reflogs of the remote branches whose local branches have commits not on them, which tell a
    // force push on origin from new commits on either side, see RemoteRewrites
    public IReadOnlyList<ReflogEntry> RemoteReflog { get; }

    public override string ToString() =>
        $"B:{Branches.Count}, C:{Commits.Count}, T:{Tags.Count}, S:{Status} @{TimeStamp.IsoMs()}";
}
