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
        IReadOnlyDictionary<string, int>? worktreeChanges = null
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
        WorktreeChanges = worktreeChanges ?? new Dictionary<string, int>();
    }

    public DateTime TimeStamp { get; }
    public string Path { get; }
    internal IReadOnlyList<GitCommit> Commits { get; }
    public IReadOnlyList<GitBranch> Branches { get; }
    public IReadOnlyList<GitTag> Tags { get; }
    public GitStatus Status { get; }
    public MetaData MetaData { get; }
    public IReadOnlyList<GitStash> Stashes { get; }
    public bool IsTruncated { get; }

    // The worktrees of the repository, and the number of uncommitted changes in each of the
    // others, by path (a worktree not in the dictionary could not be read)
    public IReadOnlyList<GitWorktree> Worktrees { get; }
    public IReadOnlyDictionary<string, int> WorktreeChanges { get; }

    public override string ToString() =>
        $"B:{Branches.Count}, C:{Commits.Count}, T:{Tags.Count}, S:{Status} @{TimeStamp.IsoMs()}";
}
