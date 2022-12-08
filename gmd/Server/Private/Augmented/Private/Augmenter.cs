using GitCommit = gmd.Git.Commit;
using GitBranch = gmd.Git.Branch;

namespace gmd.Server.Private.Augmented.Private;

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
        Threading.AssertIsOtherThread();
        WorkRepo repo = new WorkRepo(gitRepo.TimeStamp, gitRepo.Path, ToStatus(gitRepo));

        SetAugBranches(repo, gitRepo);
        SetAugCommits(repo, gitRepo, partialMax);
        SetCommitBranches(repo, gitRepo);
        SetAugTags(repo, gitRepo);

        return repo;
    }


    Status ToStatus(GitRepo repo)
    {
        var s = repo.Status;
        return new Status(s.Modified, s.Added, s.Deleted, s.Conflicted,
          s.IsMerging, s.MergeMessage, s.ModifiedFiles, s.AddedFiles, s.DeleteddFiles, s.ConflictsFiles);
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

            commit.Index = i;
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
            pc.Index = repo.Commits.Count;
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


    void SetAugTags(WorkRepo repo, GitRepo gitRepo)
    {
        gitRepo.Tags.ForEach(t =>
        {
            var tag = new Tag(t.Name, t.CommitId);
            if (repo.CommitsById.TryGetValue(t.CommitId, out var c))
            {
                c.Tags.Add(tag);
            }
        });
    }


    void SetCommitBranches(WorkRepo repo, GitRepo gitRepo)
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
                invalidBranches.Add(b.Name);
                continue;
            }

            // Adding the branch to the branch tip commit
            tip.TryAddToBranches(b);
            tip.BranchTips.Add(b.Name);
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
                // Adding the child branches to the parent branches (inherited down)
                firstParent.TryAddToBranches(c.Branches);
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

    void DetermineCommitBranches(WorkRepo repo, GitRepo gitRepo)
    {
        foreach (var c in repo.Commits)
        {
            var branch = DetermineCommitBranch(repo, c, gitRepo);
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

    WorkBranch DetermineCommitBranch(WorkRepo repo, WorkCommit c, GitRepo gitRepo)
    {
        WorkBranch? branch;
        if (TryIsBranchSetByUser(repo, gitRepo, c, out branch))
        {   // Commit branch was set/determined by user, 
            return branch!;
        }
        else if (TryHasOnlyOneBranch(c, out branch))
        {   // Commit only has one branch, it must have been an actual branch tip originally, use that
            return branch!;
        }
        else if (TryIsLocalRemoteBranch(c, out branch))
        {
            // Commit has only local and its remote branch, prefer remote remote branch
            return branch!;
        }
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
        else if (TryHasOnlyOneChild(c, out branch))
        {   // Commit has one child commit and not merge commits, reuse that child commit branch
            return branch!;
        }
        else if (TryHasMultipleChildrenWithOneLikelyBranch(c, out branch))
        {   // Commit multiple possible git branches but has a child, which has a likely known branch, use same branch
            return branch!;
        }
        else if (TryHasBranchNameInSubject(repo, c, out branch))
        {   // A branch name could be parsed form the commit subject or a child subject.
            // The commit will be set to that branch and also if above (first child) commits have
            // ambiguous branches, the will be reset to same branch as well. This will 'repair' branch
            // when a parsable commit subjects are encountered.
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

    bool TryIsBranchSetByUser(WorkRepo repo, GitRepo gitRepo, WorkCommit c, out WorkBranch? branch)
    {
        branch = null;

        if (!gitRepo.MetaData.TryGet(c.Sid, out var branchDisplayName, out var isSetByUser))
        {
            return false;
        }

        var childrenBranches = c.Children.Select(cc => cc.Branch);
        var tipBranches = c.BranchTips.Select(n => repo.Branches.First(b => b.Name == n));
        var branches = childrenBranches.Concat(tipBranches).Where(b => b != null && b.DisplayName == branchDisplayName);
        if (branches.Any())
        {
            var remote = branches.FirstOrDefault(b => b != null && b.IsRemote);
            if (remote != null)
            {
                c.IsBranchSetByUser = isSetByUser;
                branch = remote;
                return true;
            }

            c.IsBranchSetByUser = isSetByUser;
            branch = branches.First();
            return true;
        }

        return false;
    }

    bool TryHasOnlyOneBranch(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Branches.Count == 1)
        {  // Commit only has one branch, it must have been an actual branch tip originally, use that
            branch = c.Branches[0];
            return true;
        }

        branch = null;
        return false;
    }

    bool TryIsLocalRemoteBranch(WorkCommit c, out WorkBranch? branch)
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


    bool TrySameChildrenBranches(WorkCommit c, out WorkBranch? branch)
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
                var mergeChild = c.MergeChildren[0];

                if (branchNameService.IsPullMerge(mergeChild))
                {
                    if (mergeChild.Branch!.DisplayName == name)
                    {
                        // The merge child is a pull merge, so this commit is on a "dead" branch part,
                        // which used to be the local branch of the pull merge commit.
                        // We need to connect this branch with the actual branch.
                        var pullMergeBranch = mergeChild.Branch;
                        branch = AddPullMergeBranch(repo, c, name, pullMergeBranch!);
                        pullMergeBranch!.PullMergeBranches.TryAdd(branch);
                        return true;
                    }
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
        if (c.Branches.Count == 0 && c.Children.Count == 1 && !c.Children[0].IsAmbiguous)
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

    private bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {

        branch = null;
        return false;

        string name = branchNameService.GetBranchName(commit.Id);
        if (name == "")
        {
            return false;
        }

        // A branch name could be parsed form the commit subject or a merge child subject.
        branch = TryGetBranchFromName(commit, name);
        if (branch == null)
        {   // Found no suitable branch
            return false;
        }

        // Lets use that as a branch name and also let children (commits above)
        // use that branch if they are an ambiguous branch

        if (branch.TipID == commit.Id)
        {  // The commit is branch tip, we should not find higher/previous commit up, since tip would move up  
            commit.Branch = branch;
            commit.IsLikely = true;
            commit.TryAddToBranches(branch);
            return true;
        }


        // Lets iterate upp (first child) as long as commits are ambiguous and the branch exists
        var namedBranch = branch;
        var current = commit;
        while (true)
        {
            var child = current.Children
                .Where(c => c.IsAmbiguous)
                .FirstOrDefault(c =>
                    c.Branch! == namedBranch ||
                    c.Branches.Contains(namedBranch) ||
                    c.Branch!.AmbiguousBranches.Contains(namedBranch));
            if (child == null)
            {   // No ambiguous commit with that branch, cannot step up further
                break;
            }

            // // Step the ambiguous branch bottom upp since current belongs branch
            // child.Branch!.BottomID = child.Id;

            if (current.Id == branch.TipID)
            {   // Found the commit tip of the branch no commits above that.
                break;
            }

            // Go to upp to child
            current = child;
        }

        if (current.Children.Any() &&
            null == current.Children.FirstOrDefault(c => !c.IsAmbiguous && c.Branch == namedBranch))
        {   // Failed to reach last not ambiguous branch part of named branch
            return false;
        }

        branch.AmbiguousTipId = "";
        branch.IsAmbiguousBranch = false;
        branch.AmbiguousBranches.Clear();

        if (current.Branch != null && current.Branch != branch)
        {   // Need to move bottom of current branch upp to current child since current will
            // belong to other branch
            if (current.Children.Any())
            {   // Sett branch bottom to child
                var firstOtherChild = current.Children.FirstOrDefault(c => c.Branch == current.Branch);
                if (firstOtherChild != null)
                {
                    current.Branch!.BottomID = firstOtherChild.Id;
                }
                else
                {   // Must have been a tip on current
                    current.Branch!.BottomID = current.Id;
                }
            }
            else
            {   // Has no children, set to current
                current.Branch!.BottomID = current.Id;
            }
        }

        do
        {
            current.Branch = branch;
            current.IsAmbiguous = false;
            current.IsAmbiguousTip = false;
            current.IsLikely = true;
            current.TryAddToBranches(branch);

            if (current.FirstParent != null &&
                current.FirstParent.Branch != null &&
                current.FirstParent.Branch != branch)
            {
                current.FirstParent.Branch.BottomID = current.Id;
            }
            current = current.FirstParent;

        } while (current != commit && current != null);

        return true;




        // // Lets iterate upp (first child) as long as commits are ambiguous and the branch exists
        // var current = c;
        // var currentBranch = branch;
        // while (true)
        // {
        //     if (current.Branch != null && current.Branch.AmbiguousTipId == current.Id)
        //     {   // Branch is no longer ambiguous all ambigous commits have been cleared.
        //         current.Branch.AmbiguousTipId = "";
        //         current.Branch.IsAmbiguousBranch = false;
        //         current.Branch.AmbiguousBranches.Clear();
        //     }

        //     current.Branch = branch;
        //     current.IsAmbiguous = false;
        //     current.IsAmbiguousTip = false;
        //     current.IsLikely = true;
        //     current.TryAddToBranches(branch);

        //     var child = current.Children
        //         .FirstOrDefault(cc => cc.IsAmbiguous && (cc.Branch! == currentBranch)
        //             || null != cc.Branch!.AmbiguousBranches.FirstOrDefault(bb => bb == currentBranch));
        //     if (child == null)
        //     {   // No ambiguous commit with that branch, cannot step up further
        //         return true;
        //     }

        //     // Step the ambiguous branch bottom upp since current belongs branch
        //     child.Branch!.BottomID = child.Id;

        //     if (current.Id == branch.TipID)
        //     {   // Found the commit tip of the branch no commits above that.
        //         return true;
        //     }

        //     // Go to upp to child
        //     current = child;
        // }
    }

    // bool SetBranchForAmbiguousCommits(WorkRepo repo, WorkBranch branch, WorkCommit endCommit)
    // {
    //     // Found an existing branch with that name, set lowest known commit to the bottom
    //     // of that known branch
    //     if (!repo.CommitsById.TryGetValue(branch.BottomID, out var current))
    //     {
    //         return false;
    //     }

    //     // Step current down one step, since current is already the last commit on that branch
    //     current = current.FirstParent;

    //     if (current != null)
    //     {
    //         // Current is now the first commit after the c.Subject named brach
    //         // Adjust the current child branch bottom id to be the 'first' child of current
    //         var otherChild = current.Children.FirstOrDefault(cc => cc.Branch == current.Branch);
    //         if (otherChild != null)
    //         {
    //             current.Branch!.BottomID = otherChild.Id;
    //         }

    //         if (current.Branch?.AmbiguousTipId == current.Id)
    //         {   // Branch is no longer ambiguous all ambigous commits have been cleared.
    //             current.Branch.AmbiguousTipId = "";
    //             current.Branch.IsAmbiguousBranch = false;
    //             current.Branch.AmbiguousBranches.Clear();
    //         }
    //     }

    //     // Set all commits to the branch below the current until reaching the commit after c
    //     for (; current != null && current != endCommit.FirstParent; current = current.FirstParent)
    //     {
    //         current.Branch = branch;
    //         current.IsAmbiguous = false;
    //         current.IsAmbiguousTip = false;
    //         current.TryAddToBranches(branch);
    //         current.IsLikely = true;
    //     }

    //     return true;
    // }


    WorkBranch? TryGetBranchFromName(WorkCommit c, string name)
    {
        // Try find a live git branch with the name or remoteName
        var remoteName = $"origin/{name}";
        var branch = c.Branches.FirstOrDefault(b => b.Name == name || b.Name == remoteName);
        if (branch != null)
        {   // Found a branch, if the branch has a remote branch, try find that
            if (branch.RemoteName != "")
            {   // Branch has a remote, lets use that if possible
                var remoteBranch = c.Branches.FirstOrDefault(b => b.Name == branch.RemoteName);
                if (remoteBranch != null)
                {
                    return remoteBranch;
                }
            }

            return branch;
        }

        // Try find a branch with the display name
        branch = c.Branches.Find(b => b.DisplayName == name);
        if (branch != null)
        {
            return branch;
        }

        if (c.Children.Count == 1)
        {   // Check if the child has an ambiguous branch with possible branches.
            branch = c.Children[0].Branch!.AmbiguousBranches.FirstOrDefault(b => b.DisplayName == name);
        }

        if (branch != null)
        {   // Found a branch, if the branch has a remote branch, try find that
            if (branch.RemoteName != "")
            {   // Branch has a remote, lets use that if possible
                var remoteBranch = c.Children[0].Branch!.AmbiguousBranches.FirstOrDefault(b => b.Name == branch.RemoteName);
                if (remoteBranch != null)
                {
                    return remoteBranch;
                }
            }

            return branch;
        }

        return branch;
    }


    private bool TryHasOnlyOneChild(WorkCommit c, out WorkBranch? branch)
    {
        if (c.Children.Count == 1)//  Why is was this needed??? && c.MergeChildren.Count == 0)
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
        branch = null;
        var ambiguousChild = c.Children.FirstOrDefault(c => c.IsAmbiguous);
        if (ambiguousChild == null)
        {   // No ambiguous child
            return false;
        }

        branch = ambiguousChild.Branch!;
        var amBranch = branch;

        // If more ambiguous children, merge in their sub branches as well
        c.Children
            .Where(cc => cc.IsAmbiguous && cc != ambiguousChild)
            .ForEach(cc => cc.Branch!.AmbiguousBranches
                .ForEach(b =>
                {
                    if (!amBranch.AmbiguousBranches.Contains(b))
                    {
                        amBranch.AmbiguousBranches.Add(b);
                    }
                }));

        c.IsAmbiguous = true;
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
            c.FirstParent.TryAddToBranches(c.Branch);
        }
    }

    private WorkBranch AddPullMergeBranch(
        WorkRepo repo, WorkCommit c, string name, WorkBranch pullMergeBranch)
    {
        var branchName = name != "" ? $"{name}:{c.Sid}" : $"branch:{c.Sid}";
        var displayName = name != "" ? name : $"branch@{c.Sid}";
        var branch = new WorkBranch(
            name: branchName,
            commonName: pullMergeBranch.CommonName,
            displayName: displayName,
            tipID: c.Id);
        branch.PullMergeBranch = pullMergeBranch;

        repo.Branches.Add(branch);
        return branch;
    }

    private WorkBranch AddNamedBranch(WorkRepo repo, WorkCommit c, string name = "")
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
        if (ambiguousBranches.Count < 2)
        {
            Log.Info("ambiguous count <2");
            (var bb, var amb) = GetLikelyBranches(c);
        }

        c.IsAmbiguous = true;
        c.Branch = branch;
        c.Branch.IsAmbiguousBranch = true;
        c.Branch.AmbiguousTipId = c.Id;
        c.Branch.AmbiguousBranches = ambiguousBranches;

        return branch;
    }

    (WorkBranch, List<WorkBranch>) GetLikelyBranches(WorkCommit commit)
    {
        var ambiguousBranches = commit.Branches;

        if (!commit.Children.Any())
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
                .DistinctBy(b => b.Name).ToList();

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
        ambiguousBranches = ambiguousBranches.Concat(childBranches).DistinctBy(b => b.Name).ToList();

        return (likelyBranch2, ambiguousBranches);
    }


    private void DetermineBranchHierarchy(WorkRepo repo)
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
        var rootBranches = repo.Branches.Where(b => b.ParentBranch == null).ToList();
        if (!rootBranches.Any())
        {   // No root branches (empty repo)
            return;
        }

        // Select most likley root branch (but prioritize)
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

        rootBranch.IsMainBranch = true;
        if (rootBranch.LocalName != "")
        {
            var rootLocalBranch = repo.Branches.First(b => b.Name == rootBranch.LocalName);
            rootLocalBranch.IsMainBranch = true;
        }
    }
}

