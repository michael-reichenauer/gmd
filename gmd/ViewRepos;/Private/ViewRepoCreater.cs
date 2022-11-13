

using System.Diagnostics.CodeAnalysis;

namespace gmd.ViewRepos.Private;


interface IViewRepoCreater
{
    Repo GetViewRepoAsync(Augmented.Repo augRepo, string[] showBranches);
    bool IsFirstAncestorOfSecond(Augmented.Repo augmentedRepo, Augmented.Branch ancestor, Augmented.Branch branch);
}

class ViewRepoCreater : IViewRepoCreater
{
    private readonly IConverter converter;

    internal ViewRepoCreater(IConverter converter)
    {
        this.converter = converter;
    }

    public Repo GetViewRepoAsync(Augmented.Repo augRepo, string[] showBranches)
    {
        var t = Timing.Start;
        var branches = FilterOutViewBranches(augRepo, showBranches);

        var commits = FilterOutViewCommits(augRepo, branches);

        if (TryGetUncommittedCommit(augRepo, branches, out var uncommitted))
        {
            AdjustCurrentBranch(augRepo, branches, commits, uncommitted);
        }

        SetAheadBehind(branches, commits);

        var repo = new Repo(
            DateTime.UtcNow,
            augRepo,
            converter.ToCommits(commits),
            converter.ToBranches(branches),
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

    void SetAheadBehind(List<Augmented.Branch> branches, List<Augmented.Commit> commits)
    {
        foreach (var b in branches.ToList())  // ToList() since SetBehind/SetAhead modifies branches
        {
            if (b.IsRemote && b.LocalName != "" && b.BehindCount > 0)
            {
                // Remote branch with ahead commits (remote only commits)
                SetBehindCommits(b, branches, commits);
            }
            else if (!b.IsRemote && b.RemoteName != "" && b.AheadCount > 0)
            {
                // Local branch with behind commits (local only commits)
                SetAheadCommits(b, branches, commits);
            }
        }
    }

    void SetBehindCommits(
        Augmented.Branch remoteBranch,
        List<Augmented.Branch> branches,
        List<Augmented.Commit> commits)
    {
        int remoteBranchIndex = branches.FindIndex(b => b.Name == remoteBranch.Name);
        int localBranchIndex = branches.FindIndex(b => b.Name == remoteBranch.LocalName);
        var localBranch = branches[localBranchIndex];
        if (localBranch.TipId == remoteBranch.TipId)
        {
            // Local branch tip on same commit as remote branch tip (i.e. synced)
            return;
        }


        var localTip = commits.First(c => c.Id == localBranch.TipId);
        var localBottom = commits.First(c => c.Id == localBranch.BottomId);
        var localBase = commits.First(c => c.Id == localBottom.ParentIds[0]);

        bool hasBehindCommits = false;
        int count = 0;
        int commitIndex = commits.FindIndex(c => c.Id == remoteBranch.TipId);
        while (commitIndex != -1)
        {
            var commit = commits[commitIndex];
            count++;
            if (commit.BranchName != remoteBranch.Name || count > 50)
            {   // Other branch or to many ahead commits
                break;
            }

            if (commit.Id == localBase.Id)
            {
                // Reached same commit as local branch branched from (i.e. synced from this point)
                break;
            }

            if (commit.ParentIds.Count > 1)
            {
                var mergeParent = commits.FirstOrDefault(c => c.Id == c.ParentIds[1]);
                if (mergeParent != null && mergeParent.BranchName == localBranch.Name)
                {   // Merge from local branch (into this remote branch)
                    break;
                }
            }

            // Commit is(behind)
            commits[commitIndex] = commit with { IsBehind = true };
            hasBehindCommits = true;

            if (commit.ParentIds.Count == 0)
            {   // Reach last commit
                break;
            }
            commitIndex = commits.FindIndex(c => c.Id == commit.ParentIds[0]);
        }

        if (hasBehindCommits)
        {
            branches[remoteBranchIndex] = remoteBranch with { HasBehindCommits = true };
            branches[localBranchIndex] = localBranch with { HasBehindCommits = true };
        }
    }

    void SetAheadCommits(
        Augmented.Branch localBranch,
        List<Augmented.Branch> branches,
        List<Augmented.Commit> commits)
    {

        int localBranchIndex = branches.FindIndex(b => b.Name == localBranch.Name);
        int remoteBranchIndex = branches.FindIndex(b => b.Name == localBranch.RemoteName);
        var remoteBranch = branches[remoteBranchIndex];
        bool hasAheadCommits = false;
        int count = 0;
        int commitIndex = commits.FindIndex(c => c.Id == localBranch.TipId);
        while (commitIndex != -1)
        {
            count++;
            var commit = commits[commitIndex];

            if (commit.BranchName != localBranch.Name || count > 50)
            {
                break;
            }

            if (commit.Id != Repo.UncommittedId)
            {
                // Commit is ahead
                commits[commitIndex] = commit with { IsAhead = true };
                hasAheadCommits = true;
            }

            if (commit.ParentIds.Count == 0)
            {   // Reach last commit
                break;
            }
            commitIndex = commits.FindIndex(c => c.Id == commit.ParentIds[0]);
        }

        if (hasAheadCommits)
        {
            branches[localBranchIndex] = localBranch with { HasAheadCommits = true };
            branches[remoteBranchIndex] = remoteBranch with { HasAheadCommits = true };
        }
    }

    List<Augmented.Commit> FilterOutViewCommits(
        Augmented.Repo repo, IReadOnlyList<Augmented.Branch> viewBranches)
    {
        // Return commits, which branch does exist in branches to be viewed.
        return repo.Commits
            .Where(c => viewBranches.FirstOrDefault(b => b.Name == c.BranchName) != null)
            .ToList();
    }

    List<Augmented.Branch> FilterOutViewBranches(Augmented.Repo repo, string[] showBranches)
    {
        var branches = showBranches
            .Select(name => repo.Branches.FirstOrDefault(b => b.Name == name))
            .Where(b => b != null)
            .Select(b => b!) // Workaround since compiler does not recognize the previous Where().
            .ToList();       // To be able to add more

        if (showBranches.Length == 0)
        {   // No branches where specified, assume current branch
            var current = repo.Branches.FirstOrDefault(b => b.IsCurrent);
            if (current != null)
            {
                branches.Add(current);
            }
        }


        // Ensure that main branch is always included 
        var main = repo.Branches.First(b => b.IsMainBranch);
        branches.Add(main);

        // Ensure all ancestors are included
        foreach (var b in branches.ToList())
        {
            Ancestors(repo, b).ForEach(bb => branches.Add(bb));
        }

        // Ensure all local branches of remote branches are included 
        // (remote branches of local branches are ancestors and already included)
        foreach (var b in branches.ToList())
        {
            if (b.IsRemote && b.LocalName != "")
            {
                branches.Add(repo.BranchByName[b.LocalName]);
            }
        }

        // Remove duplicates (ToList(), since Sort works inline)
        branches = branches.DistinctBy(b => b.Name).ToList();

        // Sort on branch hierarchy
        branches.Sort((b1, b2) => CompareBranches(repo, b1, b2));
        return branches;
    }

    bool TryGetUncommittedCommit(
      Augmented.Repo repo,
      IReadOnlyList<Augmented.Branch> viewBranches,
      [MaybeNullWhen(false)] out Augmented.Commit uncommitted)
    {
        if (!repo.Status.IsOk)
        {
            var currentBranch = viewBranches.FirstOrDefault(b => b.IsCurrent);
            if (currentBranch != null)
            {
                var current = repo.CommitById[currentBranch.TipId];

                var parentIds = new List<string>() { current.Id };

                int changesCount = repo.Status.ChangesCount;
                string subject = $"{changesCount} uncommitted files";
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
                    Index: 0, currentBranch.Name, ParentIds: parentIds, ChildIds: new List<string>(),
                    Tags: new List<Tag>(), BranchTips: new List<string>(),
                    IsCurrent: false, IsUncommitted: true, IsConflicted: repo.Status.Conflicted > 0,
                    IsAhead: false, IsBehind: false,
                    IsPartialLogCommit: false, IsAmbiguous: false, IsAmbiguousTip: false);

                return true;
            }
        }

        uncommitted = null;
        return false;
    }

    private void AdjustCurrentBranch(Augmented.Repo augRepo, List<Augmented.Branch> branches, List<Augmented.Commit> commits, Augmented.Commit uncommitted)
    {
        // Prepend the commits with the uncommitted commit
        commits.Insert(0, uncommitted);

        // We need to adjust the current branch and the tip of that branch to include the
        // uncommitted commit
        var currentBranchIndex = branches.FindIndex(b => b.Name == uncommitted.BranchName);
        var currentBranch = branches[currentBranchIndex];
        var tipCommitIndex = commits.FindIndex(c => c.Id == currentBranch.TipId);
        var tipCommit = commits[tipCommitIndex];

        // Adjust the current branch tip id and if the branch is empty, the bottom id
        var tipId = uncommitted.Id;
        var bottomId = currentBranch.BottomId;
        if (tipCommit.BranchName != currentBranch.Name)
        {
            // Current branch does not yet have any commits, so bottom id will be the uncommitted commit
            bottomId = uncommitted.Id;
        }

        var newCurrentBranch = currentBranch with { TipId = tipId, BottomId = bottomId };
        branches[currentBranchIndex] = newCurrentBranch;

        // Adjust the current tip commit to have the uncommitted commit as child
        var childIds = tipCommit.ChildIds.Append(uncommitted.Id).ToList();
        var newTipCommit = tipCommit with { ChildIds = childIds };
        commits[tipCommitIndex] = newTipCommit;
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