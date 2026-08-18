namespace gmd.Server.Private.Augmented.Private;

// The rules CommitBranchService tries, in order, to determine which branch a commit belongs to.
// Each rule answers "is there enough evidence for this commit to belong to a branch", and the
// order they are tried in is the strength of that evidence, see DetermineCommitBranch.
interface ICommitBranchRules
{
    bool TryIsBranchSetByUser(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasOnlyOneBranch(WorkCommit commit, out WorkBranch? branch);
    bool TryIsLocalRemoteBranch(WorkCommit commit, out WorkBranch? branch);
    bool TryHasMainBranch(WorkCommit commit, out WorkBranch? branch);
    bool TryIsMergedDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryIsStrangeDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasOnlyOneChild(WorkCommit commit, out WorkBranch? branch);
    bool TryHasOneChildWithLikelyBranch(WorkCommit commit, out WorkBranch? branch);
    bool TryHasMultipleChildrenWithOneLikelyBranch(WorkCommit commit, out WorkBranch? branch);
    bool TrySameChildrenBranches(WorkCommit commit, out WorkBranch? branch);
    bool TryIsMergedBranchesToParent(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryIsChildAmbiguousCommit(WorkCommit commit, out WorkBranch? branch);
}

class CommitBranchRules : ICommitBranchRules
{
    readonly IBranchNameService branchNameService;

    public CommitBranchRules(IBranchNameService branchNameService)
    {
        this.branchNameService = branchNameService;
    }

    // Commit branch was set/determined by user,
    public bool TryIsBranchSetByUser(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        if (!gitRepo.MetaData.TryGetCommitBranch(commit.Sid, out var branchNiceName, out var isSetByUser))
        { // Commit has not a branch set by user
            return false;
        }
        // Log.Info($"Commit {commit.Sid} has branch set to {branchHumanName} (by user: {isSetByUser})");

        var branches = commit.Branches.Where(b => b.NiceName == branchNiceName);
        if (!branches.Any())
        { // Branch not found by obvious commit branches, create a new branch
            commit.IsBranchSetByUser = isSetByUser;
            branch = BranchFactory.AddNamedBranch(repo, commit, branchNiceName);
            return true;
        }

        // Prefer remote branches over local branches
        var remote = branches.FirstOrDefault(b => b.IsRemote);
        if (remote != null)
        {
            commit.IsBranchSetByUser = isSetByUser;
            branch = remote;
            return BranchAmbiguity.TrySetBranch(repo, commit, branch);
        }

        // Just use the first branch with that human name
        commit.IsBranchSetByUser = isSetByUser;
        branch = branches.First();
        return BranchAmbiguity.TrySetBranch(repo, commit, branch);
    }

    // Commit only has one branch, use that
    public bool TryHasOnlyOneBranch(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 1)
        { // Commit only has one branch, use that
            branch = commit.Branches[0];
            return true;
        }

        branch = null;
        return false;
    }

    // Commit has only local and its remote branch, prefer remote remote branch
    public bool TryIsLocalRemoteBranch(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 2)
        {
            if (commit.Branches[0].IsRemote && commit.Branches[0].Name == commit.Branches[1].RemoteName)
            { // remote and local branch, prefer remote
                branch = commit.Branches[0];
                return true;
            }
            if (!commit.Branches[0].IsRemote && commit.Branches[0].RemoteName == commit.Branches[1].Name)
            { // local and remote branch, prefer remote
                branch = commit.Branches[1];
                return true;
            }
        }

        branch = null;
        return false;
    }

    // Commit, has several possible branches, and one is in the priority list, e.g. main, master, ...
    public bool TryHasMainBranch(WorkCommit c, out WorkBranch? branch)
    {
        branch = null;
        if (c.Branches.Count < 1)
            return false;

        // Check if commit has one of the main branches
        foreach (var name in WellKnownBranches.MainNamePriority)
        {
            branch = c.Branches.Find(b => b.Name == name);
            if (branch != null)
            {
                return true;
            }
        }

        return false;
    }

