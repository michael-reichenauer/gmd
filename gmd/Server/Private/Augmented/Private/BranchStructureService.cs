namespace gmd.Server.Private.Augmented.Private;

interface IBranchStructureService
{
    void DetermineCommitBranches(WorkRepo repo, GitRepo gitRepo);
}


class BranchStructureService : IBranchStructureService
{
    readonly string[] MainBranchNamePriority = new string[] { "origin/main", "main", "origin/master", "master", "origin/trunk", "trunk" };
    const string truncatedBranchName = "<truncated-branch>";  // Name of virtual branch in case of truncated repo log

    readonly IBranchNameService branchNameService;


    public BranchStructureService(IBranchNameService branchNameService)
    {
        this.branchNameService = branchNameService;
    }


    public void DetermineCommitBranches(WorkRepo repo, GitRepo gitRepo)
    {
        // Start be setting branch tips on tip commits, this will be starting point for determining branches
        SetGitBranchTipsOnCommits(repo);

        // Set parents and children on commits to be able to traverse the commit graph easier
        SetCommitParentsAndChildren(repo);

        // Not iterate all commits from the lates (top of log) and down to the first commit
        // If multiple possible branchea exist for a commit, try to determine the most likely branch
        DetermineAllCommitsBranches(repo, gitRepo);

        // Determine parent/child branch relationships, where a child branch is branch out of a parent
        DetermineBranchHierarchy(repo);

        // Determine the ancestors of each branch (i.e. parents, grandparents, etc.)
        DetermineAncestors(repo);
    }


    // Set branch tips for branches on their tip commits
    // Remove branches that are do not have an existing tip id in the repo (e.g. deleted branches or truncated)
    void SetGitBranchTipsOnCommits(WorkRepo repo)
    {
        List<string> notFoundBranches = new List<string>();

        foreach (var b in repo.Branches.Values)
        {
            if (!repo.CommitsById.TryGetValue(b.TipID, out var tip))
            {   // A branch tip id, which commit id does not exist in the repo (deleted branch or truncated repo)
                // Store that branch name so it can be removed from the list later
                notFoundBranches.TryAdd(b.Name);
                continue;
            }

            if (!b.IsDetached)
            {   // Adding the branch to the branch tip commit (unless detached, handled separately later)
                tip.Branches.TryAdd(b);
                tip.BranchTips.TryAdd(b.Name);
            }

            b.BottomID = b.TipID; // We initialize the bottomId to same as tip (moved down later)
        }

        // Remove branches that do not have existing tip commit id,
        notFoundBranches.ForEach(n => repo.Branches.Remove(n));
    }


    // Update a commit with parents and children to be able to traverse the commit graph
    // Also swap parent order for pull merges, to make branch structure more logical and persistent
    void SetCommitParentsAndChildren(WorkRepo repo)
    {
        foreach (var c in repo.Commits)
        {
            // Parsing commit subject to if possible determine likely branch name (result cached in service)
            branchNameService.ParseCommitSubject(c);

            if (c.ParentIds.Count == 2 && branchNameService.IsPullMerge(c))
            {   // if the commit is a pull merge (remote commits merged into the local branch),
                // The order of parents are switched, to make the branch structure more logical.
                // So the first parent is the now remote branch and second parent the local branch
                // This makes the local commits to look like they where merged into the remote
                // branch, instead of existin remote commits merged/moved into the local branch,
                // which would make the remote branch alter commit order whenever local commits 
                // are not updated to remote server in time.
                var tmp = c.ParentIds[0];
                c.ParentIds[0] = c.ParentIds[1];
                c.ParentIds[1] = tmp;
            }

            if (c.ParentIds.Any() && repo.CommitsById.TryGetValue(c.ParentIds[0], out var firstParent))
            {   // Commit has a first parent, and that parents children is updated with this commit
                c.FirstParent = firstParent;
                firstParent.FirstChildren.Add(c);
                firstParent.AllChildIds.Add(c.Id);
                firstParent.FirstChildIds.Add(c.Id);
            }

            if (c.ParentIds.Count > 1 && repo.CommitsById.TryGetValue(c.ParentIds[1], out var mergeParent))
            {   // Commit has a merge parent, that parents merge children is updated with this commit
                c.MergeParent = mergeParent;
                mergeParent.MergeChildren.Add(c);
                mergeParent.AllChildIds.Add(c.Id);
                mergeParent.MergeChildIds.Add(c.Id);
            }
        }
    }

