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
        List<string> notFoundBranches = [];

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
        notFoundBranches.ForEach(n => RemoveBranch(repo, n));
    }

    // Removes a branch and parts it from the branch it pairs with, which the later stages and the UI
    // look up by name: a local branch whose remote branch is left out, e.g. with its tip below a
    // truncated log, is a branch of its own, as one whose remote branch was deleted is (see
    // Augmenter), and a remote branch whose local branch is left out has none.
    static void RemoveBranch(WorkRepo repo, string name)
    {
        var b = repo.Branches[name];
        repo.Branches.Remove(name);

        if (b.IsRemote && b.LocalName != "" && repo.Branches.TryGetValue(b.LocalName, out var local))
        {
            local.RemoteName = "";
            local.PrimaryName = local.Name;
            local.IsPrimary = true;
            local.RelatedBranches.Add(local);
        }
        else if (!b.IsRemote && b.RemoteName != "" && repo.Branches.TryGetValue(b.RemoteName, out var remote))
        {
            remote.LocalName = "";
            remote.IsLocalCurrent = false;
            remote.RelatedBranches.Remove(b);
        }
    }

    // Update a commit with parents and children to be able to traverse the commit graph
    // Also swap parent order for pull merges and foxtrot merges, to make branch structure more logical
    // and persistent
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
                c.IsParentsSwapped = true;
            }
        }

        SwapFoxtrotMerges(repo);

        foreach (var c in repo.Commits)
        {
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

    // A foxtrot merge is main merged into a branch to bring it up to date, "Merge branch 'main' into
    // feature", after which main was fast-forwarded to that merge, e.g. by 'git merge feature' on main.
    // Git's first parent of the merge is then feature's commit, so main's line would run through
    // feature, and main's own previous commit would be drawn as merged in. Found by walking main's line
    // from its tips, and swapped like a pull merge, which keeps main's commits on its line and draws
    // feature as merged into it. Only on main's line: the same subject on feature's line is what it
    // says it is.
    void SwapFoxtrotMerges(WorkRepo repo)
    {
        HashSet<string> walked = [];
        foreach (var trunk in repo.Branches.Values.Where(b => WellKnownBranches.MainNamePriority.Contains(b.Name)))
        {
            repo.CommitsById.TryGetValue(trunk.TipID, out var c);
            while (c != null && walked.Add(c.Id))
            {
                if (c.ParentIds.Count == 2 && !c.IsParentsSwapped && branchNameService.IsMergeOfInto(c, trunk.NiceName))
                {
                    (c.ParentIds[1], c.ParentIds[0]) = (c.ParentIds[0], c.ParentIds[1]);
                    c.IsParentsSwapped = true;
                    branchNameService.ParentsSwapped(c);
                }

                c = c.ParentIds.Count > 0 ? repo.CommitsById.GetValueOrDefault(c.ParentIds[0]) : null;
            }
        }
    }
}