    // Commit has no branches and no children, but has a merge child.
    // The commit is a tip of a deleted branch. It might be a deleted remote branch.
    // Lets try determine branch name based on merge child's subject
    // or use a generic branch name based on commit id
    public bool TryIsMergedDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 0 && commit.FirstChildren.Count == 0 && commit.MergeChildren.Count == 1)
        { // Commit has no branch and no children, but has a merge child. I.e. must be a
            // deleted branch that was merged into some other branch.
            // Trying to use parsed branch name from the merge children subjects e.g. like:
            // "Merge branch 'branch-name' into develop"
            if (branchNameService.TryGetBranchName(commit.Id, out var name))
            { // Managed to parse a branch-name
                var mergeChild = commit.MergeChildren[0];

                if (branchNameService.IsPullMerge(mergeChild) && mergeChild.Branch!.NiceName == name)
                { // The branch is a pull name and has same name as the branch is was merged into
                    // The merge child is a pull merge, so this commit is on a "dead" branch part,
                    // which used to be the local branch of the pull merge commit.
                    // Lets connect this branch with the actual branch.
                    var pullMergeBranch = mergeChild.Branch;
                    branch = BranchFactory.AddPullMergeBranch(repo, commit, name, pullMergeBranch!);
                    pullMergeBranch!.PullMergeChildBranches.TryAdd(branch);
                    return true;
                }

                branch = BranchFactory.AddNamedBranch(repo, commit, name);
                return true;
            }

            // could not parse a name from any of the merge children, use id named branch
            branch = BranchFactory.AddNamedBranch(repo, commit);
            return true;
        }

        branch = null;
        return false;
    }

    // Commit has no branches and no children, but may have merge children.
    // The commit is a tip of a deleted remote branch.
    // Lets try determine branch name based on merge child's subject
    // or use a generic branch name based on commit id
    public bool TryIsStrangeDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 0 && commit.FirstChildren.Count == 0)
        { // Commit has no branch, and no children, must be a deleted branch tip unusual branch
            // Trying to use parsed branch name from one of the merge children subjects e.g. Merge branch 'a' into develop

            if (branchNameService.TryGetBranchName(commit.Id, out var name))
            { // Managed to parse a branch name
                branch = BranchFactory.AddNamedBranch(repo, commit, name);
                return true;
            }

            // could not parse a name from any of the merge children, use id named branch
            branch = BranchFactory.AddNamedBranch(repo, commit);
            return true;
        }

        branch = null;
        return false;
    }

    // A branch name could be parsed form the commit subject or a child subject.
    // The commit will be set to that branch and also if above (first child) commits have
    // ambiguous branches, the will be reset to same branch as well. This will 'repair' branch
    // when a parsable commit subjects are encountered.
    public bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;

        if (!branchNameService.TryGetBranchName(commit.Id, out var name))
            return false;

        // A branch name could be parsed form the commit subject or a merge child subject.
        branch = TryGetBranchFromName(commit, name);
        if (branch == null)
        { // Found no matching branch
            return false;
        }

        return BranchAmbiguity.TrySetBranch(repo, commit, branch);
    }

    static WorkBranch? TryGetBranchFromName(WorkCommit commit, string name)
    {
        // Try find a live git branch with the remoteName or local name
        var remoteName = $"origin/{name}";
        var branch = commit.Branches.FirstOrDefault(b => b.Name == remoteName);
        if (branch != null)
        {
            return branch;
        }
        branch = commit.Branches.FirstOrDefault(b => b.Name == name);
        if (branch != null)
        {
            return branch;
        }

        // Try find a branch with the human name
        branch = commit.Branches.Find(b => b.NiceName == name);
        if (branch != null)
        {
            return branch;
        }

        // Pull requests names include repository as prefix, try check if branch ends with name
        branch = commit.Branches.Find(b => name.EndsWith(b.NiceName));
        if (branch != null)
        {
            return branch;
        }

        return branch;
    }

    // Commit has one child commit reuse that child commit branch
    public bool TryHasOnlyOneChild(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.FirstChildren.Count == 1)
        { // Commit has only one child, ensure commit has same possible branches
            var child = commit.FirstChildren[0];
            if (commit.Branches.Count != child.Branches.Count)
            { // Number of branches have changed
                branch = null;
                return false;
            }

            for (int i = 0; i < commit.Branches.Count; i++)
            {
                if (commit.Branches[i].Name != child.Branches[i].Name)
                { // Some branch has changed
                    branch = null;
                    return false;
                }
            }

            // Commit has one child and same branches, use that child commit branch
            branch = child.Branch;
            commit.IsAmbiguous = child.IsAmbiguous;
            commit.IsLikely = child.IsLikely;
            return true;
        }

        branch = null;
        return false;
    }

    // Commit multiple possible git branches but has one child, which has a likely known branch, use same branch
    public bool TryHasOneChildWithLikelyBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.FirstChildren.Count == 1 && c.FirstChildren[0].IsLikely)
        { // Commit has one child, which has a likely known branch, use same branch
            branch = c.FirstChildren[0].Branch;
            c.IsAmbiguous = c.FirstChildren[0].IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
    }

    // Commit multiple possible git branches but has a child, which has a likely known branch, use same branch
    public bool TryHasMultipleChildrenWithOneLikelyBranch(WorkCommit c, out WorkBranch? branch)
    {
        branch = null;
        if (c.FirstChildren.Count(c => c.IsLikely) != 1)
        {
            return false;
        }

        // commit has only one child with a likely branch
        var child = c.FirstChildren.First(c => c.IsLikely);
        c.IsAmbiguous = child.IsAmbiguous;

        if (child.Branch!.IsRemote)
        { // The branch is remote, we prefer that
            branch = child.Branch;
            return true;
        }

        if (child.Branch!.RemoteName != "")
        { // The child branch has a corresponding remote branch, lets try to use that
            var remoteBranch = c.Branches.FirstOrDefault(b => b.Name == child.Branch!.RemoteName);
            if (remoteBranch != null)
            { // The child branch was local and the corresponding remote is also possible,
                branch = remoteBranch;
                return true;
            }
        }

        branch = child.Branch;
        c.IsAmbiguous = child.IsAmbiguous;
        return true;
    }

    // For e.g. pull merges, a commit can have two children with same logical branch
    public bool TrySameChildrenBranches(WorkCommit commit, out WorkBranch? branch)
    {
        if (
            commit.Branches.Count == 2
            && commit.FirstChildren.Count == 2
            && commit.FirstChildren[0].Branch!.PrimaryName == commit.FirstChildren[1].Branch!.PrimaryName
        )
        { // Commit has 2 children with same branch use that
            if (
                commit.FirstChildren[0].Branch!.PullMergeParentBranch != null
                && commit.FirstChildren[0].Branch!.PullMergeParentBranch!.Name
                    == commit.FirstChildren[1].Branch!.LocalName
            )
            { // child branch 0 is a pull merge of child 1 local of remote branch 1, prefer parent 1
                branch = commit.FirstChildren[1].Branch;
                commit.IsAmbiguous = commit.FirstChildren[1].IsAmbiguous;
                return true;
            }
            if (commit.FirstChildren[0].Branch!.PullMergeParentBranch == commit.FirstChildren[1].Branch)
            { // child branch 0 is a pull merge of child branch 1, prefer parent 1
                branch = commit.FirstChildren[1].Branch;
                commit.IsAmbiguous = commit.FirstChildren[1].IsAmbiguous;
                return true;
            }

            branch = commit.FirstChildren[0].Branch;
            commit.IsAmbiguous = commit.FirstChildren[0].IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
    }

    // Checks if a commit with 2 children and if the one child branch is merged into the
    // other child branch. E.g. like a pull request or feature branch
    public bool TryIsMergedBranchesToParent(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        if (commit.FirstChildren.Count == 2) // Could support more children as well
        {
            var b1 = commit.FirstChildren[0].Branch!;
            var b1MergeChildren = repo.CommitsById[b1.TipID].MergeChildren;
            var b1Bottom = repo.CommitsById[b1.BottomID];
            var b2 = commit.FirstChildren[1].Branch!;
            var b2MergeChildren = repo.CommitsById[b2.TipID].MergeChildren;
            var b2Bottom = repo.CommitsById[b2.BottomID];

            if (
                !b2.IsGitBranch
                && b2Bottom.FirstParent == commit
                && b2MergeChildren.Count == 1
                && b2MergeChildren[0].Branch == b1
            )
            {
                branch = b1;
                return true;
            }
            if (
                !b1.IsGitBranch
                && b1Bottom.FirstParent == commit
                && b1MergeChildren.Count == 1
                && b1MergeChildren[0].Branch == b2
            )
            {
                branch = b2;
                return true;
            }
        }

        return false;
    }

    // If one of the commit children is a an ambiguous commit, reuse same branch
    public bool TryIsChildAmbiguousCommit(WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var ambiguousChild = commit.FirstChildren.FirstOrDefault(c => c.IsAmbiguous);
        if (ambiguousChild == null)
        { // No ambiguous child
            return false;
        }

        branch = ambiguousChild.Branch!;

        // If more ambiguous children, merge in their sub branches as well
        commit
            .FirstChildren.Where(c => c.IsAmbiguous && c != ambiguousChild)
            .ForEach(c => c.Branch!.AmbiguousBranches.ForEach(b => commit.Branches.TryAdd(b)));

        commit.IsAmbiguous = true;
        return true;
    }
}