    void DetermineAllCommitsBranches(WorkRepo repo, GitRepo gitRepo)
    {
        foreach (var c in repo.Commits)
        {
            // Determine commit branch as most likely as possible or as an ambiguous branch
            var branch = DetermineCommitBranch(repo, c, gitRepo);
            c.Branch = branch;

            if (!c.IsAmbiguous)
            {   // Commit has a branch, clear other possible branches
                c.Branches.Clear();
            }
            c.Branches.TryAdd(branch);

            // Set the IsLikely property if the branch is likly to be the correct branch
            if (branchNameService.TryGetBranchName(c.Id, out string name) && branch.Name == name)
            {   // This flag might improve other commits below to select correct branch;
                c.IsLikely = true;
            }

            // If this commit is a main branch, then its first parent will likey be it too.
            SetMasterBackbone(c);

            // Advance tha bottom id to eventually determine the bottom commit of the branch
            c.Branch.BottomID = c.Id;
        }
    }

    WorkBranch DetermineCommitBranch(WorkRepo repo, WorkCommit commit, GitRepo gitRepo)
    {
        commit.Branches.TryAddAll(commit.FirstChildren.SelectMany(c => c.Branches));
        var branchNames = string.Join(",", commit.Branches.Select(b => b.Name));

        WorkBranch? branch;
        if (commit.Id == Repo.TruncatedLogCommitID)
        {
            return AddTruncatedBranch(repo);
        }
        else if (TryIsBranchSetByUser(repo, gitRepo, commit, out branch))
        {   // Commit branch was set/determined by user,
            return branch!;
        }
        else if (TryHasOnlyOneBranch(commit, out branch))
        {   // Commit only has one branch, use that
            return branch!;
        }
        else if (TryIsLocalRemoteBranch(commit, out branch))
        {   // Commit has only local and its remote branch, prefer remote remote branch
            return branch!;
        }
        else if (TryHasMainBranch(commit, out branch))
        {   // Commit, has several possible branches, and one is in the priority list, e.g. main, master, ...
            return branch!;
        }
        else if (TryIsMergedDeletedBranchTip(repo, commit, out branch))
        {   // Commit has no branches and no children, but has a merge child.
            // The commit is a tip of a deleted branch. It might be a deleted remote branch.
            // Lets try determine branch name based on merge child's subject
            // or use a generic branch name based on commit id
            return branch!;
        }
        else if (TryIsStrangeDeletedBranchTip(repo, commit, out branch))
        {   // Commit has no branches and no children, but may have merge children.
            // The commit is a tip of a deleted remote branch.
            // Lets try determine branch name based on merge child's subject 
            // or use a generic branch name based on commit id
            return branch!;
        }
        else if (TryHasBranchNameInSubject(repo, commit, out branch))
        {   // A branch name could be parsed form the commit subject or a child subject.
            // The commit will be set to that branch and also if above (first child) commits have
            // ambiguous branches, the will be reset to same branch as well. This will 'repair' branch
            // when a parsable commit subjects are encountered.
            return branch!;
        }
        else if (TryHasOnlyOneChild(commit, out branch))
        {   // Commit has one child commit reuse that child commit branch
            return branch!;
        }
        else if (TryHasOneChildWithLikelyBranch(commit, out branch))
        {   // Commit multiple possible git branches but has one child, which has a likely known branch, use same branch
            return branch!;
        }
        else if (TryHasMultipleChildrenWithOneLikelyBranch(commit, out branch))
        {   // Commit multiple possible git branches but has a child, which has a likely known branch, use same branch
            return branch!;
        }
        else if (TrySameChildrenBranches(commit, out branch))
        {   // For e.g. pull merges, a commit can have two children with same logical branch
            return branch!;
        }
        else if (TryIsMergedBranchesToParent(repo, commit, out branch))
        {   // Checks if a commit with 2 children and if the one child branch is merged into the 
            // other child branch. E.g. like a pull request or feature branch
            return branch!;
        }
        else if (TryIsChildAmbiguousCommit(commit, out branch))
        {   // If one of the commit children is a an ambiguous commit, reuse same branch
            // Log.Info($"Commit {commit.Sid} has ambiguous child commit {branchNames}");
            return branch!;
        }
        // Log.Warn($"Ambiguous branch {commit}");

        // Commit, has several possible branches, and we could not determine which branch is best,
        // create a new ambiguous branch. Later commits may fix this by parsing subjects of later
        // commits, or the user has to manually set the branch.
        return AddAmbiguousCommit(repo, commit);
    }


