namespace gmd.Server.Private.Augmented.Private;

using GitCommit = gmd.Git.Commit;
using GitBranch = gmd.Git.Branch;


class WorkRepo
{
    public DateTime TimeStamp { get; }
    public string Path { get; }
    public Status Status { get; }
    internal List<WorkCommit> Commits { get; } = new List<WorkCommit>();
    internal Dictionary<string, WorkCommit> CommitsById { get; } = new Dictionary<string, WorkCommit>();
    internal List<WorkBranch> Branches { get; } = new List<WorkBranch>();
    internal Dictionary<string, WorkBranch> BranchByName { get; } = new Dictionary<string, WorkBranch>();
    internal List<Tag> Tags { get; } = new List<Tag>();
    internal Dictionary<string, Tag> TagById { get; } = new Dictionary<string, Tag>();
    internal List<Stash> Stashes { get; } = new List<Stash>();
    internal Dictionary<string, Stash> StashById { get; } = new Dictionary<string, Stash>();

    public WorkRepo(
        DateTime timeStamp,
        string path,
        Status status)
    {
        TimeStamp = timeStamp;
        Path = path;
        Status = status;
    }

    public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, S:{Status}";
}

internal class WorkCommit
{
    // Git properties
    public string Id { get; }
    public string Sid { get; }
    public string Subject { get; }
    public string Message { get; }
    public string Author { get; }
    public DateTime AuthorTime { get; }

    // Augmented properties
    public int GitIndex { get; internal set; }
    public List<Tag> Tags { get; } = new List<Tag>();
    public List<string> BranchTips { get; } = new List<string>();

    public bool IsCurrent { get; set; }
    public bool IsDetached { get; set; }
    public bool IsUncommitted { get; set; }
    public bool IsConflicted { get; set; }
    public bool IsAhead { get; set; }
    public bool IsBehind { get; set; }
    public bool IsPartialLogCommit { get; set; }
    public bool IsAmbiguous { get; set; }
    public bool IsAmbiguousTip { get; set; }
    public bool IsBranchSetByUser { get; set; }

    public List<string> ParentIds { get; }
    public WorkCommit? FirstParent { get; internal set; }
    public WorkCommit? MergeParent { get; internal set; }

    public List<string> ChildIds { get; } = new List<string>();
    public List<WorkCommit> Children { get; } = new List<WorkCommit>();
    public List<WorkCommit> MergeChildren { get; } = new List<WorkCommit>();

    public List<WorkBranch> Branches { get; } = new List<WorkBranch>();
    public WorkBranch? Branch { get; internal set; }

    public bool IsLikely { get; internal set; }

    public WorkCommit(GitCommit c)
    {
        Id = c.Id;
        Sid = c.Sid;
        Subject = c.Subject;
        Message = c.Message;
        Author = c.Author;
        AuthorTime = c.AuthorTime;
        ParentIds = new List<string>(c.ParentIds.AsEnumerable<string>());
    }

    public WorkCommit(string id, string subject, string message, string author,
        DateTime authorTime, string[] parentIds)
    {
        Id = id;
        Sid = id.Sid();
        Subject = subject;
        Message = message;
        Author = author;
        AuthorTime = authorTime;
        ParentIds = new List<string>(parentIds.AsEnumerable<string>());
    }

    public override string ToString() => $"#{GitIndex} {Sid} {Subject} ({Branch?.Name ?? "<n/a>"})";
}

internal class WorkBranch
{
    // Git properties
    public string Name { get; }
    public string CommonName { get; }
    public bool IsRemote { get; }
    public bool IsDetached { get; }
    public int AheadCount { get; }
    public int BehindCount { get; }
    public bool IsRemoteMissing { get; }
    public bool IsCurrent { get; }

    // Augmented properties
    public bool IsLocalCurrent { get; set; }
    public string DisplayName { get; set; } = "";
    public string RemoteName { get; set; } = "";
    public string LocalName { get; set; } = "";
    public string CommonBaseName { get; set; } = "";
    public string TipID { get; }
    public string BottomID { get; internal set; } = "";

    public WorkBranch? ParentBranch { get; internal set; }
    public WorkBranch? PullMergeBranch { get; set; }

    public bool IsGitBranch { get; set; }
    public bool IsAmbiguousBranch { get; set; }
    public bool IsSetAsParent { get; set; }
    public bool IsMainBranch { get; set; }
    public bool HasLocalOnly { get; set; }
    public bool HasRemoteOnly { get; set; }

    public string AmbiguousTipId { get; set; } = "";
    public string PullRequestParent { get; internal set; } = "";

    public List<WorkBranch> AmbiguousBranches = new List<WorkBranch>();
    public List<WorkBranch> PullMergeBranches = new List<WorkBranch>();

    // Called when creating a WorkBranch based on a git branch
    public WorkBranch(GitBranch b)
    {
        Name = b.Name;
        CommonName = b.CommonName;
        DisplayName = b.CommonName;
        TipID = b.TipID;
        IsGitBranch = true;
        IsCurrent = b.IsCurrent;
        IsRemote = b.IsRemote;
        IsDetached = b.IsDetached;
        AheadCount = b.AheadCount;
        BehindCount = b.BehindCount;
        IsRemoteMissing = b.IsRemoteMissing;
        RemoteName = b.RemoteName;
        LocalName = "";
        BottomID = b.TipID;
    }

    // Called when creating a branched based on a name, usually from a deleted branch
    public WorkBranch(string name, string commonName, string displayName, string tipID)
    {
        Name = name;
        CommonName = commonName;
        DisplayName = displayName;
        TipID = tipID;
        BottomID = tipID;
    }

    public override string ToString() => IsRemote ? $"{Name}<-{LocalName}" : $"{Name}->{RemoteName}";
}

