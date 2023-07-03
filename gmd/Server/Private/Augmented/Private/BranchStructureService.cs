namespace gmd.Server.Private.Augmented.Private;

interface IBranchStructureService
{
    void SetCommitBranches(WorkRepo repo, GitRepo gitRepo);
}


class BranchStructureService : IBranchStructureService
{
    readonly string[] DefaultBranchPriority = new string[] { "origin/main", "main", "origin/master", "master", "origin/trunk", "trunk" };
    const string truncatedBranchName = "<truncated-branch>";


    readonly IBranchNameService branchNameService;

    public BranchStructureService(IBranchNameService branchNameService)
    {
        this.branchNameService = branchNameService;
    }


    public void SetCommitBranches(WorkRepo repo, GitRepo gitRepo)
    {
        SetGitBranchTips(repo);
        SetCommitBranchesAndChildren(repo);
        DetermineCommitBranches(repo, gitRepo);
        DetermineBranchHierarchy(repo);
    }

    void SetGitBranchTips(WorkRepo repo)
    {
        List<string> invalidBranches = new List<string>();

        foreach (var b in repo.Branches)
        {
            if (!repo.CommitsById.TryGetValue(b.TipID, out var tip))
            {
                // A branch tip id, which commit id does not exist in the repo
                // Store that branch name so it can be removed from the list below
                invalidBranches.TryAdd(b.Name);
                continue;
            }

            // Adding the branch to the branch tip commit
            if (!b.IsDetached)
            {
                tip.Branches.TryAdd(b);
                tip.BranchTips.TryAdd(b.Name);
            }
            b.BottomID = b.TipID; // We initialize the bottomId to same as tip
        }

        // Remove branches that do not have existing tip commit id,
        foreach (var name in invalidBranches)
        {
            int i = repo.Branches.FindIndex(b => b.Name == name);
            if (i != -1)
            {
                repo.Branches.RemoveAt(i);
            }
        }
    }

    void SetCommitBranchesAndChildren(WorkRepo repo)
    {
        foreach (var c in repo.Commits)
        {
            branchNameService.ParseCommit(c);
            if (c.ParentIds.Count == 2 && branchNameService.IsPullMerge(c))
            {   // if the commit is a pull merger, we do switch the order of parents
                // So the first parent is the remote branch and second parent the local branch
                var tmp = c.ParentIds[0];
                c.ParentIds[0] = c.ParentIds[1];
                c.ParentIds[1] = tmp;
            }

            if (c.ParentIds.Count > 0 && repo.CommitsById.TryGetValue(c.ParentIds[0], out var firstParent))
            {
                c.FirstParent = firstParent;
                firstParent.Children.Add(c);
                firstParent.ChildIds.Add(c.Id);
            }

            if (c.ParentIds.Count > 1 && repo.CommitsById.TryGetValue(c.ParentIds[1], out var mergeParent))
            {
                c.MergeParent = mergeParent;
                mergeParent.MergeChildren.Add(c);
                mergeParent.ChildIds.Add(c.Id);
            }
        }
    }

    void DetermineCommitBranches(WorkRepo repo, GitRepo gitRepo)
    {
        foreach (var c in repo.Commits)
        {
            var branch = DetermineCommitBranch(repo, c, gitRepo);
            c.Branch = branch;
            if (!c.IsAmbiguous)
            {
                c.Branches.Clear();
            }
            c.Branches.TryAdd(branch);

            string name = branchNameService.GetBranchName(c.Id);
            if (c.Branch.CommonName == name)
            {
                // This flag might improve other commits below to select correct branch;
                c.IsLikely = true;
            }

            SetMasterBackbone(c);
            c.Branch.BottomID = c.Id;
        }
    }

