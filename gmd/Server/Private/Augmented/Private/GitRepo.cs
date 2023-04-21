

using GitCommit = gmd.Git.Commit;
using GitBranch = gmd.Git.Branch;
using GitTag = gmd.Git.Tag;
using GitStatus = gmd.Git.Status;
using GitStash = gmd.Git.Stash;

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
        IReadOnlyList<GitStash> stashes)
    {
        TimeStamp = timeStamp;
        Path = path;
        Commits = commits;
        Branches = branches;
        Tags = tags;
        Status = status;
        MetaData = metaData;
        Stashes = stashes;
    }

    public DateTime TimeStamp { get; }
    public string Path { get; }
    internal IReadOnlyList<GitCommit> Commits { get; }
    public IReadOnlyList<GitBranch> Branches { get; }
    public IReadOnlyList<GitTag> Tags { get; }
    public GitStatus Status { get; }
    public MetaData MetaData { get; }
    public IReadOnlyList<GitStash> Stashes { get; }

    public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, T: {Tags.Count}, S:{Status}";
}

