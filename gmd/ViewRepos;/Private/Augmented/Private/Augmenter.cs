using GitCommit = gmd.Git.Commit;
using GitBranch = gmd.Git.Branch;

namespace gmd.ViewRepos.Private.Augmented.Private;

// Augmenter augments repos of git repo information, The augmentations 
// adds information not available in git directly, but can be inferred by parsing the 
// git information. 
// Examples of augmentation is which branch a commits belongs to and the hierarchical structure
// of branches. 
interface IAugmenter
{
    Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo, int partialMax);
}

class Augmenter : IAugmenter
{
    readonly string[] DefaultBranchPriority = new string[] { "origin/main", "main", "origin/master", "master" };

    readonly IBranchNameService branchNameService;


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
                    if (b.IsCurrent)
                    {
                        remoteBranch.IsLocalCurrent = true;
                    }
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
        {   // Commit has no branch and no children, but has a merge child.
            // The commit is a tip of a deleted branch. It might be a deleted remote branch.
            // Lets try determine branch name based on merge child's subject
            // or use a generic branch name based on commit id
            return branch!;
        }
        else if (TryIsMergedDeletedBranchTip(repo, c, out branch))
        {   // Commit has no branch and no children, but has a merge child.
            // The commit is a tip of a deleted remote branch.
            // Lets try determine branch name based on merge child's subject 
            // or use a generic branch name based on commit id
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
        else if (TryHasMultipleChildrenWithOneLikelyBranch(c, out branch))
        {   // Commit multiple possible git branches but has a child, which has a likely known branch, use same branch
            return branch!;
        }
        else if (TryIsChildAmbiguousCommit(c, out branch))
        {   // one of the commit children is a an ambigouous commit, reuse same branch
            return branch!;
        }

        // Commit, has several possible branches, and we could not determine which branch is best,
        // create a new ambiguous branch. Later commits may fix this by parsing subjects of later
        // commits, or the user has to manually set the branch.
        return AddAmbiguousCommit(repo, c);
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
            c.IsAmbiguous = c.Children[0].IsAmbiguous;
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

                if (name == mergeChildBranch.CommonName)
                {
                    branch = mergeChildBranch;
                    return true;
                }

                branch = AddNamedBranch(repo, c, name);
                return true;

            }

            // could not parse a name from any of the merge children, use id named branch
            branch = AddNamedBranch(repo, c);
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
            branch = AddNamedBranch(repo, c);
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
            c.IsAmbiguous = c.Children[0].IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
    }

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
        if (c.Children.Count(c => c.IsLikely) == 1)
        {
            // commit has only one child with a likely branch
            var child = c.Children.First(c => c.IsLikely);
            branch = child.Branch;
            c.IsAmbiguous = child.IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
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

            if (branch != null && branch.TipID == c.Id)
            {  // The commit is branch tip, we should not find higher/previous commit up, since tip would move up
                c.Branch = branch;
                c.TryAddToBranches(branch);
                c.IsLikely = true;
                return true;
            }

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
                current = c;
                while (true)
                {
                    if (current.Children.Count == 0)
                    {   // if no child, there are no commits above
                        break;
                    }
                    if (current.Children.Count > 1)
                    {   // if multiple children, we do not know which child to follow up.
                        break;
                    }
                    var firstChild = current.Children[0];
                    if (!firstChild.IsAmbiguous)
                    {   // We only go upp if child up is ambigous as well
                        break;
                    }

                    // Step the ambiguous branch bottom upp since current will belong to other branch
                    firstChild.Branch!.BottomID = firstChild.Id;

                    if (branch != null && current.Id == branch.TipID)
                    {   // Found a commit with the branch tip
                        break;
                    }

                    // Go to upp to child
                    current = current.Children[0];
                }
            }

            if (branch != null)
            {
                for (; current != null && current != c.FirstParent; current = current.FirstParent)
                {
                    current.Branch = branch;
                    current.IsAmbiguous = false;
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
        return c.Branches.Find(b => b.CommonName == name);
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
            c.IsAmbiguous = child.IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
    }


    private bool TryIsChildAmbiguousCommit(WorkCommit c, out WorkBranch? branch)
    {
        foreach (var cc in c.Children)
        {
            if (cc.IsAmbiguous && cc.Branch != null)
            {   // one of the commit children is a ambiguous commit
                c.IsAmbiguous = true;
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

    private WorkBranch? AddNamedBranch(WorkRepo repo, WorkCommit c, string name = "")
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
        (var branch, var ambiguousBranches) = GetLikelyBranches(c);

        c.IsAmbiguous = true;
        c.Branch = branch;
        c.Branch.AmbiguousTipId = c.Id;
        c.Branch.AmbiguousBranches = ambiguousBranches;

        return branch;
    }

    (WorkBranch, List<WorkBranch>) GetLikelyBranches(WorkCommit c)
    {
        if (!c.Children.Any())
        {
            // Commit has no children (i.e.must a branch tip with multiple possible tipps)
            // Prefer remote branch if possible
            var likelyBranch1 = c.Branches.FirstOrDefault(b => b.IsRemote);
            if (likelyBranch1 == null)
            {   // No remote branch, just take one branch
                likelyBranch1 = c.Branches.First();
            }

            var ambiguousBranches1 = c.Branches;
            return (likelyBranch1, ambiguousBranches1);
        }

        var likelyChild = c.Children.FirstOrDefault(c => c.IsLikely);
        if (likelyChild != null)
        {
            var branch = likelyChild.Branch!;
            var ambiguousBranches3 = c.Children.Select(c => c.Branch!).ToList();

            return (branch, ambiguousBranches3);
        }

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

        var likelyBranch2 = oldestChild.Branch!;
        var ambiguousBranches2 = childBranches;

        return (likelyBranch2, ambiguousBranches2);
    }


    internal WorkBranch AddAmbiguousBranch(WorkRepo repo, WorkCommit c)
    {
        var name = $"ambiguous:{c.Sid}";
        WorkBranch branch = new WorkBranch(
            name: name,
            commonName: name,
            displayName: name,
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

        var rootBranch = repo.Branches.First(b => b.ParentBranch == null);
        rootBranch.IsMainBranch = true;
        if (rootBranch.LocalName != "")
        {
            var rootLocalBranch = repo.Branches.First(b => b.Name == rootBranch.LocalName);
            rootLocalBranch.IsMainBranch = true;
        }
    }
}