    WorkBranch DetermineCommitBranch(WorkRepo repo, WorkCommit commit, GitRepo gitRepo)
    {
        commit.Branches.TryAddAll(commit.Children.SelectMany(c => c.Branches));
        var branchNames = string.Join(",", commit.Branches.Select(b => b.Name));

        WorkBranch? branch;
        if (commit.Id == Repo.TruncatedLogCommitID)
        {
            commit.Branches.Clear();
            return AddTruncatedBranch(repo);
        }
        // else if (TryIsBranchSetByUser(repo, gitRepo, commit, out branch))
        // {   // Commit branch was set/determined by user,
        //     commit.Branches.Clear();
        //     return branch!;
        // }
        else if (TryHasOnlyOneBranch(commit, out branch))
        {   // Commit only has one branch, use that
            return branch!;
        }
        // else if (TryIsLocalRemoteBranch(commit, out branch))
        // {   // Commit has only local and its remote branch, prefer remote remote branch
        //     commit.Branches.Clear();
        //     return branch!;
        // }
        // else if (TryIsMergedDeletedRemoteBranchTip(repo, commit, out branch))
        // {   // Commit has no branches and no children, but has a merge child.
        //     // The commit is a tip of a deleted branch. It might be a deleted remote branch.
        //     // Lets try determine branch name based on merge child's subject
        //     // or use a generic branch name based on commit id
        //     return branch!;
        // }
        // else if (TryIsMergedDeletedBranchTip(repo, commit, out branch))
        // {   // Commit has no branches and no children, but has a merge child.
        //     // The commit is a tip of a deleted remote branch.
        //     // Lets try determine branch name based on merge child's subject 
        //     // or use a generic branch name based on commit id
        //     return branch!;
        // }
        // else if (TryHasMainBranch(commit, out branch))
        // {   // Commit, has several possible branches, and one is in the priority list, e.g. main, master, ...
        //     return branch!;
        // }
        // else if (TryHasBranchNameInSubject(repo, commit, out branch))
        // {   // A branch name could be parsed form the commit subject or a child subject.
        //     // The commit will be set to that branch and also if above (first child) commits have
        //     // ambiguous branches, the will be reset to same branch as well. This will 'repair' branch
        //     // when a parsable commit subjects are encountered.
        //     return branch!;
        // }
        // else if (TryHasOnlyOneChild(commit, out branch))
        // {   // Commit has one child commit reuse that child commit branch
        //     return branch!;
        // }
        // // old // else if (TryHasOneChildInDeletedBranch(commit, out branch)) // Not needed any more??
        // //  old // {   // Commit is middle commit in a deleted branch with only one child above, use same branch
        // //  old //    return branch!;
        // //  old  //}
        // else if (TryHasOneChildWithLikelyBranch(commit, out branch))
        // {   // Commit multiple possible git branches but has one child, which has a likely known branch, use same branch
        //     return branch!;
        // }
        // else if (TryHasMultipleChildrenWithOneLikelyBranch(commit, out branch))
        // {   // Commit multiple possible git branches but has a child, which has a likely known branch, use same branch
        //     return branch!;
        // }
        // // old //else if (TryAllChildrenArePullRequests(repo, commit, out branch))
        // // old //{
        // // old //    return branch!;
        // //old // }
        // else if (TrySameChildrenBranches(commit, out branch))
        // {   // For e.g. pull merges, a commit can have two children with same logical branch
        //     return branch!;
        // }
        // else if (TryIsMergedBranchesToParent(repo, commit, out branch))
        // {
        //     return branch!;
        // }
        else if (TryIsChildAmbiguousCommit(commit, out branch))
        {   // one of the commit children is a an ambiguous commit, reuse same branch
            return branch!;
        }

        // Commit, has several possible branches, and we could not determine which branch is best,
        // create a new ambiguous branch. Later commits may fix this by parsing subjects of later
        // commits, or the user has to manually set the branch.
        return AddAmbiguousCommit(repo, commit);
    }



