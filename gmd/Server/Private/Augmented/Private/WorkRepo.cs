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
    public Dictionary<string, WorkBranch> Branches { get; } = new Dictionary<string, WorkBranch>();
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

    public List<string> AllChildIds { get; } = new List<string>();             // Id of all children of this commit
    public List<string> FirstChildIds { get; } = new List<string>();           // Child id, which have this commit as first parent
    public List<string> MergeChildIds { get; } = new List<string>();           // Child id, which have this commit as merge parent

    public List<WorkCommit> FirstChildren { get; } = new List<WorkCommit>();   // Children which have this commit as first parent
    public List<WorkCommit> MergeChildren { get; } = new List<WorkCommit>();   // Chilren, which have this commit as merge parent

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
    public bool IsRemote { get; }
    public bool IsDetached { get; }
    public bool IsRemoteMissing { get; }
    public bool IsCurrent { get; }

    // Augmented properties
    public string PrimaryName { get; set; }  // The name of main/primary branch related branch (remote if both local and remote) 
    public string PrimaryBaseName { get; set; } = "";  // a name based on first commit and parent commit
    public string NiceName { get; set; } = "";         // Nice name (might not be unique)
    public string NiceNameUnique { get; set; } = ""; // Unique nice name (with branch number if needed)
    public string RemoteName { get; set; } = "";  // A local branch's remote name
    public string LocalName { get; set; } = "";   // A remote branch's local name
    public string TipID { get; }                         // First commit id
    public string BottomID { get; internal set; } = "";  // Last commit id

    public WorkBranch? ParentBranch { get; set; }       // Parent branch (remote if local)
    public WorkBranch? PullMergeParentBranch { get; set; }    // For pull merge branches, their parent branch

    public bool IsLocalCurrent { get; set; }  // True if local branch corresponding to this remote is current
    public bool IsGitBranch { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsAmbiguousBranch { get; set; }
    public bool IsMainBranch { get; set; }
    public bool HasLocalOnly { get; set; }
    public bool HasRemoteOnly { get; set; }

    public string AmbiguousTipId { get; set; } = ""; // Set if this branch has ambigous last part
    public bool IsCircularAncestors { get; internal set; }

    public List<WorkBranch> RelatedBranches = new List<WorkBranch>();
    public List<WorkBranch> AmbiguousBranches = new List<WorkBranch>();
    public List<WorkBranch> PullMergeChildBranches = new List<WorkBranch>();
    public List<WorkBranch> Ancestors = new List<WorkBranch>();

    // Called when creating a WorkBranch based on a git branch
    public WorkBranch(GitBranch b)
    {
        Name = b.Name;
        PrimaryName = "";                // Will be set later
        NiceName = b.Name.TrimPrefix("origin/");
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
    public WorkBranch(string name, string primaryName, string niceName, string tipID)
    {
        Name = name;
        PrimaryName = primaryName;
        IsPrimary = name == primaryName;
        NiceName = niceName;
        TipID = tipID;
        BottomID = tipID;
    }

    public override string ToString() => IsRemote ? $"{Name}<-{LocalName}" : $"{Name}->{RemoteName}";
}

