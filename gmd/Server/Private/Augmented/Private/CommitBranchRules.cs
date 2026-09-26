namespace gmd.Server.Private.Augmented.Private;

// The rules CommitBranchService tries, in order, to determine which branch a commit belongs to.
// Each rule answers "is there enough evidence for this commit to belong to a branch", and the
// order they are tried in is the strength of that evidence, see DetermineCommitBranch.
interface ICommitBranchRules
{
    bool TryIsBranchSetByUser(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasOnlyOneBranch(WorkCommit commit, out WorkBranch? branch);
    bool TryIsLocalRemoteBranch(WorkCommit commit, out WorkBranch? branch);
    bool TryHasMainBranch(WorkCommit commit, out WorkBranch? branch);
    bool TryIsWitnessed(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch);
    bool TryIsPublishedTipOfLocalBranches(WorkCommit commit, out WorkBranch? branch);
    bool TryIsMergedDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryIsStrangeDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryHasOnlyOneChild(WorkCommit commit, out WorkBranch? branch);
    bool TrySameChildrenBranches(WorkCommit commit, out WorkBranch? branch);
    bool TryIsMergedBranchesToParent(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryDecideBranchPoint(WorkRepo repo, WorkCommit commit, out WorkBranch? branch);
    bool TryIsChildAmbiguousCommit(WorkCommit commit, out WorkBranch? branch);
}

class CommitBranchRules : ICommitBranchRules
{
    readonly IBranchNameService branchNameService;

    public CommitBranchRules(IBranchNameService branchNameService)
    {
        this.branchNameService = branchNameService;
    }

    // Commit branch was set/determined by user,
    public bool TryIsBranchSetByUser(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        if (!gitRepo.MetaData.TryGetCommitBranch(commit.Id, out var branchNiceName, out var isSetByUser))
        { // Commit has not a branch set by user
            return false;
        }
        // Log.Info($"Commit {commit.Sid} has branch set to {branchHumanName} (by user: {isSetByUser})");

        var branches = commit.Branches.Where(b => b.NiceName == branchNiceName);
        if (!branches.Any())
        { // Branch not found by obvious commit branches, create a new branch
            commit.IsBranchSetByUser = isSetByUser;
            branch = BranchFactory.AddNamedBranch(repo, commit, branchNiceName);
            return true;
        }

        // Prefer remote branches over local branches
        var remote = branches.FirstOrDefault(b => b.IsRemote);
        if (remote != null)
        {
            commit.IsBranchSetByUser = isSetByUser;
            branch = remote;
            return BranchAmbiguity.TrySetBranch(repo, commit, branch);
        }

        // Just use the first branch with that human name
        commit.IsBranchSetByUser = isSetByUser;
        branch = branches.First();
        return BranchAmbiguity.TrySetBranch(repo, commit, branch);
    }

    // Commit only has one branch, use that
    public bool TryHasOnlyOneBranch(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 1)
        { // Commit only has one branch, use that
            branch = commit.Branches[0];
            return true;
        }

        branch = null;
        return false;
    }

    // Commit has only local and its remote branch, prefer remote remote branch
    public bool TryIsLocalRemoteBranch(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 2)
        {
            if (commit.Branches[0].IsRemote && commit.Branches[0].Name == commit.Branches[1].RemoteName)
            { // remote and local branch, prefer remote
                branch = commit.Branches[0];
                return true;
            }
            if (!commit.Branches[0].IsRemote && commit.Branches[0].RemoteName == commit.Branches[1].Name)
            { // local and remote branch, prefer remote
                branch = commit.Branches[1];
                return true;
            }
        }

        branch = null;
        return false;
    }

    // Commit, has several possible branches, and one is in the priority list, e.g. main, master, ...
    public bool TryHasMainBranch(WorkCommit c, out WorkBranch? branch)
    {
        branch = null;
        if (c.Branches.Count < 1)
            return false;

        // Check if commit has one of the main branches
        foreach (var name in WellKnownBranches.MainNamePriority)
        {
            branch = c.Branches.Find(b => b.Name == name);
            if (branch != null)
            {
                return true;
            }
        }

        return false;
    }

