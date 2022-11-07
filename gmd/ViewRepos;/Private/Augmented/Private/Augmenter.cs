
using GitCommit = gmd.Utils.Git.Commit;
using GitBranch = gmd.Utils.Git.Branch;

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IAugmenter
{
    Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo, int partialMax);
}

class Augmenter : IAugmenter
{
    readonly IBranchNameService branchNameService;

    readonly string[] DefaultBranchPriority = new string[] { "origin/main", "main", "origin/master", "master" };

    internal Augmenter(IBranchNameService branchNameService, IConverter converter)
    {
        this.branchNameService = branchNameService;
    }

    public Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo, int partialMax)
    {
        return Task.Run(() => GetAugRepo(gitRepo, partialMax));

    }

    private WorkRepo GetAugRepo(GitRepo gitRepo, int partialMax)
    {
        WorkRepo repo = new WorkRepo(gitRepo.TimeStamp, gitRepo.Path, ToStatus(gitRepo));

        SetAugBranches(repo, gitRepo);
        SetAugCommits(repo, gitRepo, partialMax);
        SetCommitBranches(repo);

        return repo;
    }


    Status ToStatus(GitRepo repo)
    {
        var s = repo.Status;
        return new Status(s.Modified, s.Added, s.Deleted, s.Conflicted,
          s.IsMerging, s.MergeMessage, s.AddedFiles, s.ConflictsFiles);
    }

    void SetAugBranches(WorkRepo repo, GitRepo gitRepo)
    {
        repo.Branches.AddRange(gitRepo.Branches.Select(b => new WorkBranch(b)));

        // Set local name of all remote branches, that have a corresponding local branch as well
        // Unset RemoteName of local branch if no corresponding remote branch (deleted on remote server)
        foreach (var b in repo.Branches)
        {
            if (b.RemoteName != "")
            {
                var remoteBranch = repo.Branches.Find(bb => bb.Name == b.RemoteName);
                if (remoteBranch != null)
                {   // Corresponding remote branch, set local branch name property
                    remoteBranch.LocalName = b.Name;
                }
                else
                {   // No remote corresponding remote branch, unset property
                    b.RemoteName = "";
                }
            }
        }
    }

    void SetAugCommits(WorkRepo repo, GitRepo gitRepo, int partialMax)
    {
        IReadOnlyList<GitCommit> gitCommits = gitRepo.Commits;
        // For repositories with a lot of commits, only the latest 'partialMax' number of commits
        // are used, i.w. partial commits, which should have parents, but they are unknown
        bool isPartialPossible = gitCommits.Count >= partialMax;
        bool isPartialNeeded = false;
        repo.Commits.Capacity = gitCommits.Count;

        // Iterate git commits in reverse
        for (var i = gitCommits.Count - 1; i >= 0; i--)
        {
            GitCommit gc = gitCommits[i];
            WorkCommit commit = new WorkCommit(gc);

            if (isPartialPossible)
            {
                // The repo was truncated, check if commits have missing parents, which will be set
                // to a virtual/fake "partial commit"
                if (commit.ParentIds.Count > 0)
                {
                    // Not a merge commit but check if parent is missing and need a partial commit parent
                    if (!repo.CommitsById.TryGetValue(commit.ParentIds[0], out var parent))
                    {
                        isPartialNeeded = true;
                        commit.ParentIds[0] = Repo.PartialLogCommitID;
                    }
                }

                if (commit.ParentIds.Count > 1)
                {
                    // Merge commit, check if parents are missing and need a partial commit parent
                    if (!repo.CommitsById.TryGetValue(commit.ParentIds[1], out var parent))
                    {
                        isPartialNeeded = true;
                        commit.ParentIds[1] = Repo.PartialLogCommitID;
                    }
                }
            }

            repo.Commits.Add(commit);
            repo.CommitsById[commit.Id] = commit;
        }

        if (isPartialNeeded)
        {
            // Add a virtual/fake partial commit, which some commits will have as a parent
            string msg = "...    (more commits)";
            WorkCommit pc = new WorkCommit(
                id: Repo.PartialLogCommitID, subject: msg, message: msg,
                author: "", authorTime: new DateTime(1, 1, 1), parentIds: new string[0]);
            pc.IsPartialLogCommit = true;
            repo.Commits.Add(pc);
            repo.CommitsById[pc.Id] = pc;
        }

        // Set current commit if there is a current branch with an existing tip
        GitBranch? currentBranch = gitRepo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {
            if (repo.CommitsById.TryGetValue(currentBranch.TipID, out var currentCommit))
            {
                currentCommit.IsCurrent = true;
            }
        }

        repo.Commits.Reverse();
    }

    void SetCommitBranches(WorkRepo repo)
    {
        SetGitBranchTips(repo);
        SetCommitBranchesAndChildren(repo);
        DetermineCommitBranches(repo);
        MergeAmbiguousBranches(repo);
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
                invalidBranches.Add(b.Name);
                continue;
            }

            // Adding the branch to the branch tip commit
            tip.TryAddToBranches(b);
            tip.BranchTips.Add(b.Name);
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
                // Adding the child branches to the parent branches (inherited down)
                firstParent.TryAddToBranches(c.Branches.ToArray());
            }

            if (c.ParentIds.Count > 1 && repo.CommitsById.TryGetValue(c.ParentIds[1], out var mergeParent))
            {
                c.MergeParent = mergeParent;
                mergeParent.MergeChildren.Add(c);
                mergeParent.ChildIds.Add(c.Id);
                // Note: merge parent do not inherit child branches
            }
        }
    }

    void DetermineCommitBranches(WorkRepo repo)
    {
        foreach (var c in repo.Commits)
        {
            var branch = DetermineCommitBranch(repo, c);
            c.Branch = branch;
            c.TryAddToBranches(c.Branch);

            SetMasterBackbone(c);
            c.Branch.BottomID = c.Id;
        }
    }

    WorkBranch DetermineCommitBranch(WorkRepo repo, WorkCommit c)
    {
        WorkBranch? branch;
        if (TryHasOnlyOneBranch(c, out branch))
        {   // Commit only has one branch, it must have been an actual branch tip originally, use that
            return branch!;
        }
        else if (TryIsLocalRemoteBranch(c, out branch))
        {
            // Commit has only local and its remote branch, prefer remote remote branch
            return branch!;
        }

        // else if branch := h.hasParentChildSetBranch(c, branchesChildren); branch != nil {
        // // The commit has several possible branches, and one is set as parent of the others by the user
        // return branch
        // } else if branch := h.hasChildrenPriorityBranch(c, branchesChildren); branch != nil {
        // 	// The commit has several possible branches, and one of the children's branches is set as the
        // 	// the parent branch of the other children's branches
        // 	return branch
        // }

        else if (TrySameChildrenBranches(c, out branch))
        {   // Commit has no branch but has 2 children with same branch
            return branch!;
        }
        else if (TryIsMergedDeletedRemoteBranchTip(repo, c, out branch))
        {   // Commit has no branch and no children, but has a merge child, the commit is a tip
            // of a deleted branch. It might be a deleted remote branch. Lets try determine branch name
            // based on merge child's subject or use a generic branch name based on commit id
            return branch!;
        }
        else if (TryIsMergedDeletedBranchTip(repo, c, out branch))
        {   // Commit has no branch and no children, but has a merge child, the commit is a tip
            // of a deleted remote branch, lets try determine branch name based on merge child's
            // subject or use a generic branch name based on commit id
            return branch!;
        }
        else if (TryHasOneChildInDeletedBranch(c, out branch))
        {   // Commit is middle commit in a deleted branch with only one child above, use same branch
            return branch!;
        }
        else if (TryHasOneChildWithLikelyBranch(c, out branch))
        {   // Commit multiple possible git branches but has one child, which has a likely known branch, use same branch
            return branch!;
        }
        else if (TryHasMainBranch(c, out branch))
        {   // Commit, has several possible branches, and one is in the priority list, e.g. main, master, ...
            return branch!;
        }
        else if (TryHasBranchNameInSubject(repo, c, out branch))
        {   // A branch name could be parsed form the commit subject or a child subject.
            // The commit will be set to that branch and also if above (first child) commits have
            // ambiguous branches, the will be reset to same branch as well. This will 'repair' branch
            // when a parsable commit subjects are encountered.
            return branch!;
        }
        else if (TryHasOnlyOneChild(c, out branch))
        {   // Commit has one child commit and not merge commits, reuse that child commit branch
            return branch!;
        }
        else if (TryIsChildAmbiguousBranch(c, out branch))
        {   // one of the commit children is a ambiguous branch, reuse same ambiguous branch
            return branch!;
        }

        // Commit, has several possible branches, and we could not determine which branch is best,
        // create a new ambiguous branch. Later commits may fix this by parsing subjects of later
        // commits, or the user has to manually set the branch.
        return AddAmbiguousBranch(repo, c);
    }


    private bool TryHasOnlyOneBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Branches.Count == 1)
        {  // Commit only has one branch, it must have been an actual branch tip originally, use that
            branch = c.Branches[0];
            return true;
        }

        branch = null;
        return false;
    }

    private bool TryIsLocalRemoteBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Branches.Count == 2)
        {
            if (c.Branches[0].IsRemote && c.Branches[0].Name == c.Branches[1].RemoteName)
            {   // remote and local branch, prefer remote
                branch = c.Branches[0];
                return true;
            }
            if (!c.Branches[0].IsRemote && c.Branches[0].RemoteName == c.Branches[1].Name)
            {   // local and remote branch, prefer remote
                branch = c.Branches[1];
                return true;

            }
        }
        branch = null;
        return false;
    }

    private bool TrySameChildrenBranches(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Branches.Count == 0 && c.Children.Count == 2 &&
            c.Children[0].Branch == c.Children[1].Branch)
        {   // Commit has no branch but has 2 children with same branch use that
            branch = c.Children[0].Branch;
            return true;
        }

        branch = null;
        return false;
    }

    private bool TryIsMergedDeletedRemoteBranchTip(WorkRepo repo, WorkCommit c, out WorkBranch? branch)
    {
        if (c.Branches.Count == 0 && c.Children.Count == 0 && c.MergeChildren.Count == 1)
        {   // Commit has no branch and no children, but has a merge child, lets check if pull merger
            // Trying to use parsed branch name from the merge children subjects e.g. Merge branch 'a' into develop
            string name = branchNameService.GetBranchName(c.Id);

            if (name != "")
            {   // Managed to parse a branch name
                var mergeChildBranch = c.MergeChildren[0].Branch!;

                if (name == mergeChildBranch.DisplayName)
                {
                    branch = mergeChildBranch;
                    return true;
                }

                branch = AddNamedBranch(repo, c, name);
                return true;

            }

            // could not parse a name from any of the merge children, use id named branch
            branch = AddNamedBranch(repo, c, "branch");
            return true;
        }

        branch = null;
        return false;
    }


    private bool TryIsMergedDeletedBranchTip(WorkRepo repo, WorkCommit c, out WorkBranch? branch)
    {
        if (c.Branches.Count == 0 && c.Children.Count == 0)
        {   // Commit has no branch, must be a deleted branch tip merged into some branch or unusual branch
            // Trying to use parsed branch name from one of the merge children subjects e.g. Merge branch 'a' into develop
            string name = branchNameService.GetBranchName(c.Id);
            if (name != "")
            {   // Managed to parse a branch name
                branch = AddNamedBranch(repo, c, name);
                return true;
            }

            // could not parse a name from any of the merge children, use id named branch
            branch = AddNamedBranch(repo, c, "branch");
            return true;
        }

        branch = null;
        return false;
    }


    private bool TryHasOneChildInDeletedBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Branches.Count == 0 && c.Children.Count == 1)
        {   // Commit has no branch, but it has one child commit, use that child commit branch
            branch = c.Children[0].Branch;
            return true;
        }

        branch = null;
        return false;
    }

    private bool TryHasOneChildWithLikelyBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Children.Count == 1 && c.Children[0].IsLikely)
        {   // Commit has one child, which has a likely known branch, use same branch
            branch = c.Children[0].Branch;
            return true;
        }

        branch = null;
        return false;
    }

    private bool TryHasMainBranch(WorkCommit c, out WorkBranch? branch)
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
            return branch != null;
        }

        branch = null;
        return false;
    }

    private bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit c, out WorkBranch? branch)
    {
        string name = branchNameService.GetBranchName(c.Id);
        if (name != "")
        {   // A branch name could be parsed form the commit subject or a merge child subject.
            // Lets use that as a branch name and also let children (commits above)
            // use that branch if they are an ambiguous branch
            WorkCommit? current = null;

            branch = TryGetBranchFromName(c, name);

            if (branch != null && branch.BottomID != "")
            {
                // Found an existing branch with that name, set lowest known commit to the bottom
                // of that known branch
                repo.CommitsById.TryGetValue(branch.BottomID, out current);
            }

            if (current == null)
            {
                // branch has no known last (bottom) commit, lets iterate upp (first child) as long
                // as commits are on an ambiguous branch
                for (current = c;
                    current.Children.Count == 1 && (current.Children[0].Branch?.IsAmbiguousBranch ?? true);
                    current = current.Children[0])
                {
                }
            }

            if (branch != null)
            {
                for (; current != null && current != c.FirstParent; current = current.FirstParent)
                {
                    current.Branch = branch;
                    current.TryAddToBranches(branch);
                    current.IsLikely = true;
                }

                return true;
            }
        }

        branch = null;
        return false;
    }


    private WorkBranch? TryGetBranchFromName(WorkCommit c, string name)
    {
        // Try find a live git branch with the name
        foreach (var b in c.Branches)
        {
            if (name == b.Name)
            {   // Found a branch, if the branch has a remote branch, try find that
                if (b.RemoteName != "")
                {
                    foreach (var b2 in c.Branches)
                    {
                        if (b.RemoteName == b2.Name)
                        {
                            // Found the remote branch, prefer that
                            return b2;
                        }
                    }
                }

                // branch b had no remote branch, use local
                return b;
            }
        }

        // Try find a branch with the display name
        return c.Branches.Find(b => b.DisplayName == name);
    }
    private bool TryHasOnlyOneChild(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Children.Count == 1 && c.MergeChildren.Count == 0)
        {   // Commit has only one child, ensure commit has same branches
            var child = c.Children[0];
            if (c.Branches.Count != child.Branches.Count)
            {
                // Number of branches have changed
                branch = null;
                return false;
            }

            for (int i = 0; i < c.Branches.Count; i++)
            {
                if (c.Branches[i].Name != child.Branches[i].Name)
                {
                    branch = null;
                    return false;
                }
            }

            // Commit has one child commit, use that child commit branch
            branch = child.Branch;
            return true;
        }

        branch = null;
        return false;
    }


    private bool TryIsChildAmbiguousBranch(WorkCommit c, out WorkBranch? branch)
    {
        foreach (var cc in c.Children)
        {
            if (cc.Branch != null && cc.Branch.IsAmbiguousBranch)
            {   // one of the commit children is a ambiguous branch
                branch = cc.Branch;
                return true;
            }
        }

        branch = null;
        return false;
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
            c.FirstParent.TryAddToBranches(c.Branch);
        }
    }

    private WorkBranch? AddNamedBranch(WorkRepo repo, WorkCommit c, string branchName)
    {
        var branch = new WorkBranch(
            name: $"{branchName}:{c.Sid}",
            displayName: $"{branchName}@{c.Sid}",
            tipID: c.Id);

        repo.Branches.Add(branch);
        return branch;
    }


    internal WorkBranch AddAmbiguousBranch(WorkRepo repo, WorkCommit c)
    {
        WorkBranch branch = new WorkBranch(
            name: $"ambiguous:{c.Sid}",
            displayName: $"ambiguous@{c.Sid}",
            tipID: c.Id);

        branch.IsAmbiguousBranch = true;

        foreach (var cc in c.Children)
        {
            if (cc.Branch == null)
            {
                continue;
            }

            branch.AmbiguousBranches.Add(cc.Branch);
        }

        repo.Branches.Add(branch);
        return branch;
    }


    private void MergeAmbiguousBranches(WorkRepo repo)
    {
        var ambiguousBranches = repo.Branches.Where(b => b.IsAmbiguousBranch).ToList();

        foreach (var b in ambiguousBranches)
        {
            var tip = repo.CommitsById[b.TipID];

            // Determine the parent commit this branch was created from
            var otherId = b.BottomID;
            var parentBranchCommit = repo.CommitsById[b.BottomID].FirstParent;
            if (parentBranchCommit != null)
            {
                otherId = parentBranchCommit.Id;
            }

            // Find the tip of the ambiguous commits (and the next commit)
            WorkCommit? ambiguousTip = null;
            WorkCommit? ambiguousSecond = null;
            for (var c = tip; c != null && c.Id != otherId; c = c.FirstParent)
            {
                if (c.Branch != b)
                {   // Still a normal branch commit (no longer part of the ambiguous branch)
                    continue;
                }

                // tip of the ambiguous commits
                ambiguousTip = c;
                ambiguousSecond = c.FirstParent;
                c.IsAmbiguousTip = true;
                c.IsAmbiguous = true;

                // Determine the most likely branch (branch of the oldest child)
                var oldestChild = c.Children[0];
                List<WorkBranch> childBranches = new List<WorkBranch>();
                foreach (var cc in c.Children)
                {
                    if (cc.AuthorTime > oldestChild.AuthorTime)
                    {
                        oldestChild = cc;
                    }
                    childBranches.Add(c.Branch!);
                }
                c.Branch = oldestChild.Branch!;
                c.Branch.AmbiguousTipId = c.Id;
                c.Branch.AmbiguousBranches = childBranches;
                c.Branch.BottomID = c.Id;
                break;
            }

            // Set the branch of the rest of the ambiguous commits to same as the tip
            for (var c = ambiguousSecond; c != null && c.Id != otherId; c = c.FirstParent)
            {
                c.Branch = ambiguousTip!.Branch!;
                c.Branch.BottomID = c.Id;
                c.IsAmbiguous = true;
            }
        }

        // Removing the ambiguous branches (no longer needed)
        var bs = repo.Branches.Where(b => !b.IsAmbiguousBranch).ToList();
        repo.Branches.Clear();
        repo.Branches.AddRange(bs);
    }


    private void DetermineBranchHierarchy(WorkRepo repo)
    {
        foreach (var b in repo.Branches)
        {
            // var bs := branchesChildren[b.BaseName()]
            // b.IsSetAsParent = len(bs) > 0

            if (b.BottomID == "")
            {
                // For branches with no own commits (multiple tips to same commit)
                b.BottomID = b.TipID;
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

        var rootBranch = repo.Branches.First(b => b.ParentBranch == null);
        rootBranch.IsMainBranch = true;
    }
}

