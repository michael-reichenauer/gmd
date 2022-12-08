using System.Diagnostics.CodeAnalysis;

namespace gmd.Server.Private;


interface IViewRepoCreater
{
    Repo GetViewRepoAsync(Augmented.Repo augRepo, IReadOnlyList<string> showBranches);
    bool IsFirstAncestorOfSecond(Augmented.Repo augmentedRepo, Augmented.Branch ancestor, Augmented.Branch branch);
}

class ViewRepoCreater : IViewRepoCreater
{
    private readonly IConverter converter;

    internal ViewRepoCreater(IConverter converter)
    {
        this.converter = converter;
    }

    public Repo GetViewRepoAsync(Augmented.Repo augRepo, IReadOnlyList<string> showBranches)
    {
        var t = Timing.Start;
        var filteredBranches = FilterOutViewBranches(augRepo, showBranches);

        var filteredCommits = FilterOutViewCommits(augRepo, filteredBranches);

        if (TryGetUncommittedCommit(augRepo, filteredBranches, out var uncommitted))
        {
            AdjustCurrentBranch(augRepo, filteredBranches, filteredCommits, uncommitted);
        }

        SetAheadBehind(filteredBranches, filteredCommits);

        var repo = new Repo(
            DateTime.UtcNow,
            augRepo,
            converter.ToCommits(filteredCommits),
            converter.ToBranches(filteredBranches),
            augRepo.Status);

        Log.Info($"{t} B:{repo.Branches.Count}, C:{repo.Commits.Count}, S:{repo.Status}");
        return repo;
    }

    public bool IsFirstAncestorOfSecond(Augmented.Repo repo, Augmented.Branch ancestor, Augmented.Branch branch)
    {
        if (branch == ancestor)
        {
            // Same initial branches (not ancestor)
            return false;
        }
        if (branch.LocalName == ancestor.Name)
        {
            // Branch is remote branch of the ancestor
            return false;
        }
        if (branch.RemoteName == ancestor.Name)
        {
            // branch is the local name of the is the remote branch of the branch
            return true;
        }

        var current = branch;
        while (true)
        {
            if (current == ancestor)
            {
                // Current must now be one of its parents and thus is an ancestor
                return true;
            }
            if (current.Name == ancestor.LocalName)
            {
                // Current is the local branch of the ancestor, which is an ancestor as well
                return true;
            }
            if (current.Name == ancestor.RemoteName)
            {
                // Current is the no the remote branch of the ancestor, which is an ancestor of the
                //  original branch
                return true;
            }

            if (current.ParentBranchName == "")
            {
                // Reached root (current usually is origin/main or origin/master)
                return false;
            }

            // Try with parent of current
            current = repo.BranchByName[current.ParentBranchName];
        }
    }

    void SetAheadBehind(
        List<Augmented.Branch> filterdBranches,
        List<Augmented.Commit> filteredCommits)
    {
        foreach (var b in filterdBranches.ToList())  // ToList() since SetBehind/SetAhead modifies branches
        {
            if (b.IsRemote && b.LocalName != "")
            {
                // Remote branch with ahead commits (remote only commits)
                SetBehindCommits(b, filterdBranches, filteredCommits);
            }
            else if (!b.IsRemote && b.RemoteName != "")
            {
                // Local branch with behind commits (local only commits)
                SetAheadCommits(b, filterdBranches, filteredCommits);
            }
        }
    }