    // Commit branch was set/determined by user,
    bool TryIsBranchSetByUser(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        if (!gitRepo.MetaData.TryGetCommitBranch(commit.Sid, out var branchNiceName, out var isSetByUser))
        {   // Commit has not a branch set by user
            return false;
        }
        // Log.Info($"Commit {commit.Sid} has branch set to {branchHumanName} (by user: {isSetByUser})");

        var branches = commit.Branches.Where(b => b.NiceName == branchNiceName);
        if (!branches.Any())
        {   // Branch not found by obvious commit branches, create a new branch
            commit.IsBranchSetByUser = isSetByUser;
            branch = AddNamedBranch(repo, commit, branchNiceName);
            return true;
        }

        // Prefer remote branches over local branches 
        var remote = branches.FirstOrDefault(b => b.IsRemote);
        if (remote != null)
        {
            commit.IsBranchSetByUser = isSetByUser;
            branch = remote;
            return TrySetBranch(repo, commit, branch);
        }

        // Just use the first branch with that human name
        commit.IsBranchSetByUser = isSetByUser;
        branch = branches.First();
        return TrySetBranch(repo, commit, branch);
    }

    // Commit only has one branch, use that
    bool TryHasOnlyOneBranch(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 1)
        {  // Commit only has one branch, use that
            branch = commit.Branches[0];
            return true;
        }

