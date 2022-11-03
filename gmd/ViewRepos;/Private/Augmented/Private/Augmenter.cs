
using GitCommit = gmd.Utils.Git.Commit;
using GitBranch = gmd.Utils.Git.Branch;

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IAugmenter
{
    Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo, int partialMax);
}

class Augmenter : IAugmenter
{
    readonly string[] DefaultBranchPriority = new string[] { "origin/main", "main", "origin/master", "master" };


    public Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo, int partialMax)
    {
        return Task.Run(() => GetAugRepo(gitRepo, partialMax));

    }

    private WorkRepo GetAugRepo(GitRepo gitRepo, int partialMax)
    {
        var repo = new WorkRepo();
        SetAugBranches(repo, gitRepo);
        SetAugCommits(repo, gitRepo, partialMax);
        SetCommitBranches(repo);

        return repo;
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
            tip.AddToBranchesIfNotExists(b);
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
            // Add this !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // h.branchNames.parseCommit(c)
            // if len(c.ParentIDs) == 2 && h.branchNames.isPullMerge(c) {
            // 	// if the commit is a pull merger, we do switch the order of parents
            // 	// So the first parent is the remote branch and second parent the local branch
            // 	c.ParentIDs = []string{c.ParentIDs[1], c.ParentIDs[0]}
            // }

            if (c.ParentIds.Count > 0 && repo.CommitsById.TryGetValue(c.ParentIds[0], out var firstParent))
            {
                c.FirstParent = firstParent;
                firstParent.Children.Add(c);
                firstParent.ChildIds.Add(c.Id);
                // Adding the child branches to the parent branches (inherited down)
                firstParent.AddToBranchesIfNotExists(c.Branches.ToArray());
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
            c.AddToBranchesIfNotExists(c.Branch);

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
        else if (TryIsChildAmbiguousBranch(c, out branch))
        {
            // one of the commit children is a ambiguous branch, reuse same ambiguous branch
            return branch!;
        }

        // Commit, has several possible branches, and we could not determine which branch is best,
        // create a new ambiguous branch. Later commits may fix this by parsing subjects of later
        // commits, or the user has to manually set the branch.
        return AddAmbiguousBranch(repo, c);
    }




    private bool TryHasOnlyOneBranch(WorkCommit c, out WorkBranch? branch)
    {
        branch = null;
        if (c.Branches.Count != 1)
        {
            return false;
        }

        // Commit only has one branch, it must have been an actual branch tip originally, use that
        branch = c.Branches[0];
        return true;
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
            c.FirstParent.AddToBranchesIfNotExists(c.Branch);
        }
    }

    internal WorkBranch AddAmbiguousBranch(WorkRepo repo, WorkCommit c)
    {
        WorkBranch b = new WorkBranch(
            name: $"ambiguous:{c.Sid}",
            displayName: $"ambiguous@{c.Sid}",
            tipID: c.Id);

        b.IsAmbiguousBranch = true;

        foreach (var cc in c.Children)
        {
            if (cc.Branch == null)
            {
                continue;
            }

            b.AmbiguousBranches.Add(cc.Branch);
        }

        repo.Branches.Add(b);
        return b;
    }
}

