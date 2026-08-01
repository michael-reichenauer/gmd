namespace gmd.Server.Private.Augmented.Private;

// The last stages of the branch structure pipeline, which relate the branches to each other once
// every commit has a branch: which branch a branch was branched out of, which branch is the trunk
// of the repository, and the ancestors that follow from those.
interface IBranchHierarchyService
{
    void DetermineBranchHierarchy(WorkRepo repo);
    void DetermineRootBranch(WorkRepo repo);
    void DetermineAncestors(WorkRepo repo);
}

class BranchHierarchyService : IBranchHierarchyService
{
    // Determine the parent/child relationship between branches, and which branch is the main branch
    // A child branch is branches from a parent branch
    public void DetermineBranchHierarchy(WorkRepo repo)
    {
        foreach (var b in repo.Branches.Values)
        {
            if (b.IsAmbiguousBranch && b.AmbiguousTip != null)
            { // Set ambiguous tip on commit
                b.AmbiguousTip.IsAmbiguousTip = true;
            }

            if (b.RemoteName != "")
            { // For a local branch with a remote branch, the remote branch should be parent.
                var remoteBranch = repo.Branches[b.RemoteName];
                b.ParentBranch = remoteBranch;

                var bb = repo.CommitsById[b.BottomID];
                if (
                    b.TipID != b.BottomID
                    && bb.FirstParent?.Branch != remoteBranch
                    && remoteBranch.TipID != bb.FirstParent?.Id
                )
                { // The bottom commit of the local branch does not have the remote branch as first parent
                    Log.Warn(
                        $"Branch {b.Name} has unexpected bottom branch '{bb.FirstParent?.Branch}', expected: {b.ParentBranch}"
                    );
                }
                continue;
            }

            var bottom = repo.CommitsById[b.BottomID];
            if (b.TipID == b.BottomID && bottom.Branch != b)
            { // Branch does not own the bottom (or tip) commit, i.e. a branch pointer to another branch with no own commits yet
                b.ParentBranch = bottom.Branch;
                continue;
            }
            if (bottom.FirstParent == null)
            { // Branch bottom commit has no first parent, is a root branch like e.g. main/master, or doc branch
                // Log.Warn($"Branch {b.Name} has bottom commit {bottom.Sid} with no first parent");
                continue;
            }

            // Branch bottom commit has a first parent, use that as parent branch (this is the normal case)
            b.ParentBranch = bottom.FirstParent.Branch;
        }
    }

    // Determine the root branch, i.e. the branch that has no parent branch, and which branch is the main branch
    // A repository can have several root branches, e.g. the doc branch in GitHub. So we try to determine
    // the most likely root branch, and set that as the main branch.
    public void DetermineRootBranch(WorkRepo repo)
    {
        // A repo can have several root branches (e.g. the doc branch in GitHub). If the repo is truncated
        // we need to remove the truncated branch and redirect all its children to the most likely root branch
        repo.Branches.TryGetValue(WellKnownBranches.TruncatedName, out var truncatedBranch);
        var rootBranches = repo
            .Branches.Values.Where(b =>
                b != truncatedBranch // The truncated branch is a scaffold, it is removed just below
                && (b.ParentBranch == null || b.ParentBranch == truncatedBranch)
            )
            .ToList();
        if (!rootBranches.Any())
            return; // No root branches (empty repo)

        var rootBranch = SelectRootBranch(repo, rootBranches);

        if (truncatedBranch != null)
        { // Remove the truncated branch and redirect all its children to the root branch
            var truncatedCommit = repo.CommitsById[gmd.Server.Repo.TruncatedLogCommitId];
            truncatedCommit.Branch = rootBranch;
            rootBranch.ParentBranch = null;
            rootBranch.BottomID = truncatedCommit.Id;
            repo.Branches.Remove(truncatedBranch.Name);

            // Redirect all branches that has the truncated branch as parent to the root branch instead
            repo.Branches.Values.Where(b => b.ParentBranch == truncatedBranch)
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

    // Select the branch that is most likely the trunk of the repository. A repo usually has just
    // one root branch, but orphan branches (e.g. a gh-pages or doc branch) are root branches too,
    // since their history is unrelated and thus has its own first commit.
    static WorkBranch SelectRootBranch(WorkRepo repo, IReadOnlyList<WorkBranch> rootBranches)
    {
        // A branch with a well known main branch name is the trunk, whatever its history looks like
        foreach (var name in WellKnownBranches.MainNamePriority)
        {
            var branch = rootBranches.FirstOrDefault(b => b.Name == name);
            if (branch != null)
                return branch;
        }

        // No branch is named like a main branch, so the trunk is the branch whose history reaches
        // furthest back, since the other root branches were started later in the life of the repo.
        // Not the number of commits, an orphan branch can easily have more of them than the trunk.
        // The name only breaks the tie of two histories starting at the same time, so that the
        // choice never depends on the order git happened to list the branches in.
        return rootBranches
            .OrderBy(b => repo.CommitsById[b.BottomID].AuthorTime)
            .ThenBy(b => b.Name, StringComparer.Ordinal)
            .First();
    }

    public void DetermineAncestors(WorkRepo repo)
    {
        int circularAncestors = 0;
        foreach (var b in repo.Branches.Values)
        {
            var ancestor = b.ParentBranch;
            while (ancestor != null)
            {
                // if (b.Ancestors.Contains(ancestor))
                // {   // Debug code in case of circular ancestors (should no happen)
                //     Log.Error($"Branch {b.Name} has circular ancestor {ancestor.Name}");
                //     Log.Error("Ancestors: " + b.Ancestors.Select(a => a.Name).Join(","));
                //     b.IsCircularAncestors = true;
                //     circularAncestors++;
                //     break;
                // }
                b.Ancestors.Add(ancestor);
                ancestor = ancestor.ParentBranch;
            }
        }

        if (circularAncestors > 0)
            Log.Error($"Repo has {circularAncestors} circular ancestors");
    }
}
