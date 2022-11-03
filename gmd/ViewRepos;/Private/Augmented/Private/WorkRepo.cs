namespace gmd.ViewRepos.Private.Augmented.Private;

using GitCommit = gmd.Utils.Git.Commit;
using GitBranch = gmd.Utils.Git.Branch;


class WorkRepo
{
    internal List<WorkCommit> Commits { get; } = new List<WorkCommit>();
    internal Dictionary<string, WorkCommit> CommitsById { get; } = new Dictionary<string, WorkCommit>();
    internal List<WorkBranch> Branches { get; } = new List<WorkBranch>();
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
    public List<Tag> Tags { get; } = new List<Tag>();
    public List<string> BranchTips { get; } = new List<string>();

    public bool IsCurrent { get; set; }
    public bool IsUncommitted { get; set; }
    public bool IsLocalOnly { get; set; }
    public bool IsRemoteOnly { get; set; }
    public bool IsPartialLogCommit { get; set; }
    public bool IsAmbiguous { get; set; }
    public bool IsAmbiguousTip { get; set; }

    public List<string> ParentIds { get; }
    public WorkCommit? FirstParent { get; internal set; }
    public WorkCommit? MergeParent { get; internal set; }

    public List<string> ChildIds { get; } = new List<string>();
    public List<WorkCommit> Children { get; } = new List<WorkCommit>();
    public List<WorkCommit> MergeChildren { get; } = new List<WorkCommit>();

    public List<WorkBranch> Branches { get; } = new List<WorkBranch>();
    public WorkBranch? Branch { get; internal set; }


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
        Sid = id.Substring(0, 6);
        Subject = subject;
        Message = message;
        Author = author;
        AuthorTime = authorTime;
        ParentIds = new List<string>(parentIds.AsEnumerable<string>());
    }

    internal void AddToBranchesIfNotExists(params WorkBranch[] branches)
    {
        foreach (var branch in branches)
        {
            if (Branches.Exists(b => b.Name == branch.Name))
            {
                continue;
            }
            Branches.Add(branch);
        }
    }
}

internal class WorkBranch
{
    // Git properties
    public string Name { get; }
    public string DisplayName { get; }
    public bool IsCurrent { get; }
    public bool IsRemote { get; }
    public bool IsDetached { get; }
    public int AheadCount { get; }
    public int BehindCount { get; }
    public bool IsRemoteMissing { get; }

    // Augmented properties
    public string RemoteName { get; set; } = "";
    public string LocalName { get; set; } = "";
    public string TipID { get; }
    public string BottomID { get; internal set; } = "";

    public bool IsGitBranch { get; set; }
    public bool IsAmbiguousBranch { get; set; }
    public bool IsSetAsParent { get; set; }
    public bool IsMainBranch { get; set; }
    public bool HasLocalOnly { get; set; }
    public bool HasRemoteOnly { get; set; }

    public string AmbiguousTipId { get; set; } = "";
    public List<string> AmbiguousBranchNames { get; } = new List<string>();
    public List<WorkBranch> AmbiguousBranches = new List<WorkBranch>();

    public WorkBranch(GitBranch b)
    {
        Name = b.Name;
        DisplayName = b.DisplayName;
        TipID = b.TipID;
        IsCurrent = b.IsCurrent;
        IsRemote = b.IsRemote;
        IsDetached = b.IsDetached;
        AheadCount = b.AheadCount;
        BehindCount = b.BehindCount;
        IsRemoteMissing = b.IsRemoteMissing;
        RemoteName = b.RemoteName;
        LocalName = "";
        BottomID = "";
    }

    public WorkBranch(string name, string displayName, string tipID)
    {
        Name = name;
        DisplayName = displayName;
        TipID = tipID;
    }
}

