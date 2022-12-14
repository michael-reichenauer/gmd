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
        var t = Timing.Start();
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
        List<Augmented.Branch> filteredBranches,
        List<Augmented.Commit> filteredCommits)
    {
        foreach (var b in filteredBranches.ToList())  // ToList() since SetBehind/SetAhead modifies branches
        {
            if (b.IsRemote && b.LocalName != "")
            {
                // Remote branch with ahead commits (remote only commits)
                SetBehindCommits(b, filteredBranches, filteredCommits);
            }
            else if (!b.IsRemote && b.RemoteName != "")
            {
                // Local branch with behind commits (local only commits)
                SetAheadCommits(b, filteredBranches, filteredCommits);
            }
        }
    }

    void SetBehindCommits(
        Augmented.Branch remoteBranch,
        List<Augmented.Branch> filteredBranches,
        List<Augmented.Commit> filteredCommits)
    {
        int remoteBranchIndex = filteredBranches.FindIndex(b => b.Name == remoteBranch.Name);
        int localBranchIndex = filteredBranches.FindIndex(b => b.Name == remoteBranch.LocalName);

        var localBranch = filteredBranches[localBranchIndex];
        if (localBranch.TipId == remoteBranch.TipId)
        {
            // Local branch tip on same commit as remote branch tip (i.e. synced)
            return;
        }

        var localTip = filteredCommits.First(c => c.Id == localBranch.TipId);
        var localBottom = filteredCommits.First(c => c.Id == localBranch.BottomId);
        var localBase = filteredCommits.First(c => c.Id == localBottom.ParentIds[0]);

        bool hasBehindCommits = false;
        int count = 0;
        int commitIndex = filteredCommits.FindIndex(c => c.Id == remoteBranch.TipId);
        while (commitIndex != -1)
        {
            var commit = filteredCommits[commitIndex];
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
                var mergeParent = filteredCommits.FirstOrDefault(c => c.Id == commit.ParentIds[1]);
                if (mergeParent != null && mergeParent.BranchName == localBranch.Name)
                {   // Merge from local branch (into this remote branch)
                    break;
                }
            }

            // Commit is(behind)
            filteredCommits[commitIndex] = commit with { IsBehind = true };
            hasBehindCommits = true;

            if (commit.ParentIds.Count == 0)
            {   // Reach last commit
                break;
            }
            commitIndex = filteredCommits.FindIndex(c => c.Id == commit.ParentIds[0]);
        }

        if (hasBehindCommits)
        {
            filteredBranches[remoteBranchIndex] = remoteBranch with { HasBehindCommits = true };
            filteredBranches[localBranchIndex] = localBranch with { HasBehindCommits = true };
        }
    }

    void SetAheadCommits(
        Augmented.Branch localBranch,
        List<Augmented.Branch> filteredBranches,
        List<Augmented.Commit> filteredCommits)
    {
        int localBranchIndex = filteredBranches.FindIndex(b => b.Name == localBranch.Name);
        int remoteBranchIndex = filteredBranches.FindIndex(b => b.Name == localBranch.RemoteName);
        var remoteBranch = filteredBranches[remoteBranchIndex];
        bool hasAheadCommits = false;
        int count = 0;
        int commitIndex = filteredCommits.FindIndex(c => c.Id == localBranch.TipId);
        while (commitIndex != -1)
        {
            count++;
            var commit = filteredCommits[commitIndex];

            if (commit.BranchName != localBranch.Name || count > 50)
            {
                break;
            }

            if (commit.Id != Repo.UncommittedId)
            {
                // Commit is ahead
                filteredCommits[commitIndex] = commit with { IsAhead = true };
                hasAheadCommits = true;
            }

            if (commit.ParentIds.Count == 0)
            {   // Reach last commit
                break;
            }
            commitIndex = filteredCommits.FindIndex(c => c.Id == commit.ParentIds[0]);
        }

        if (hasAheadCommits)
        {
            filteredBranches[localBranchIndex] = localBranch with { HasAheadCommits = true };
            filteredBranches[remoteBranchIndex] = remoteBranch with { HasAheadCommits = true };
        }
    }

    List<Augmented.Commit> FilterOutViewCommits(
        Augmented.Repo repo, IReadOnlyList<Augmented.Branch> filteredBranches)
    {
        // Return filtered commits, where commit branch does is in filtered branches to be viewed.
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

        // Ensure all related branches are included
        foreach (var b in branches.ToList())
        {
            branches.TryAddAll(repo.Branches.Where(bb => bb.CommonName == b.CommonName));
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

        // Sort on branch hierarchy, For some strange reason, List.Sort does not work, why ????
        Sorter.Sort(branches, (b1, b2) => CompareBranches(repo, b1, b2));
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
                if (repo.Status.MergeHeadId != "")
                {   // Add the source merge id as a merge parent to the uncommitted commit
                    if (repo.CommitById.TryGetValue(repo.Status.MergeHeadId, out var mergeHead))
                    {
                        parentIds.Add(repo.Status.MergeHeadId);
                    }
                }

                uncommitted = new Augmented.Commit(
                    Id: Repo.UncommittedId, Sid: Repo.UncommittedId.Substring(0, 6),
                    Subject: subject, Message: subject, Author: "", AuthorTime: DateTime.Now,
                    GitIndex: 0, currentBranch.Name, currentBranch.CommonName,
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


    int CompareBranches(Augmented.Repo repo, Augmented.Branch b1, Augmented.Branch b2)
    {
        if (b1 == b2) return 0;
        if (b1.Name == b2.ParentBranchName) return -1;   // b1 is parent of b2
        if (b2.Name == b1.ParentBranchName) return 1;   // b2 is parent of b1

        // Check if b1 is ancestor of b2
        var current = b2.ParentBranchName != "" ? repo.BranchByName[b2.ParentBranchName] : null;
        while (current != null)
        {
            if (b1 == current) return -1;
            current = current.ParentBranchName != "" ? repo.BranchByName[current.ParentBranchName] : null;
        }

        // Check if b2 is ancestor of b1
        current = b1.ParentBranchName != "" ? repo.BranchByName[b1.ParentBranchName] : null;
        while (current != null)
        {
            if (b2 == current) return 1;
            current = current.ParentBranchName != "" ? repo.BranchByName[current.ParentBranchName] : null;
        }

        // Not related
        return 0;
    }
}
