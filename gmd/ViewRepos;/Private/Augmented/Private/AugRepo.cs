namespace gmd.ViewRepos.Private.Augmented.Private;

using GitCommit = gmd.Utils.Git.Commit;
using GitBranch = gmd.Utils.Git.Branch;

class AugRepo
{
    internal List<AugCommit> Commits { get; } = new List<AugCommit>();
    internal Dictionary<string, AugCommit> CommitsById { get; } = new Dictionary<string, AugCommit>();
    internal List<AugBranch> Branches { get; } = new List<AugBranch>();
}

internal class AugCommit
{
    // Git properties
    public string Id { get; }
    public string Sid { get; }
    public string Subject { get; }
    public string Message { get; }
    public string Author { get; }
    public DateTime AuthorTime { get; }
    public List<string> ParentIds { get; }

    // Augmented properties
    public string BranchName { get; set; } = "";
    public List<string> ChildIds { get; } = new List<string>();
    public List<Tag> Tags { get; } = new List<Tag>();
    public List<string> BranchTips { get; } = new List<string>();
    public bool IsCurrent { get; set; }
    public bool IsUncommitted { get; set; }
    public bool IsLocalOnly { get; set; }
    public bool IsRemoteOnly { get; set; }
    public bool IsPartialLogCommit { get; set; }
    public bool IsAmbiguous { get; set; }
    public bool IsAmbiguousTip { get; set; }

    public AugCommit(GitCommit c)
    {
        Id = c.Id;
        Sid = c.Sid;
        Subject = c.Subject;
        Message = c.Message;
        Author = c.Author;
        AuthorTime = c.AuthorTime;
        ParentIds = new List<string>(c.ParentIds.AsEnumerable<string>());
    }

    public AugCommit(string id, string subject, string message, string author,
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
}

internal class AugBranch
{
    // Git properties
    public string Name { get; }
    public string DisplayName { get; }
    public string TipID { get; }
    public bool IsCurrent { get; }
    public bool IsRemote { get; }
    public bool IsDetached { get; }
    public int AheadCount { get; }
    public int BehindCount { get; }
    public bool IsRemoteMissing { get; }

    // Augmented properties
    public string RemoteName { get; set; }
    public string LocalName { get; set; }
    public bool IsGitBranch { get; set; }
    public bool IsAmbiguousBranch { get; set; }
    public bool IsSetAsParent { get; set; }
    public bool IsMainBranch { get; set; }
    public bool HasLocalOnly { get; set; }
    public bool HasRemoteOnly { get; set; }

    public string AmbiguousTipId { get; set; } = "";
    public List<string> AmbiguousBranchNames { get; } = new List<string>();


    public AugBranch(GitBranch b)
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
    }
}

