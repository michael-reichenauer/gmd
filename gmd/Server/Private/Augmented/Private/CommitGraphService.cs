namespace gmd.Server.Private.Augmented.Private;

// The first stages of the branch structure pipeline, which make the commit graph of a work repo
// traversable for the stages that follow. This is the graph of commits and branch tips, not the
// branch graph the UI draws.
interface ICommitGraphService
{
    void SetGitBranchTipsOnCommits(WorkRepo repo);
    void SetCommitParentsAndChildren(WorkRepo repo);
}

class CommitGraphService : ICommitGraphService
{
    readonly IBranchNameService branchNameService;

    public CommitGraphService(IBranchNameService branchNameService)
    {
        this.branchNameService = branchNameService;
    }

    // Set branch tips for branches on their tip commits
    // Remove branches that are do not have an existing tip id in the repo (e.g. deleted branches or truncated)
    public void SetGitBranchTipsOnCommits(WorkRepo repo)
    {
        List<string> notFoundBranches = new List<string>();

        foreach (var b in repo.Branches.Values)
        {
            if (!repo.CommitsById.TryGetValue(b.TipID, out var tip))
            { // A branch tip id, which commit id does not exist in the repo (deleted branch or truncated repo)
                // Store that branch name so it can be removed from the list later
                notFoundBranches.TryAdd(b.Name);
                continue;
            }

            if (!b.IsDetached)
            { // Adding the branch to the branch tip commit (unless detached, handled separately later)
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
    public void SetCommitParentsAndChildren(WorkRepo repo)
    {
        foreach (var c in repo.Commits)
        {
            // Parsing commit subject to if possible determine likely branch name (result cached in service)
            branchNameService.ParseCommitSubject(c);

            if (c.ParentIds.Count == 2 && branchNameService.IsPullMerge(c))
            { // if the commit is a pull merge (remote commits merged into the local branch),
                // The order of parents are switched, to make the branch structure more logical.
                // So the first parent is the now remote branch and second parent the local branch
                // This makes the local commits to look like they where merged into the remote
                // branch, instead of existing remote commits merged/moved into the local branch,
                // which would make the remote branch alter commit order whenever local commits
                // are not updated to remote server in time.
                (c.ParentIds[1], c.ParentIds[0]) = (c.ParentIds[0], c.ParentIds[1]);
            }

            if (c.ParentIds.Any() && repo.CommitsById.TryGetValue(c.ParentIds[0], out var firstParent))
            { // Commit has a first parent, and that parents children is updated with this commit
                c.FirstParent = firstParent;
                firstParent.FirstChildren.Add(c);
                firstParent.AllChildIds.Add(c.Id);
                firstParent.FirstChildIds.Add(c.Id);
            }

            if (c.ParentIds.Count > 1 && repo.CommitsById.TryGetValue(c.ParentIds[1], out var mergeParent))
            { // Commit has a merge parent, that parents merge children is updated with this commit
                c.MergeParent = mergeParent;
                mergeParent.MergeChildren.Add(c);
                mergeParent.AllChildIds.Add(c.Id);
                mergeParent.MergeChildIds.Add(c.Id);
            }
        }
    }
}
