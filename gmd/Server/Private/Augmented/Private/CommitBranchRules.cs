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
    bool TryIsWitnessed(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch);
    bool TryIsPublishedTipOfLocalBranches(WorkCommit commit, out WorkBranch? branch);
    bool TryIsMergedDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryIsStrangeDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasOnlyOneChild(WorkCommit commit, out WorkBranch? branch);
    bool TryHasSeniorBranch(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasOnlyOneName(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
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
        if (!gitRepo.MetaData.TryGetCommitBranch(commit.Id, out var branchNiceName, out var isSetByUser))
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

    // The reflog witnessed which branch the commit was made on, or that a branch was started from it
    // on another branch (see ReflogWitness), which is just what the user means by a commit's branch.
    // Taken only among the branches the commit can be on: a commit a reset moved off the branch it
    // was made on is not on it any more, and a fact never makes up a branch, as a choice in the
    // metadata does. Not above the trunk either, it comes after the main branch rule: a feature
    // fast-forwarded into main would take main's commits. And the commit is not marked likely, since
    // where a commit was made says nothing about its parent, e.g. a branch point below a branch's
    // first commit, which is what the likely-child rules would read it as.
    public bool TryIsWitnessed(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        if (!gitRepo.WitnessedBranchById.TryGetValue(commit.Id, out var name))
            return false;

        branch = RemoteFirst(commit.Branches.Where(b => b.NiceName == name));
        return branch != null && BranchAmbiguity.TrySetBranch(repo, commit, branch, isLikely: false);
    }

    // The commit is the tip of a published branch, and every other candidate is a local branch only,
    // e.g. a feature started at dev's tip with 'git checkout -b', outside gmd, so nothing recorded
    // where it started. The tip is the last commit made on the published branch, and a local branch
    // pointing at it or passing through it came later. Published means the branch has a remote, so
    // a local tip ahead of it counts too.
    // Not the other way round: a local branch left pointing at an older commit of a published branch
    // did not make that commit, and must not take the published branch's history from there down.
    public bool TryIsPublishedTipOfLocalBranches(WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var localOnly = commit.Branches.Where(IsLocalOnly).ToList();
        var published = commit.Branches.Where(b => !IsLocalOnly(b)).ToList();
        if (localOnly.Count == 0 || published.Count == 0)
            return false;

        if (published.Any(b => !b.IsGitBranch) || published.Select(b => b.PrimaryName).Distinct().Count() != 1)
        { // Only one published branch, the local and remote branch of it
            return false;
        }
        if (!published.Any(b => b.TipID == commit.Id))
        { // The published branch passes through, the commit is not its tip
            return false;
        }

        branch = published.FirstOrDefault(b => b.IsRemote) ?? published.First();
        return true;
    }

    // A local git branch that has no remote branch, i.e. one that was never pushed
    static bool IsLocalOnly(WorkBranch b) => b.IsGitBranch && !b.IsRemote && b.RemoteName == "";

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
        branch = RemoteFirst(commit.Branches.Where(b => b.NiceName == name));
        if (branch != null)
        {
            return branch;
        }

        // Pull requests names include the owner as prefix, e.g. 'owner/dev', try a branch the name
        // ends with, as a whole part of it, so that 'hotfix-dev' is not taken for 'dev'
        return RemoteFirst(commit.Branches.Where(b => name.EndsWith("/" + b.NiceName)));
    }

    // A nice name is shared by a local branch and its remote branch, and the remote branch is the
    // one a commit belongs to whenever it is a candidate, since it is the primary of the two. Taking
    // the local branch there made it the parent of its own parent: the remote branch, then owning
    // nothing, got the local branch as its parent, a cycle.
    static WorkBranch? RemoteFirst(IEnumerable<WorkBranch> branches) =>
        branches.OrderBy(b => b.IsRemote ? 0 : 1).FirstOrDefault();

    // Commit has one child commit reuse that child commit branch
    public bool TryHasOnlyOneChild(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.FirstChildren.Count == 1)
        { // Commit has only one child, ensure commit has same possible branches
            var child = commit.FirstChildren[0];
            if (commit.Branches.Count != child.Branches.Count || !commit.Branches.All(child.Branches.Contains))
            { // Some branch has changed, the order of them does not matter
                branch = null;
                return false;
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

    // Commit is where branches meet, and one of them is senior to the others, i.e. the branch they
    // were started from, which goes on below. An integration branch by name (develop, dev) is senior.
    // Failing that, so is a branch that clearly more other branches were merged into: at least two,
    // and at least twice as many as any other, not counting the trunk merged in to bring a branch up
    // to date. Merge subjects alone cannot tell which branch goes on: a branch merged into another
    // looks the same whether it is a feature merged back into dev or dev merged into a feature to
    // bring it up to date, which is why one merged branch is never enough. Only a clear winner decides.
    public bool TryHasSeniorBranch(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var groups = commit
            .Branches.GroupBy(b => b.PrimaryName)
            .Select(g => (branches: g, primary: repo.Branches[g.Key]))
            .ToList();
        if (groups.Count < 2)
            return false;

        var integration = groups.Where(g => WellKnownBranches.IsIntegrationName(g.primary.NiceName)).ToList();
        if (integration.Count == 1)
        {
            branch = RemoteFirst(integration[0].branches);
            return true;
        }
        if (integration.Count > 1)
            return false;

        var byMerged = groups.OrderByDescending(g => g.primary.MergedFromNames.Count).ToList();
        var most = byMerged[0].primary.MergedFromNames.Count;
        var next = byMerged[1].primary.MergedFromNames.Count;
        if (most < 2 || most < 2 * next)
            return false;

        branch = RemoteFirst(byMerged[0].branches);
        return true;
    }

    // Commit is where branches of one name meet, e.g. a deleted branch recovered from the subject of
    // each merge of it, all named dev. There is nothing for the user to choose between, so the one
    // most branches were merged into goes on, or failing that the first.
    public bool TryHasOnlyOneName(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var groups = commit
            .Branches.GroupBy(b => b.PrimaryName)
            .Select(g => (branches: g, primary: repo.Branches[g.Key]))
            .ToList();
        if (groups.Count < 2 || groups.Select(g => g.primary.NiceName).Distinct().Count() != 1)
            return false;

        branch = RemoteFirst(groups.OrderByDescending(g => g.primary.MergedFromNames.Count).First().branches);
        return true;
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
