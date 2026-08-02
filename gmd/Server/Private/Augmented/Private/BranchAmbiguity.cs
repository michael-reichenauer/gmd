namespace gmd.Server.Private.Augmented.Private;

// The two ends of ambiguity while assigning branches to commits: TrySetBranch repairs an ambiguous
// stretch of commits once evidence of the real branch shows up, and AddAmbiguousCommit gives up on
// a commit and records the branches it could belong to, so the user can be offered the choice.
static class BranchAmbiguity
{
    // Sets the branch of the commit and lets the commits above (first children) use that branch as
    // well as long as they are ambiguous, i.e. 'repairs' the branch of an ambiguous stretch.
    public static bool TrySetBranch(WorkRepo repo, WorkCommit commit, WorkBranch branch)
    {
        // Lets use that as a branch name and also let children (commits above)
        // use that branch if they are an ambiguous branch
        if (branch.TipID == commit.Id)
        { // The commit is branch tip, we should not find higher/previous commit up, since tip would move up
            commit.Branch = branch;
            commit.IsLikely = true;
            commit.Branches.TryAdd(branch);
            return true;
        }

        // Lets iterate upp (first child) as long as commits are ambiguous and the branch exists
        var namedBranch = branch;
        var current = commit;
        Dictionary<string, string> bottoms = [];
        while (current.Id != branch.TipID)
        {
            var child = current
                .FirstChildren.Where(c => c.IsAmbiguous)
                .FirstOrDefault(c => c.Branches.Contains(namedBranch));
            if (child == null)
            { // No ambiguous child commit with that branch, cannot step up further
                break;
            }
            // Remember highest known id of each branch, to later be used to set branch bottom id
            bottoms[child.Branch!.Name] = child.Id;

            // Step upp to child
            current = child;
        }

        if (
            current.FirstChildren.Any()
            && current.Id != branch.TipID
            && null == current.FirstChildren.FirstOrDefault(c => !c.IsAmbiguous && c.Branch == namedBranch)
        )
        { // Failed to reach last not ambiguous branch part of named branch
            return false;
        }

        branch.AmbiguousTip = null;
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
                { // Sett branch bottom to child
                    var firstOtherChild = com.FirstChildren.FirstOrDefault(c => c.Branch == com.Branch);
                    if (firstOtherChild != null)
                    {
                        com.Branch!.BottomID = firstOtherChild.Id;
                    }
                    else
                    { // Must have been a tip on current
                        com.Branch!.BottomID = com.Id;
                    }
                }
                else
                { // Has no children, set to current
                    com.Branch!.BottomID = com.Id;
                }
            }
        }

        do
        {
            if (current.Branch != null && current.Branch != branch && current.Branch.AmbiguousTip == current)
            {
                current.Branch.IsAmbiguousBranch = false;
                current.Branch.AmbiguousBranches.Clear();
                current.Branch.AmbiguousTip = null;
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

    // Commit, has several possible branches, and we could not determine which branch is best,
    // create a new ambiguous branch. Later commits may fix this by parsing subjects of later
    // commits, or the user has to manually set the branch.
    public static WorkBranch AddAmbiguousCommit(WorkRepo repo, WorkCommit c)
    {
        WorkBranch? branch;
        List<WorkBranch>? ambiguousBranches;
        if (!c.Branches.Any())
        {
            branch = BranchFactory.AddNamedBranch(repo, c, "ambiguous");
            ambiguousBranches = [branch];
        }
        else
        {
            (branch, ambiguousBranches) = GetLikelyBranches(c);
        }

        c.IsAmbiguous = true;
        c.Branch = branch;
        c.Branch.IsAmbiguousBranch = true;
        c.Branch.AmbiguousTip = c;
        c.Branch.AmbiguousBranches = ambiguousBranches;
        c.Branches.TryAddAll(ambiguousBranches);

        return branch;
    }

    static (WorkBranch, List<WorkBranch>) GetLikelyBranches(WorkCommit commit)
    {
        var ambiguousBranches = commit.Branches;

        if (commit.FirstChildren.Count < 2)
        {
            // Commit has no children (i.e.must a branch tip with multiple possible tips)
            // Prefer remote branch if possible
            var likelyBranch = commit.Branches.FirstOrDefault(b => b.IsRemote);
            likelyBranch ??= ambiguousBranches.First();

            return (likelyBranch, ambiguousBranches);
        }

        // Likely child is preferred
        var likelyChild = commit.FirstChildren.FirstOrDefault(c => c.IsLikely);
        if (likelyChild != null)
        {
            var likelyBranch = likelyChild.Branch!;
            ambiguousBranches = ambiguousBranches
                .Concat(commit.FirstChildren.Select(c => c.Branch!))
                .Distinct()
                .ToList();

            return (likelyBranch, ambiguousBranches);
        }

        // Determine the most likely branch (branch of the oldest child)
        var oldestChild = commit.FirstChildren[0];
        List<WorkBranch> childBranches = [];
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
}