    // The reflog witnessed which branch the commit was made on, or that a branch was started from it
    // on another branch (see ReflogWitness), which is just what the user means by a commit's branch.
    // Taken only among the branches the commit can be on: a commit a reset moved off the branch it
    // was made on is not on it any more, and a fact never makes up a branch, as a choice in the
    // metadata does. Not above the trunk either, it comes after the main branch rule: a feature
    // fast-forwarded into main would take main's commits. And the commit is not marked likely, since
    // where a commit was made says nothing about its parent, e.g. a branch point below a branch's
    // first commit, which is what the likely-child rules would read it as.
    //
    // Except where the branch it was made on was merged by a fast-forward into the branch it was
    // started from ('git merge feature' on dev): that branch runs through the commit and on below, so
    // the commit is on its line now, and so is the rest of the merged work below it. Taken for the
    // branch it was made on, it gave that branch dev's line from there down, and drew dev as started
    // from it. Followed as far as the reflog says which branch each was started from, e.g. to dev for
    // a feature2 started from a feature1 started from dev.
    //
    // The reflog expires, so a fact that decided between branches is kept in the metadata (see
    // WorkRepo.WitnessedToKeep), and a kept fact is taken before the reflog's: it was decided while the
    // reflog said more. A branch's oldest entry, where it was started, is the first to go, and the
    // commit made on it would then be taken from dev again. The reflog's is taken when nothing is kept,
    // or what is kept names no branch the commit can be on.
    public bool TryIsWitnessed(WorkRepo repo, GitRepo gitRepo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var isKept = gitRepo.MetaData.TryGetWitnessedBranch(commit.Id, out var keptName);
        var isInReflog = gitRepo.WitnessedBranchById.TryGetValue(commit.Id, out var reflogName);
        string? name =
            isKept && IsCandidate(commit, keptName) ? keptName
            : isInReflog && IsCandidate(commit, reflogName!) ? reflogName
            : null;
        if (name == null)
            return false;
        name = BranchStartedFrom(gitRepo, commit, name);

        branch = RemoteFirst(commit.Branches.Where(b => b.NiceName == name));
        var isChoice = commit.Branches.Select(b => b.PrimaryName).Distinct().Count() > 1;
        if (branch == null || !BranchAmbiguity.TrySetBranch(repo, commit, branch, isLikely: false))
            return false;

        if (isChoice && (!isKept || keptName != name))
        { // The reflog decided between branches, which the metadata should keep
            repo.WitnessedToKeep.Add(new WitnessedBranch(commit.Id, name));
        }
        return true;
    }

    static bool IsCandidate(WorkCommit commit, string niceName) => commit.Branches.Any(b => b.NiceName == niceName);

    // The branch the named branch was started from, or the one that one was started from, and so on,
    // the furthest back that the commit can be on too, or the named branch when there is none
    static string BranchStartedFrom(GitRepo gitRepo, WorkCommit commit, string name)
    {
        var branch = name;
        HashSet<string> seen = [name];
        for (var n = name; gitRepo.SourceByBranch.TryGetValue(n, out var source) && seen.Add(source); n = source)
        {
            if (IsCandidate(commit, source))
                branch = source;
        }
        return branch;
    }

    // The commit is the tip of a published branch, and every other candidate is a local branch only,
    // e.g. a feature started at dev's tip with 'git checkout -b', outside gmd, so nothing recorded
    // where it started. The tip is the last commit made on the published branch, and a local branch
    // pointing at it or passing through it came later. Published means the branch has a remote, so
    // a local tip ahead of it counts too.
    // Not the other way round: a local branch left pointing at an older commit of a published branch
    // did not make that commit, and must not take the published branch's history from there down.
    public bool TryIsPublishedTipOfLocalBranches(WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var localOnly = commit.Branches.Where(IsLocalOnly).ToList();
        var published = commit.Branches.Where(b => !IsLocalOnly(b)).ToList();
        if (localOnly.Count == 0 || published.Count == 0)
            return false;

        if (published.Any(b => !b.IsGitBranch) || published.Select(b => b.PrimaryName).Distinct().Count() != 1)
        { // Only one published branch, the local and remote branch of it
            return false;
        }
        if (!published.Any(b => b.TipID == commit.Id))
        { // The published branch passes through, the commit is not its tip
            return false;
        }

        branch = published.FirstOrDefault(b => b.IsRemote) ?? published.First();
        return true;
    }