    void SetBehindCommits(
        Augmented.Branch remoteBranch,
        List<Augmented.Branch> filterdBranches,
        List<Augmented.Commit> filterdCommits)
    {
        int remoteBranchIndex = filterdBranches.FindIndex(b => b.Name == remoteBranch.Name);
        int localBranchIndex = filterdBranches.FindIndex(b => b.Name == remoteBranch.LocalName);

        var localBranch = filterdBranches[localBranchIndex];
        if (localBranch.TipId == remoteBranch.TipId)
        {
            // Local branch tip on same commit as remote branch tip (i.e. synced)
            return;
        }

        var localTip = filterdCommits.First(c => c.Id == localBranch.TipId);
        var localBottom = filterdCommits.First(c => c.Id == localBranch.BottomId);
        var localBase = filterdCommits.First(c => c.Id == localBottom.ParentIds[0]);

        bool hasBehindCommits = false;
        int count = 0;
        int commitIndex = filterdCommits.FindIndex(c => c.Id == remoteBranch.TipId);
        while (commitIndex != -1)
        {
            var commit = filterdCommits[commitIndex];
            count++;
            if (commit.BranchName != remoteBranch.Name || count > 50)
            {   // Other branch or to many ahead commits
                break;
            }
            if (commit.Id == localTip.Id)
            {
                // Local branch tip on same commit as remote branch tip (i.e. synced)
                break;
            }
            if (commit.Id == localBase.Id)
            {
                // Reached same commit as local branch branched from (i.e. synced from this point)
                break;
            }

            if (commit.ParentIds.Count > 1)
            {
                var mergeParent = filterdCommits.FirstOrDefault(c => c.Id == commit.ParentIds[1]);
                if (mergeParent != null && mergeParent.BranchName == localBranch.Name)
                {   // Merge from local branch (into this remote branch)
                    break;
                }
            }

            // Commit is(behind)
            filterdCommits[commitIndex] = commit with { IsBehind = true };
            hasBehindCommits = true;

            if (commit.ParentIds.Count == 0)
            {   // Reach last commit
                break;
            }
            commitIndex = filterdCommits.FindIndex(c => c.Id == commit.ParentIds[0]);
        }

        if (hasBehindCommits)
        {
            filterdBranches[remoteBranchIndex] = remoteBranch with { HasBehindCommits = true };
            filterdBranches[localBranchIndex] = localBranch with { HasBehindCommits = true };
        }
    }

    void SetAheadCommits(
        Augmented.Branch localBranch,
        List<Augmented.Branch> filterdBranches,
        List<Augmented.Commit> filterdCommits)
    {
        int localBranchIndex = filterdBranches.FindIndex(b => b.Name == localBranch.Name);
        int remoteBranchIndex = filterdBranches.FindIndex(b => b.Name == localBranch.RemoteName);
        var remoteBranch = filterdBranches[remoteBranchIndex];
        bool hasAheadCommits = false;
        int count = 0;
        int commitIndex = filterdCommits.FindIndex(c => c.Id == localBranch.TipId);
        while (commitIndex != -1)
        {
            count++;
            var commit = filterdCommits[commitIndex];

            if (commit.BranchName != localBranch.Name || count > 50)
            {
                break;
            }

            if (commit.Id != Repo.UncommittedId)
            {
                // Commit is ahead
                filterdCommits[commitIndex] = commit with { IsAhead = true };
                hasAheadCommits = true;
            }

            if (commit.ParentIds.Count == 0)
            {   // Reach last commit
                break;
            }
            commitIndex = filterdCommits.FindIndex(c => c.Id == commit.ParentIds[0]);
        }

        if (hasAheadCommits)
        {
            filterdBranches[localBranchIndex] = localBranch with { HasAheadCommits = true };
            filterdBranches[remoteBranchIndex] = remoteBranch with { HasAheadCommits = true };
        }
    }

    List<Augmented.Commit> FilterOutViewCommits(
        Augmented.Repo repo, IReadOnlyList<Augmented.Branch> filteredBranches)
    {
        // Return filterd commits, where commit branch does is in filtered branches to be viewed.
        return repo.Commits
            .Where(c => filteredBranches.FirstOrDefault(b => b.Name == c.BranchName) != null)
            .ToList();
    }

    List<Augmented.Branch> FilterOutViewBranches(Augmented.Repo repo, IReadOnlyList<string> showBranches)
    {
        var branches = showBranches
            .Select(name => repo.Branches.FirstOrDefault(b => b.Name == name))
            .Where(b => b != null)
            .Select(b => b!) // Workaround since compiler does not recognize the previous Where().
            .ToList();       // To be able to add more

        if (showBranches.Count == 0)
        {   // No branches where specified, assume current branch
            var current = repo.Branches.FirstOrDefault(b => b.IsCurrent);
            if (current != null)
            {
                branches.TryAdd(current);
            }
        }


        // Ensure that main branch is always included 
        var main = repo.Branches.First(b => b.IsMainBranch);
        branches.TryAdd(main);

        // Ensure all ancestors are included
        foreach (var b in branches.ToList())
        {
            Ancestors(repo, b).ForEach(bb => branches.TryAdd(bb));
        }

        // Ensure all local branches of remote branches are included 
        // (remote branches of local branches are ancestors and already included)
        foreach (var b in branches.ToList())
        {
            if (b.IsRemote && b.LocalName != "")
            {
                branches.TryAdd(repo.BranchByName[b.LocalName]);
            }
        }

        // Ensure all pull merger branches of a branch are included 
        foreach (var b in branches.ToList())
        {
            b.PullMergeBranchNames.ForEach(bb => branches.TryAdd(repo.BranchByName[bb]));
        }

        // Ensure all branch tip branches are included (in case of tip on parent with no own commits)
        foreach (var b in branches.ToList())
        {
            branches.TryAdd(repo.BranchByName[repo.CommitById[b.TipId].BranchName]);
        }

        // Remove duplicates (ToList(), since Sort works inline)
        branches = branches.DistinctBy(b => b.Name).ToList();

        // Sort on branch hierarchy
        branches.Sort((b1, b2) => CompareBranches(repo, b1, b2));
        return branches;
    }

