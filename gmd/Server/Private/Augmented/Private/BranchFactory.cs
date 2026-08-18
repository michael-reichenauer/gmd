namespace gmd.Server.Private.Augmented.Private;

// Creates the branches that git no longer has, i.e. deleted branches recovered from a merge subject
// and the virtual branch that stands in for the unread history of a truncated log. A created branch
// is registered in the repo and in the related branches of its primary branch.
static class BranchFactory
{
    public static WorkBranch AddPullMergeBranch(
        WorkRepo repo,
        WorkCommit c,
        string name,
        WorkBranch pullMergeParentBranch
    )
    {
        var branchName = name != "" ? $"{name}:{c.Sid}" : $"branch:{c.Sid}";
        var humanName = name != "" ? name : $"branch@{c.Sid}";
        var branch = new WorkBranch(
            name: branchName,
            primaryName: pullMergeParentBranch.PrimaryName,
            niceName: humanName,
            tipID: c.Id
        )
        {
            PullMergeParentBranch = pullMergeParentBranch,
        };

        repo.Branches[branch.Name] = branch;
        repo.Branches[branch.PrimaryName].RelatedBranches.Add(branch);
        return branch;
    }

    public static WorkBranch AddTruncatedBranch(WorkRepo repo)
    {
        var branchName = WellKnownBranches.TruncatedName;
        var branch = new WorkBranch(
            name: branchName,
            primaryName: branchName,
            niceName: branchName,
            tipID: gmd.Server.Repo.TruncatedLogCommitId
        );

        repo.Branches[branch.Name] = branch;
        repo.Branches[branch.PrimaryName].RelatedBranches.Add(branch);
        return branch;
    }

    public static WorkBranch AddNamedBranch(WorkRepo repo, WorkCommit c, string name = "")
    {
        var branchName = name != "" ? $"{name}:{c.Sid}" : $"branch:{c.Sid}";
        var niceName = name != "" ? name : "branch";
        var branch = new WorkBranch(name: branchName, primaryName: branchName, niceName: niceName, tipID: c.Id);

        repo.Branches[branch.Name] = branch;
        repo.Branches[branch.PrimaryName].RelatedBranches.Add(branch);
        return branch;
    }
}