    // A local git branch that has no remote branch, i.e. one that was never pushed
    static bool IsLocalOnly(WorkBranch b) => b.IsGitBranch && !b.IsRemote && b.RemoteName == "";

    // Commit has no branches and no children, but has a merge child.
    // The commit is a tip of a deleted branch. It might be a deleted remote branch.
    // Lets try determine branch name based on merge child's subject
    // or use a generic branch name based on commit id
    public bool TryIsMergedDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 0 && commit.FirstChildren.Count == 0 && commit.MergeChildren.Count == 1)
        { // Commit has no branch and no children, but has a merge child. I.e. must be a
            // deleted branch that was merged into some other branch.
            // Trying to use parsed branch name from the merge children subjects e.g. like:
            // "Merge branch 'branch-name' into develop"
            if (branchNameService.TryGetBranchName(commit.Id, out var name))
            { // Managed to parse a branch-name
                var mergeChild = commit.MergeChildren[0];

                if (branchNameService.IsPullMerge(mergeChild) && mergeChild.Branch!.NiceName == name)
                { // The branch is a pull name and has same name as the branch is was merged into
                    // The merge child is a pull merge, so this commit is on a "dead" branch part,
                    // which used to be the local branch of the pull merge commit.
                    // Lets connect this branch with the actual branch.
                    var pullMergeBranch = mergeChild.Branch;
                    branch = BranchFactory.AddPullMergeBranch(repo, commit, name, pullMergeBranch!);
                    pullMergeBranch!.PullMergeChildBranches.TryAdd(branch);
                    return true;
                }

                branch = BranchFactory.AddNamedBranch(repo, commit, name);
                return true;
            }

            // could not parse a name from any of the merge children, use id named branch
            branch = BranchFactory.AddNamedBranch(repo, commit);
            return true;
        }

        branch = null;
        return false;
    }

    // Commit has no branches and no children, but may have merge children.
    // The commit is a tip of a deleted remote branch.
    // Lets try determine branch name based on merge child's subject
    // or use a generic branch name based on commit id
    public bool TryIsStrangeDeletedBranchTip(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.Branches.Count == 0 && commit.FirstChildren.Count == 0)
        { // Commit has no branch, and no children, must be a deleted branch tip unusual branch
            // Trying to use parsed branch name from one of the merge children subjects e.g. Merge branch 'a' into develop

            if (branchNameService.TryGetBranchName(commit.Id, out var name))
            { // Managed to parse a branch name
                branch = BranchFactory.AddNamedBranch(repo, commit, name);
                return true;
            }

            // could not parse a name from any of the merge children, use id named branch
            branch = BranchFactory.AddNamedBranch(repo, commit);
            return true;
        }

        branch = null;
        return false;
    }

    // A branch name could be parsed form the commit subject or a child subject.
    // The commit will be set to that branch and also if above (first child) commits have
    // ambiguous branches, the will be reset to same branch as well. This will 'repair' branch
    // when a parsable commit subjects are encountered.
    public bool TryHasBranchNameInSubject(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;

        if (!branchNameService.TryGetBranchName(commit.Id, out var name))
            return false;

        // A branch name could be parsed form the commit subject or a merge child subject.
        branch = TryGetBranchFromName(commit, name);
        if (branch == null)
        { // Found no matching branch
            return false;
        }

        return BranchAmbiguity.TrySetBranch(repo, commit, branch);
    }

    static WorkBranch? TryGetBranchFromName(WorkCommit commit, string name)
    {
        // Try find a live git branch with the remoteName or local name
        var remoteName = $"origin/{name}";
        var branch = commit.Branches.FirstOrDefault(b => b.Name == remoteName);
        if (branch != null)
        {
            return branch;
        }
        branch = commit.Branches.FirstOrDefault(b => b.Name == name);
        if (branch != null)
        {
            return branch;
        }

        // Try find a branch with the human name
        branch = RemoteFirst(commit.Branches.Where(b => b.NiceName == name));
        if (branch != null)
        {
            return branch;
        }

        // Pull requests names include the owner as prefix, e.g. 'owner/dev', try a branch the name
        // ends with, as a whole part of it, so that 'hotfix-dev' is not taken for 'dev'
        return RemoteFirst(commit.Branches.Where(b => name.EndsWith("/" + b.NiceName)));
    }

    // A nice name is shared by a local branch and its remote branch, and the remote branch is the
    // one a commit belongs to whenever it is a candidate, since it is the primary of the two. Taking
    // the local branch there made it the parent of its own parent: the remote branch, then owning
    // nothing, got the local branch as its parent, a cycle.
    static WorkBranch? RemoteFirst(IEnumerable<WorkBranch> branches) =>
        branches.OrderBy(b => b.IsRemote ? 0 : 1).FirstOrDefault();

    // Commit has one child commit reuse that child commit branch
    public bool TryHasOnlyOneChild(WorkCommit commit, out WorkBranch? branch)
    {
        if (commit.FirstChildren.Count == 1)
        { // Commit has only one child, ensure commit has same possible branches
            var child = commit.FirstChildren[0];
            if (commit.Branches.Count != child.Branches.Count || !commit.Branches.All(child.Branches.Contains))
            { // Some branch has changed, the order of them does not matter
                branch = null;
                return false;
            }

            // Commit has one child and same branches, use that child commit branch
            branch = child.Branch;
            commit.IsAmbiguous = child.IsAmbiguous;
            commit.IsLikely = child.IsLikely;
            return true;
        }

        branch = null;
        return false;
    }

    // Commit is where branches meet, and one of them is the branch the others were started from,
    // which goes on below. Decided by the evidence in order, the first that tells one branch from the
    // rest (the trunk is decided before, by TryHasMainBranch):
    //   - the name: an integration branch (develop, dev) is senior to a release or hotfix branch,
    //     which is senior to any other, since each is started from the one before;
    //   - among the branches of the most senior name: when they all have one name, e.g. a deleted
    //     dev recovered once per merge of it, there is nothing to choose between, and the one most
    //     branches were merged into goes on, or the first;
    //   - otherwise the branch that clearly more other branches were merged into: at least two, and
    //     at least twice as many as any other, not counting the trunk merged in to bring a branch up
    //     to date.
    // Merge subjects alone cannot tell which branch goes on: a branch merged into another looks the
    // same whether it is a feature merged back into dev or dev merged into a feature to bring it up
    // to date, which is why one merged branch is never enough, and why which child is named by a
    // merge subject decides nothing either. Without a clear answer the commit is left to the rules
    // after, and so usually ambiguous.
    public bool TryDecideBranchPoint(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var groups = commit
            .Branches.GroupBy(b => b.PrimaryName)
            .Select(g => new BranchGroup(g.ToList(), repo.Branches[g.Key]))
            .ToList();
        if (groups.Count < 2)
            return false;

        var senior = groups
            .GroupBy(g => WellKnownBranches.NameTier(g.Primary.NiceName, repo.IntegrationNames))
            .OrderByDescending(t => t.Key)
            .First()
            .ToList();
        var chosen =
            senior.Count == 1 ? senior[0]
            : senior.Select(g => g.Primary.NiceName).Distinct().Count() == 1 ? MostMergedInto(senior)
            : ClearlyMostMergedInto(senior);
        if (chosen == null)
            return false;

        branch = RemoteFirst(chosen.Branches);
        return true;
    }

    // The branches of one primary branch among a commit's candidates, e.g. dev and origin/dev
    record BranchGroup(IReadOnlyList<WorkBranch> Branches, WorkBranch Primary);

    static BranchGroup MostMergedInto(IReadOnlyList<BranchGroup> groups) =>
        groups.OrderByDescending(g => g.Primary.MergedFromNames.Count).First();

    static BranchGroup? ClearlyMostMergedInto(IReadOnlyList<BranchGroup> groups)
    {
        var byMerged = groups.OrderByDescending(g => g.Primary.MergedFromNames.Count).ToList();
        var most = byMerged[0].Primary.MergedFromNames.Count;
        var next = byMerged[1].Primary.MergedFromNames.Count;
        return most >= 2 && most >= 2 * next ? byMerged[0] : null;
    }

    // For e.g. pull merges, a commit can have two children with same logical branch
    public bool TrySameChildrenBranches(WorkCommit commit, out WorkBranch? branch)
    {
        if (
            commit.Branches.Count == 2
            && commit.FirstChildren.Count == 2
            && commit.FirstChildren[0].Branch!.PrimaryName == commit.FirstChildren[1].Branch!.PrimaryName
        )
        { // Commit has 2 children with same branch use that
            if (
                commit.FirstChildren[0].Branch!.PullMergeParentBranch != null
                && commit.FirstChildren[0].Branch!.PullMergeParentBranch!.Name
                    == commit.FirstChildren[1].Branch!.LocalName
            )
            { // child branch 0 is a pull merge of child 1 local of remote branch 1, prefer parent 1
                branch = commit.FirstChildren[1].Branch;
                commit.IsAmbiguous = commit.FirstChildren[1].IsAmbiguous;
                return true;
            }
            if (commit.FirstChildren[0].Branch!.PullMergeParentBranch == commit.FirstChildren[1].Branch)
            { // child branch 0 is a pull merge of child branch 1, prefer parent 1
                branch = commit.FirstChildren[1].Branch;
                commit.IsAmbiguous = commit.FirstChildren[1].IsAmbiguous;
                return true;
            }

            branch = commit.FirstChildren[0].Branch;
            commit.IsAmbiguous = commit.FirstChildren[0].IsAmbiguous;
            return true;
        }

        branch = null;
        return false;
    }

    // Checks if a commit with 2 children and if the one child branch is merged into the
    // other child branch. E.g. like a pull request or feature branch
    public bool TryIsMergedBranchesToParent(WorkRepo repo, WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        if (commit.FirstChildren.Count == 2) // Could support more children as well
        {
            var b1 = commit.FirstChildren[0].Branch!;
            var b1MergeChildren = repo.CommitsById[b1.TipID].MergeChildren;
            var b1Bottom = repo.CommitsById[b1.BottomID];
            var b2 = commit.FirstChildren[1].Branch!;
            var b2MergeChildren = repo.CommitsById[b2.TipID].MergeChildren;
            var b2Bottom = repo.CommitsById[b2.BottomID];

            if (
                !b2.IsGitBranch
                && b2Bottom.FirstParent == commit
                && b2MergeChildren.Count == 1
                && b2MergeChildren[0].Branch == b1
            )
            {
                branch = b1;
                return true;
            }
            if (
                !b1.IsGitBranch
                && b1Bottom.FirstParent == commit
                && b1MergeChildren.Count == 1
                && b1MergeChildren[0].Branch == b2
            )
            {
                branch = b2;
                return true;
            }
        }

        return false;
    }

    // If one of the commit children is a an ambiguous commit, reuse same branch
    public bool TryIsChildAmbiguousCommit(WorkCommit commit, out WorkBranch? branch)
    {
        branch = null;
        var ambiguousChild = commit.FirstChildren.FirstOrDefault(c => c.IsAmbiguous);
        if (ambiguousChild == null)
        { // No ambiguous child
            return false;
        }

        branch = ambiguousChild.Branch!;

        // If more ambiguous children, merge in their sub branches as well
        commit
            .FirstChildren.Where(c => c.IsAmbiguous && c != ambiguousChild)
            .ForEach(c => c.Branch!.AmbiguousBranches.ForEach(b => commit.Branches.TryAdd(b)));

        commit.IsAmbiguous = true;
        return true;
    }
}