    // Checks if a commit with 2 children and if the one child branch is merged into the 
    // other child branch. E.g. like a pull request or feature branch
    bool TryIsMergedBranchesToParent(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        if (commit.Children.Count == 2) // Could support more children as well
        {
            var b1 = commit.Children[0].Branch!;
            var b1MergeChildren = repo.CommitsById[b1.TipID].MergeChildren;
            var b1Bottom = repo.CommitsById[b1.BottomID];
            var b2 = commit.Children[1].Branch!;
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

    // bool TryAllChildrenArePullRequests(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    // {
    //     branch = null;
    //     foreach (var c in commit.Children)
    //     {
    //         if (commit.Children.Where(cc => cc != c)
    //             .All(cc => cc.Branch!.PullRequestParent == c.Branch!.Name))
    //         {
    //             branch = c.Branch!;
    //             return TrySetBranch(repo, commit, branch);
    //         }
    //     }

    //     return false;
    // }

    bool TryIsBranchSetByUser(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        if (!gitRepo.MetaData.TryGet(commit.Sid, out var branchDisplayName, out var isSetByUser))
        {   // Commit has not a branch set by user
            return false;
        }

        var branches = commit.Branches.Where(b => b.DisplayName == branchDisplayName);
        if (!branches.Any())
        {   // Branch once set by user is no longer possible (might have changed name or something)
            return false;
        }

        var remote = branches.FirstOrDefault(b => b.IsRemote);
        if (remote != null)
        {
            commit.IsBranchSetByUser = isSetByUser;
            branch = remote;
            return TrySetBranch(repo, commit, branch);
        }

        commit.IsBranchSetByUser = isSetByUser;
        branch = branches.First();
        return TrySetBranch(repo, commit, branch);
    }

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


    bool TrySameChildrenBranches(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 2 && commit.Children.Count == 2 &&
            commit.Children[0].Branch!.CommonName == commit.Children[1].Branch!.CommonName)
        {   // Commit has 2 children with same branch use that
            if (commit.Children[0].Branch!.PullMergeParentBranch != null &&
                commit.Children[0].Branch!.PullMergeParentBranch!.Name == commit.Children[1].Branch!.LocalName)
            {   // child branch 0 is a pull merge of child 1 local of remote branch 1, prefer parent 1
                branch = commit.Children[1].Branch;
                commit.IsAmbiguous = commit.Children[1].IsAmbiguous;
                return true;
            }
            if (commit.Children[0].Branch!.PullMergeParentBranch == commit.Children[1].Branch)
            {   // child branch 0 is a pull merge of child branch 1, prefer parent 1
                branch = commit.Children[1].Branch;
                commit.IsAmbiguous = commit.Children[1].IsAmbiguous;
                return true;
            }

            branch = commit.Children[0].Branch;
            commit.IsAmbiguous = commit.Children[0].IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
    }

    private bool TryIsMergedDeletedRemoteBranchTip(
        WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 0 && commit.Children.Count == 0 && commit.MergeChildren.Count == 1)
        {   // Commit has no branch and no children, but has a merge child. I.e. must be a
            // deleted branch that was merged into some other branch.
            // Trying to use parsed branch name from the merge children subjects e.g. like:
            // "Merge branch 'branch-name' into develop"
            string name = branchNameService.GetBranchName(commit.Id);

            if (name != "")
            {   // Managed to parse a branch-name 
                var mergeChild = commit.MergeChildren[0];

                if (branchNameService.IsPullMerge(mergeChild) &&
                    mergeChild.Branch!.DisplayName == name)
                {
                    // The merge child is a pull merge, so this commit is on a "dead" branch part,
                    // which used to be the local branch of the pull merge commit.
                    // We need to connect this branch with the actual branch.
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


    private bool TryIsMergedDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 0 && commit.Children.Count == 0)
        {   // Commit has no branch, must be a deleted branch tip merged into some branch or unusual branch
            // Trying to use parsed branch name from one of the merge children subjects e.g. Merge branch 'a' into develop
            string name = branchNameService.GetBranchName(commit.Id);
            if (name != "")
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


    // private bool TryHasOneChildInDeletedBranch(WorkCommit commit, out WorkBranch? branch)
    // {
    //     if (commit.Branches.Count == 0 && commit.Children.Count == 1 && !commit.Children[0].IsAmbiguous)
    //     {   // Commit has no branch, but it has one child commit, use that child commit branch
    //         branch = commit.Children[0].Branch;
    //         commit.IsAmbiguous = commit.Children[0].IsAmbiguous;
    //         return true;
    //     }

    //     branch = null;
    //     return false;
    // }

    bool TryHasOneChildWithLikelyBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Children.Count == 1 && c.Children[0].IsLikely)
        {   // Commit has one child, which has a likely known branch, use same branch
            branch = c.Children[0].Branch;
            c.IsAmbiguous = c.Children[0].IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
    }

    bool TryHasMultipleChildrenWithOneLikelyBranch(WorkCommit c, out WorkBranch? branch)
    {
        branch = null;
        if (c.Children.Count(c => c.IsLikely) != 1)
        {
            return false;
        }

        // commit has only one child with a likely branch
        var child = c.Children.First(c => c.IsLikely);
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


    bool TryHasMainBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Branches.Count < 1)
        {
            branch = null;
            return false;
        }

        // Check if commit has one of the main branches
        foreach (var name in DefaultBranchPriority)
        {
            branch = c.Branches.Find(b => b.Name == name);
            if (branch != null)
            {
                return true;
            }
        }

        branch = null;
        return false;
    }

    bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;

        string name = branchNameService.GetBranchName(commit.Id);
        if (name == "")
        {
            return false;
        }

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
            var child = current.Children
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

        if (current.Children.Any() &&
            current.Id != branch.TipID &&
            null == current.Children.FirstOrDefault(c => !c.IsAmbiguous && c.Branch == namedBranch))
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
                if (com.Children.Any())
                {   // Sett branch bottom to child
                    var firstOtherChild = com.Children.FirstOrDefault(c => c.Branch == com.Branch);
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

        // Try find a branch with the display name
        branch = commit.Branches.Find(b => b.DisplayName == name);
        if (branch != null)
        {
            return branch;
        }

        return branch;
    }

    private bool TryHasOnlyOneChild(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Children.Count == 1)//  Why is was this needed??? && c.MergeChildren.Count == 0)
        {   // Commit has only one child, ensure commit has same possible branches
            var child = commit.Children[0];
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


    private bool TryIsChildAmbiguousCommit(WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var ambiguousChild = commit.Children.FirstOrDefault(c => c.IsAmbiguous);
        if (ambiguousChild == null)
        {   // No ambiguous child
            return false;
        }

        branch = ambiguousChild.Branch!;
        var amBranch = branch;

        // If more ambiguous children, merge in their sub branches as well
        commit.Children
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

        if (DefaultBranchPriority.Contains(c.Branch.Name))
        {
            // main and develop are special and will make a "backbone" for other branches to depend on
            c.FirstParent.Branches.TryAdd(c.Branch);
        }
    }

    WorkBranch AddPullMergeBranch(
       WorkRepo repo, WorkCommit c, string name, WorkBranch pullMergeBranch)
    {
        var branchName = name != "" ? $"{name}:{c.Sid}" : $"branch:{c.Sid}";
        var displayName = name != "" ? name : $"branch@{c.Sid}";
        var branch = new WorkBranch(
            name: branchName,
            commonName: pullMergeBranch.CommonName,
            displayName: displayName,
            tipID: c.Id);
        branch.PullMergeParentBranch = pullMergeBranch;

        repo.Branches.Add(branch);
        return branch;
    }

    WorkBranch AddTruncatedBranch(WorkRepo repo)
    {
        var branchName = truncatedBranchName;
        var displayName = truncatedBranchName;
        var branch = new WorkBranch(
            name: branchName,
            commonName: branchName,
            displayName: displayName,
            tipID: Repo.TruncatedLogCommitID);

        repo.Branches.Add(branch);
        return branch;
    }

    WorkBranch AddNamedBranch(WorkRepo repo, WorkCommit c, string name = "")
    {
        var branchName = name != "" ? $"{name}:{c.Sid}" : $"branch:{c.Sid}";
        var displayName = name != "" ? name : $"branch@{c.Sid}";
        var branch = new WorkBranch(
            name: branchName,
            commonName: branchName,
            displayName: displayName,
            tipID: c.Id);

        repo.Branches.Add(branch);
        return branch;
    }

    internal WorkBranch AddAmbiguousCommit(WorkRepo repo, WorkCommit c)
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

        if (commit.Children.Count < 2)
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
        var likelyChild = commit.Children.FirstOrDefault(c => c.IsLikely);
        if (likelyChild != null)
        {
            var likelyBranch = likelyChild.Branch!;
            ambiguousBranches = ambiguousBranches
                .Concat(commit.Children.Select(c => c.Branch!))
                .Distinct().ToList();

            return (likelyBranch, ambiguousBranches);
        }

        // Determine the most likely branch (branch of the oldest child)
        var oldestChild = commit.Children[0];
        List<WorkBranch> childBranches = new List<WorkBranch>();
        foreach (var c in commit.Children)
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


    void DetermineBranchHierarchy(WorkRepo repo)
    {
        foreach (var b in repo.Branches)
        {
            if (b.BottomID == "")
            {
                // For branches with no own commits (multiple tips to same commit)
                b.BottomID = b.TipID;
            }

            if (b.RemoteName != "")
            {
                // For a local branch with a remote branch, the remote branch is parent.
                var remoteBranch = repo.Branches.First(bb => bb.Name == b.RemoteName);
                b.ParentBranch = remoteBranch;
                continue;
            }

            var bottom = repo.CommitsById[b.BottomID];
            if (bottom.Branch != b)
            {
                // the tip does not own the tip commit, i.e. a branch pointer to another branch
                b.ParentBranch = bottom.Branch;
            }
            else if (bottom.FirstParent != null)
            {
                b.ParentBranch = bottom.FirstParent.Branch;
            }
        }

        // A repo can have several root branches
        var truncatedBranch = repo.Branches.FirstOrDefault(b => b.Name == truncatedBranchName);
        var rootBranches = repo.Branches.Where(b => b.ParentBranch == null || b.ParentBranch == truncatedBranch).ToList();
        if (!rootBranches.Any())
        {   // No root branches (empty repo)
            return;
        }

        // Select most likely root branch (but prioritize)
        var rootBranch = rootBranches.First();
        foreach (var name in DefaultBranchPriority)
        {
            var branch = rootBranches.FirstOrDefault(b => b.Name == name);
            if (branch != null)
            {
                rootBranch = branch;
                break;
            }
        }

        if (truncatedBranch != null)
        {
            var truncatedCommit = repo.CommitsById[Repo.TruncatedLogCommitID];
            truncatedCommit.Branch = rootBranch;
            rootBranch.ParentBranch = null;
            rootBranch.BottomID = truncatedCommit.Id;
            repo.Branches.Remove(truncatedBranch);
            repo.Branches
                .Where(b => b.ParentBranch == truncatedBranch)
                .ForEach(b => b.ParentBranch = rootBranch);
        }

        rootBranch.IsMainBranch = true;
        if (rootBranch.LocalName != "")
        {
            var rootLocalBranch = repo.Branches.First(b => b.Name == rootBranch.LocalName);
            rootLocalBranch.IsMainBranch = true;
        }
    }
}