    bool TryGetUncommittedCommit(
      Augmented.Repo repo,
      IReadOnlyList<Augmented.Branch> filteredBranches,
      [MaybeNullWhen(false)] out Augmented.Commit uncommitted)
    {
        if (!repo.Status.IsOk)
        {
            var currentBranch = filteredBranches.FirstOrDefault(b => b.IsCurrent);
            if (currentBranch != null)
            {
                var current = repo.CommitById[currentBranch.TipId];

                var parentIds = new List<string>() { current.Id };

                int changesCount = repo.Status.ChangesCount;
                string subject = $"{changesCount} uncommitted changes";
                if (repo.Status.IsMerging && repo.Status.MergeMessage != "")
                {
                    subject = $"{repo.Status.MergeMessage}, {subject}";
                }
                if (repo.Status.Conflicted > 0)
                {
                    subject = $"CONFLICTS: {repo.Status.Conflicted}, {subject}";
                }

                uncommitted = new Augmented.Commit(
                    Id: Repo.UncommittedId, Sid: Repo.UncommittedId.Substring(0, 6),
                    Subject: subject, Message: subject, Author: "", AuthorTime: DateTime.Now,
                    Index: 0, currentBranch.Name, currentBranch.CommonName,
                    ParentIds: parentIds, ChildIds: new List<string>(),
                    Tags: new List<Augmented.Tag>(), BranchTips: new List<string>(),
                    IsCurrent: false, IsUncommitted: true, IsConflicted: repo.Status.Conflicted > 0,
                    IsAhead: false, IsBehind: false,
                    IsPartialLogCommit: false, IsAmbiguous: false, IsAmbiguousTip: false,
                    IsBranchSetByUser: false);

                return true;
            }
        }

        uncommitted = null;
        return false;
    }

    private void AdjustCurrentBranch(
        Augmented.Repo augRepo,
        List<Augmented.Branch> filteredBranches,
        List<Augmented.Commit> filteredCommits,
        Augmented.Commit uncommitted)
    {
        // Prepend the commits with the uncommitted commit
        filteredCommits.Insert(0, uncommitted);

        // We need to adjust the current branch and the tip of that branch to include the
        // uncommitted commit
        var currentBranchIndex = filteredBranches.FindIndex(b => b.Name == uncommitted.BranchName);
        var currentBranch = filteredBranches[currentBranchIndex];
        var tipCommitIndex = filteredCommits.FindIndex(c => c.Id == currentBranch.TipId);
        var tipCommit = filteredCommits[tipCommitIndex];

        // Adjust the current branch tip id and if the branch is empty, the bottom id
        var tipId = uncommitted.Id;
        var bottomId = currentBranch.BottomId;
        if (tipCommit.BranchName != currentBranch.Name)
        {
            // Current branch does not yet have any commits, so bottom id will be the uncommitted commit
            bottomId = uncommitted.Id;
        }

        var newCurrentBranch = currentBranch with { TipId = tipId, BottomId = bottomId };
        filteredBranches[currentBranchIndex] = newCurrentBranch;

        // Adjust the current tip commit to have the uncommitted commit as child
        var childIds = tipCommit.ChildIds.Append(uncommitted.Id).ToList();
        var newTipCommit = tipCommit with { ChildIds = childIds };
        filteredCommits[tipCommitIndex] = newTipCommit;
    }

    int CompareBranches(Augmented.Repo repo, Augmented.Branch b1, Augmented.Branch b2)
    {
        return IsFirstAncestorOfSecond(repo, b1, b2) ? -1 :
            IsFirstAncestorOfSecond(repo, b2, b1) ? 1 : 0;
    }

    IReadOnlyList<Augmented.Branch> Ancestors(Augmented.Repo repo, Augmented.Branch branch)
    {
        List<Augmented.Branch> ancestors = new List<Augmented.Branch>();

        while (branch.ParentBranchName != "")
        {
            var parent = repo.BranchByName[branch.ParentBranchName];
            ancestors.Add(parent);
            branch = parent;
        }

        return ancestors;
    }
}