namespace gmd.Server.Private.Augmented.Private;

using GitCommit = gmd.Git.Commit;
using GitBranch = gmd.Git.Branch;

// Read/Write repo used by the AugmentedService while processing and augmenting a git repo 
class WorkRepo
{
    public DateTime TimeStamp { get; }
    public string Path { get; }
    public Status Status { get; }
    public List<WorkCommit> Commits { get; } = new List<WorkCommit>();
    public Dictionary<string, WorkCommit> CommitsById { get; } = new Dictionary<string, WorkCommit>();
    public List<WorkBranch> Branches { get; } = new List<WorkBranch>();
    public Dictionary<string, WorkBranch> BranchByName { get; } = new Dictionary<string, WorkBranch>();
    public List<Tag> Tags { get; } = new List<Tag>();
    public Dictionary<string, Tag> TagById { get; } = new Dictionary<string, Tag>();
    public List<Stash> Stashes { get; } = new List<Stash>();
    public Dictionary<string, Stash> StashById { get; } = new Dictionary<string, Stash>();

    public WorkRepo(DateTime timeStamp, string path, Status status)
    {
        TimeStamp = timeStamp;
        Path = path;
        Status = status;
    }

    public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, S:{Status}";
}

// Read/Write repo used by the AugmentedService while processing and augmenting a git repo 
class WorkCommit
{
    // Git properties
    public string Id { get; }
    public string Sid { get; }
    public string Subject { get; } // First line of message
    public string Message { get; } // Full message (including subject)
    public string Author { get; }
    public DateTime AuthorTime { get; }

    // Augmented properties
    public int GitIndex { get; set; }  // Index in git log (only used for tracing)
    public List<Tag> Tags { get; } = new List<Tag>();
    public List<string> BranchTips { get; } = new List<string>();

    public bool IsCurrent { get; set; }
    public bool IsDetached { get; set; }
    public bool IsTruncatedLogCommit { get; set; }  // Virtual commit indicating truncated large log
    public bool IsAmbiguous { get; set; }
    public bool IsAmbiguousTip { get; set; }
    public bool IsBranchSetByUser { get; set; }

    public List<string> ParentIds { get; }
    public WorkCommit? FirstParent { get; set; }
    public WorkCommit? MergeParent { get; set; }

    public List<string> ChildIds { get; } = new List<string>();
    public List<WorkCommit> Children { get; } = new List<WorkCommit>();
    public List<WorkCommit> MergeChildren { get; } = new List<WorkCommit>();

    public List<WorkBranch> Branches { get; } = new List<WorkBranch>();
    public WorkBranch? Branch { get; set; }

    public bool IsLikely { get; set; }

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

// Read/Write repo used by the AugmentedService while processing and augmenting a git repo 
internal class WorkBranch
{
    // Git properties
    public string Name { get; }
    public string CommonName { get; } // Common name for local and remote branches (name without remote prefix)
    public bool IsRemote { get; }
    public bool IsDetached { get; }
    public bool IsRemoteMissing { get; }
    public bool IsCurrent { get; }

    // Augmented properties
    public bool IsLocalCurrent { get; set; }
    public string DisplayName { get; set; } = "";
    public string RemoteName { get; set; } = "";  // A local branch's remote name
    public string LocalName { get; set; } = "";   // A remote branch's local name
    public string CommonBaseName { get; set; } = "";  // a name based on first commit and parent commit
    public string TipID { get; }                         // First commit id
    public string BottomID { get; internal set; } = "";  // Last commit id

    public WorkBranch? ParentBranch { get; set; }       // Parent branch (remote if local)
    public WorkBranch? PullMergeParentBranch { get; set; }    // For pull merge branches, the main parent branch

    public bool IsGitBranch { get; set; }
    public bool IsAmbiguousBranch { get; set; }
    public bool IsSetAsParent { get; set; }
    public bool IsMainBranch { get; set; }
    public bool HasLocalOnly { get; set; }
    public bool HasRemoteOnly { get; set; }

    public string AmbiguousTipId { get; set; } = ""; // Set if this branch has ambigous last part

    public List<WorkBranch> AmbiguousBranches = new List<WorkBranch>();
    public List<WorkBranch> PullMergeChildBranches = new List<WorkBranch>();

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
        RemoteName = b.RemoteName;
        LocalName = "";                  // Will be set later
        BottomID = b.TipID;              // Will be adjusted later
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