        branch = null;
        return false;
    }

    // Commit has only local and its remote branch, prefer remote remote branch
    bool TryIsLocalRemoteBranch(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 2)
        {
            if (commit.Branches[0].IsRemote && commit.Branches[0].Name == commit.Branches[1].RemoteName)
            {   // remote and local branch, prefer remote
                branch = commit.Branches[0];
                return true;
            }
            if (!commit.Branches[0].IsRemote && commit.Branches[0].RemoteName == commit.Branches[1].Name)
            {   // local and remote branch, prefer remote
                branch = commit.Branches[1];
                return true;
            }
        }

        branch = null;
        return false;
    }


    // For e.g. pull merges, a commit can have two children with same logical branch
    bool TrySameChildrenBranches(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 2 && commit.FirstChildren.Count == 2 &&
            commit.FirstChildren[0].Branch!.PrimaryName == commit.FirstChildren[1].Branch!.PrimaryName)
        {   // Commit has 2 children with same branch use that
            if (commit.FirstChildren[0].Branch!.PullMergeParentBranch != null &&
                commit.FirstChildren[0].Branch!.PullMergeParentBranch!.Name == commit.FirstChildren[1].Branch!.LocalName)
            {   // child branch 0 is a pull merge of child 1 local of remote branch 1, prefer parent 1
                branch = commit.FirstChildren[1].Branch;
                commit.IsAmbiguous = commit.FirstChildren[1].IsAmbiguous;
                return true;
            }
            if (commit.FirstChildren[0].Branch!.PullMergeParentBranch == commit.FirstChildren[1].Branch)
            {   // child branch 0 is a pull merge of child branch 1, prefer parent 1
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


    // Commit has no branches and no children, but has a merge child.
    // The commit is a tip of a deleted branch. It might be a deleted remote branch.
    // Lets try determine branch name based on merge child's subject
    // or use a generic branch name based on commit id
    bool TryIsMergedDeletedBranchTip(
        WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 0 && commit.FirstChildren.Count == 0 && commit.MergeChildren.Count == 1)
        {   // Commit has no branch and no children, but has a merge child. I.e. must be a
            // deleted branch that was merged into some other branch.
            // Trying to use parsed branch name from the merge children subjects e.g. like:
            // "Merge branch 'branch-name' into develop"
            if (branchNameService.TryGetBranchName(commit.Id, out var name))
            {   // Managed to parse a branch-name 
                var mergeChild = commit.MergeChildren[0];

                if (branchNameService.IsPullMerge(mergeChild) &&
                    mergeChild.Branch!.NiceName == name)
                {   // The branch is a pull name and has same name as the branch is was merged into
                    // The merge child is a pull merge, so this commit is on a "dead" branch part,
                    // which used to be the local branch of the pull merge commit.
                    // Lets connect this branch with the actual branch.
                    var pullMergeBranch = mergeChild.Branch;
                    branch = AddPullMergeBranch(repo, commit, name, pullMergeBranch!);
                    pullMergeBranch!.PullMergeChildBranches.TryAdd(branch);
                    return true;
                }

                branch = AddNamedBranch(repo, commit, name);
                return true;
            }

            // could not parse a name from any of the merge children, use id named branch
            branch = AddNamedBranch(repo, commit);
            return true;
        }

        branch = null;
        return false;
    }


    // Commit has no branches and no children, but may have merge children.
    // The commit is a tip of a deleted remote branch.
    // Lets try determine branch name based on merge child's subject 
    // or use a generic branch name based on commit id
    bool TryIsStrangeDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 0 && commit.FirstChildren.Count == 0)
        {   // Commit has no branch, and no children, must be a deleted branch tip unusual branch
            // Trying to use parsed branch name from one of the merge children subjects e.g. Merge branch 'a' into develop

            if (branchNameService.TryGetBranchName(commit.Id, out var name))
            {   // Managed to parse a branch name
                branch = AddNamedBranch(repo, commit, name);
                return true;
            }

            // could not parse a name from any of the merge children, use id named branch
            branch = AddNamedBranch(repo, commit);
            return true;
        }

        branch = null;
        return false;
    }


    // Commit multiple possible git branches but has one child, which has a likely known branch, use same branch
    bool TryHasOneChildWithLikelyBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.FirstChildren.Count == 1 && c.FirstChildren[0].IsLikely)
        {   // Commit has one child, which has a likely known branch, use same branch
            branch = c.FirstChildren[0].Branch;
            c.IsAmbiguous = c.FirstChildren[0].IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
    }

    // Commit multiple possible git branches but has a child, which has a likely known branch, use same branch
    bool TryHasMultipleChildrenWithOneLikelyBranch(WorkCommit c, out WorkBranch? branch)
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
        {   // The branch is remote, we prefer that
            branch = child.Branch;
            return true;
        }

        if (child.Branch!.RemoteName != "")
        {   // The child branch has a corresponding remote branch, lets try to use that
            var remoteBranch = c.Branches.FirstOrDefault(b => b.Name == child.Branch!.RemoteName);
            if (remoteBranch != null)
            {   // The child branch was local and the corresponding remote is also possible, 
                branch = remoteBranch;
                return true;
            }
        }

        branch = child.Branch;
        c.IsAmbiguous = child.IsAmbiguous;
        return true;
    }


    // Commit, has several possible branches, and one is in the priority list, e.g. main, master, ...
    bool TryHasMainBranch(WorkCommit c, out WorkBranch? branch)
    {
        branch = null;
        if (c.Branches.Count < 1) return false;

        // Check if commit has one of the main branches
        foreach (var name in MainBranchNamePriority)
        {
            branch = c.Branches.Find(b => b.Name == name);
            if (branch != null)
            {
                return true;
            }
        }

        return false;
    }

    // A branch name could be parsed form the commit subject or a child subject.
    // The commit will be set to that branch and also if above (first child) commits have
    // ambiguous branches, the will be reset to same branch as well. This will 'repair' branch
    // when a parsable commit subjects are encountered.
    bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;

        if (!branchNameService.TryGetBranchName(commit.Id, out var name)) return false;

        // A branch name could be parsed form the commit subject or a merge child subject.
        branch = TryGetBranchFromName(commit, name);
        if (branch == null)
        {   // Found no matching branch
            return false;
        }

        return TrySetBranch(repo, commit, branch);
    }


    bool TrySetBranch(WorkRepo repo, WorkCommit commit, WorkBranch branch)
    {
        // Lets use that as a branch name and also let children (commits above)
        // use that branch if they are an ambiguous branch
        if (branch.TipID == commit.Id)
        {  // The commit is branch tip, we should not find higher/previous commit up, since tip would move up  
            commit.Branch = branch;
            commit.IsLikely = true;
            commit.Branches.TryAdd(branch);
            return true;
        }

        // Lets iterate upp (first child) as long as commits are ambiguous and the branch exists
        var namedBranch = branch;
        var current = commit;
        Dictionary<string, string> bottoms = new Dictionary<string, string>();
        while (current.Id != branch.TipID)
        {
            var child = current.FirstChildren
                .Where(c => c.IsAmbiguous)
                .FirstOrDefault(c => c.Branches.Contains(namedBranch));
            if (child == null)
            {   // No ambiguous child commit with that branch, cannot step up further
                break;
            }
            // Remember highest known id of each branch, to later be used to set branch bottom id
            bottoms[child.Branch!.Name] = child.Id;

            // Step upp to child
            current = child;
        }

        if (current.FirstChildren.Any() &&
            current.Id != branch.TipID &&
            null == current.FirstChildren.FirstOrDefault(c => !c.IsAmbiguous && c.Branch == namedBranch))
        {   // Failed to reach last not ambiguous branch part of named branch
            return false;
        }

        branch.AmbiguousTipId = "";
        branch.IsAmbiguousBranch = false;
        branch.AmbiguousBranches.Clear();

        // Adjust bottom id of seen branches since commits have been moved to new branch
        foreach (var pair in bottoms)
        {
            var com = repo.CommitsById[pair.Value];
            if (com.Branch != branch)
            {
                // Need to move bottom of current branch upp to current child since current will
                // belong to other branch
                if (com.FirstChildren.Any())
                {   // Sett branch bottom to child
                    var firstOtherChild = com.FirstChildren.FirstOrDefault(c => c.Branch == com.Branch);
                    if (firstOtherChild != null)
                    {
                        com.Branch!.BottomID = firstOtherChild.Id;
                    }
                    else
                    {   // Must have been a tip on current
                        com.Branch!.BottomID = com.Id;
                    }
                }
                else
                {   // Has no children, set to current
                    com.Branch!.BottomID = com.Id;
                }
            }
        }

        do
        {
            if (current.Branch != null && current.Branch != branch &&
                current.Branch.AmbiguousTipId == current.Id)
            {
                current.Branch.IsAmbiguousBranch = false;
                current.Branch.AmbiguousBranches.Clear();
                current.Branch.AmbiguousTipId = "";
            }

            current.Branch = branch;
            current.IsAmbiguous = false;
            current.IsAmbiguousTip = false;
            current.IsLikely = true;
            current.Branches.Clear();
            current.Branches.TryAdd(branch);

            if (current == commit)
            {
                break;
            }
            current = current.FirstParent;

        } while (current != null);

        return true;
    }


    WorkBranch? TryGetBranchFromName(WorkCommit commit, string name)
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

        return branch;
    }

    // Commit has one child commit reuse that child commit branch
    bool TryHasOnlyOneChild(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.FirstChildren.Count == 1)
        {   // Commit has only one child, ensure commit has same possible branches
            var child = commit.FirstChildren[0];
            if (commit.Branches.Count != child.Branches.Count)
            {   // Number of branches have changed
                branch = null;
                return false;
            }

            for (int i = 0; i < commit.Branches.Count; i++)
            {
                if (commit.Branches[i].Name != child.Branches[i].Name)
                {   // Some branch has changed
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

    // Checks if a commit with 2 children and if the one child branch is merged into the 
    // other child branch. E.g. like a pull request or feature branch
    bool TryIsMergedBranchesToParent(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
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

            if (!b2.IsGitBranch && b2Bottom.FirstParent == commit &&
                b2MergeChildren.Count == 1 &&
                b2MergeChildren[0].Branch == b1)
            {
                branch = b1;
                return true;
            }
            if (!b1.IsGitBranch && b1Bottom.FirstParent == commit &&
                b1MergeChildren.Count == 1 &&
                b1MergeChildren[0].Branch == b2)
            {
                branch = b2;
                return true;
            }
        }

        return false;
    }


    // If one of the commit children is a an ambiguous commit, reuse same branch
    bool TryIsChildAmbiguousCommit(WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var ambiguousChild = commit.FirstChildren.FirstOrDefault(c => c.IsAmbiguous);
        if (ambiguousChild == null)
        {   // No ambiguous child
            return false;
        }

        branch = ambiguousChild.Branch!;
        var amBranch = branch;

        // If more ambiguous children, merge in their sub branches as well
        commit.FirstChildren
            .Where(c => c.IsAmbiguous && c != ambiguousChild)
            .ForEach(c => c.Branch!.AmbiguousBranches.ForEach(b => commit.Branches.TryAdd(b)));

        commit.IsAmbiguous = true;
        return true;
    }


    void SetMasterBackbone(WorkCommit c)
    {
        if (c.FirstParent == null || c.Branch == null)
        {   // Reached the end of the repository or commit has no branch (which it always has now)
            return;
        }

        if (MainBranchNamePriority.Contains(c.Branch.Name))
        {   // main and develop are special and will make a "backbone" for other branches to depend on
            // Adding this branch to the first parent branches will make it likly to be set as
            // branch for the parent as well, and so on up to the first (oldest/last) commit.
            c.FirstParent.Branches.TryAdd(c.Branch);
        }
    }

    WorkBranch AddPullMergeBranch(
       WorkRepo repo, WorkCommit c, string name, WorkBranch pullMergeParentBranch)
    {
        var branchName = name != "" ? $"{name}:{c.Sid}" : $"branch:{c.Sid}";
        var humanName = name != "" ? name : $"branch@{c.Sid}";
        var branch = new WorkBranch(
            name: branchName,
            primaryName: pullMergeParentBranch.PrimaryName,
            niceName: humanName,
            tipID: c.Id);
        branch.PullMergeParentBranch = pullMergeParentBranch;

        repo.Branches[branch.Name] = branch;
        repo.Branches[branch.PrimaryName].RelatedBranches.Add(branch);
        return branch;
    }

    WorkBranch AddTruncatedBranch(WorkRepo repo)
    {
        var branchName = truncatedBranchName;
        var branch = new WorkBranch(
            name: branchName,
            primaryName: branchName,
            niceName: branchName,
            tipID: Repo.TruncatedLogCommitID);

        repo.Branches[branch.Name] = branch;
        repo.Branches[branch.PrimaryName].RelatedBranches.Add(branch);
        return branch;
    }

    WorkBranch AddNamedBranch(WorkRepo repo, WorkCommit c, string name = "")
    {
        var branchName = name != "" ? $"{name}:{c.Sid}" : $"branch:{c.Sid}";
        var niceName = name != "" ? name : "branch";
        var branch = new WorkBranch(
            name: branchName,
            primaryName: branchName,
            niceName: niceName,
            tipID: c.Id);

        repo.Branches[branch.Name] = branch;
        repo.Branches[branch.PrimaryName].RelatedBranches.Add(branch);
        return branch;
    }

    // Commit, has several possible branches, and we could not determine which branch is best,
    // create a new ambiguous branch. Later commits may fix this by parsing subjects of later
    // commits, or the user has to manually set the branch.
    WorkBranch AddAmbiguousCommit(WorkRepo repo, WorkCommit c)
    {
        WorkBranch? branch = null;
        List<WorkBranch>? ambiguousBranches = null;
        if (!c.Branches.Any())
        {
            branch = AddNamedBranch(repo, c, "ambiguous");
            ambiguousBranches = new List<WorkBranch>() { branch };
        }
        else
        {
            (branch, ambiguousBranches) = GetLikelyBranches(c);
        }

        c.IsAmbiguous = true;
        c.Branch = branch;
        c.Branch.IsAmbiguousBranch = true;
        c.Branch.AmbiguousTipId = c.Id;
        c.Branch.AmbiguousBranches = ambiguousBranches;
        c.Branches.TryAddAll(ambiguousBranches);

        return branch;
    }

    (WorkBranch, List<WorkBranch>) GetLikelyBranches(WorkCommit commit)
    {
        var ambiguousBranches = commit.Branches;

        if (commit.FirstChildren.Count < 2)
        {
            // Commit has no children (i.e.must a branch tip with multiple possible tips)
            // Prefer remote branch if possible
            var likelyBranch = commit.Branches.FirstOrDefault(b => b.IsRemote);
            if (likelyBranch == null)
            {   // No remote branch, just take one branch
                likelyBranch = ambiguousBranches.First();
            }

            return (likelyBranch, ambiguousBranches);
        }

        // Likely child is preferred
        var likelyChild = commit.FirstChildren.FirstOrDefault(c => c.IsLikely);
        if (likelyChild != null)
        {
            var likelyBranch = likelyChild.Branch!;
            ambiguousBranches = ambiguousBranches
                .Concat(commit.FirstChildren.Select(c => c.Branch!))
                .Distinct().ToList();

            return (likelyBranch, ambiguousBranches);
        }

        // Determine the most likely branch (branch of the oldest child)
        var oldestChild = commit.FirstChildren[0];
        List<WorkBranch> childBranches = new List<WorkBranch>();
        foreach (var c in commit.FirstChildren)
        {
            if (c.AuthorTime > oldestChild.AuthorTime)
            {
                oldestChild = c;
            }
            childBranches.Add(c.Branch!);
        }

        var likelyBranch2 = oldestChild.Branch!;
        ambiguousBranches = ambiguousBranches.Concat(childBranches).Distinct().ToList();

        return (likelyBranch2, ambiguousBranches);
    }


    // Determine the parent/child relationship between branches, and which branch is the main branch
    // A child branch is branches from a parent branch
    void DetermineBranchHierarchy(WorkRepo repo)
    {
        foreach (var b in repo.Branches.Values)
        {
            if (b.IsAmbiguousBranch)
            {
                // Log.Info($"Ambiguous branch: {b.Name} at {b.AmbiguousTipId}");
                repo.CommitsById[b.AmbiguousTipId].IsAmbiguousTip = true;
            }

            if (b.BottomID == "")
            {   // For branches with no own commits (multiple tips to same commit)
                b.BottomID = b.TipID;
            }
            if (b.RemoteName != "")
            {   // For a local branch with a remote branch, the remote branch is parent.
                var remoteBranch = repo.Branches[b.RemoteName];
                b.ParentBranch = remoteBranch;
                continue;
            }
            if (b.PullMergeParentBranch != null)
            {   // A pull merge branch branch 
                b.ParentBranch = b.PullMergeParentBranch;
                continue;
            }

            var bottom = repo.CommitsById[b.BottomID];
            if (bottom.Branch != b)
            {   // Branch does not own the bottom (or tip commit), i.e. a branch pointer to another branch with no own commits yet 
                b.ParentBranch = bottom.Branch;
            }
            else if (bottom.FirstParent != null)
            {   // Branch bottom commit has a first parent, use that as parent branch
                b.ParentBranch = bottom.FirstParent.Branch;
            }
        }

        // A repo can have several root branches (e.g. the doc branch in GitHub)
        repo.Branches.TryGetValue(truncatedBranchName, out var truncatedBranch);
        var rootBranches = repo.Branches.Values.Where(b => b.ParentBranch == null || b.ParentBranch == truncatedBranch).ToList();
        if (!rootBranches.Any()) return;  // No root branches (empty repo)

        // Select most likely root branch (but prioritize)
        var rootBranch = rootBranches.First();
        foreach (var name in MainBranchNamePriority)
        {
            var branch = rootBranches.FirstOrDefault(b => b.Name == name);
            if (branch != null)
            {
                rootBranch = branch;
                break;
            }
        }

        if (truncatedBranch != null)
        {   // Remove the truncated branch and redirect all its children to the root branch
            var truncatedCommit = repo.CommitsById[Repo.TruncatedLogCommitID];
            truncatedCommit.Branch = rootBranch;
            rootBranch.ParentBranch = null;
            rootBranch.BottomID = truncatedCommit.Id;
            repo.Branches.Remove(truncatedBranch.Name);

            // Redirect all branches that has the truncated branch as parent to the root branch instead
            repo.Branches.Values
                .Where(b => b.ParentBranch == truncatedBranch)
                .ForEach(b => b.ParentBranch = rootBranch);
        }

        // Mark the main root branch as the main branch (and its corresponding local branch as well)
        rootBranch.IsMainBranch = true;
        if (rootBranch.LocalName != "")
        {
            var rootLocalBranch = repo.Branches[rootBranch.LocalName];
            rootLocalBranch.IsMainBranch = true;
        }
    }

    void DetermineAncestors(WorkRepo repo)
    {
        foreach (var b in repo.Branches.Values)
        {
            var current = b.ParentBranch;
            while (current != null)
            {
                if (b.Ancestors.Contains(current))
                {
                    Log.Warn($"Branch {b.Name} has circular ancestor {current.Name}");
                    Log.Warn("Ancestors: " + b.Ancestors.Select(a => a.Name).Join(","));
                    b.IsCircularAncestors = true;
                    break;
                }
                b.Ancestors.Add(current);
                if (b.Ancestors.Count > 50)
                {
                    Log.Warn($"Branch {b} has more than 20 ancestors");
                }
                current = current.ParentBranch;
            }
        }
    }
}

