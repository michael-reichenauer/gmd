namespace gmd.Server.Private.Augmented.Private;

// The stage of the branch structure pipeline that assigns a branch to every commit. Git records the
// branch of a tip commit only, so for every commit below a tip the branch has to be inferred, which
// is what the ordered rules in DetermineCommitBranch do. When no rule applies, the commit is marked
// ambiguous and the user is offered the choice.
interface ICommitBranchService
{
    void DetermineAllCommitsBranches(WorkRepo repo, GitRepo gitRepo);
}

class CommitBranchService : ICommitBranchService
{
    readonly IBranchNameService branchNameService;
    readonly ICommitBranchRules rules;

    public CommitBranchService(IBranchNameService branchNameService, ICommitBranchRules rules)
    {
        this.branchNameService = branchNameService;
        this.rules = rules;
    }

    public void DetermineAllCommitsBranches(WorkRepo repo, GitRepo gitRepo)
    {
        foreach (var c in repo.Commits)
        {
            // Determine commit branch as most likely as possible or as an ambiguous branch
            var branch = DetermineCommitBranch(repo, c, gitRepo);
            c.Branch = branch;

            if (
                !c.IsAmbiguous
                && branch.IsAmbiguousBranch
                && branch.AmbiguousTip != null
                && c.GitIndex > branch.AmbiguousTip.GitIndex
            )
            { // The commit is lower than the branch ambiguous tip, set it to ambiguous as well
                c.IsAmbiguous = true;
            }

            if (!c.IsAmbiguous)
            { // Commit has a branch, clear other possible branches
                c.Branches.Clear();
            }
            c.Branches.TryAdd(branch);

            // Set the IsLikely property if the branch is likely to be the correct branch
            if (branchNameService.TryGetBranchName(c.Id, out string name) && IsNameOfBranch(branch, name))
            { // This flag might improve other commits below to select correct branch;
                c.IsLikely = true;
            }

            // If this commit is a main branch, then its first parent will likely be it too.
            SetMasterBackbone(c);

            // Advance tha bottom id to eventually determine the bottom commit of the branch
            c.Branch.BottomID = c.Id;
        }
    }

    // A name parsed from a merge subject is a nice name, e.g. 'dev', while the branch a commit was
    // assigned to is the primary branch, which is the remote branch, e.g. 'origin/dev', whenever
    // the branch has one. So a plain name comparison would only ever match a branch with no remote.
    // Deliberately not matched by nice name: a deleted branch recovered from a merge subject is
    // named "<nice name>:<sid>", and every merge into it then looks like a confirmation of a name
    // that was only ever a guess.
    static bool IsNameOfBranch(WorkBranch branch, string name) =>
        branch.Name == name || (branch.IsRemote && branch.NiceName == name);

    // The ordered rules that determine the branch of one commit. The first rule that applies wins,
    // so the order is the strength of the evidence each rule is based on.
    WorkBranch DetermineCommitBranch(WorkRepo repo, WorkCommit commit, GitRepo gitRepo)
    {
        commit.Branches.TryAddAll(commit.FirstChildren.SelectMany(c => c.Branches));
        var branchNames = string.Join(",", commit.Branches.Select(b => b.Name));

        if (commit.Id == Repo.TruncatedLogCommitId)
        {
            return BranchFactory.AddTruncatedBranch(repo);
        }
        else if (rules.TryIsBranchSetByUser(repo, gitRepo, commit, out WorkBranch? branch))
        { // Commit branch was set/determined by user,
            return branch!;
        }
        else if (rules.TryHasOnlyOneBranch(commit, out branch))
        { // Commit only has one branch, use that
            return branch!;
        }
        else if (rules.TryIsLocalRemoteBranch(commit, out branch))
        { // Commit has only local and its remote branch, prefer remote remote branch
            return branch!;
        }
        else if (rules.TryHasMainBranch(commit, out branch))
        { // Commit, has several possible branches, and one is in the priority list, e.g. main, master, ...
            return branch!;
        }
        else if (rules.TryIsMergedDeletedBranchTip(repo, commit, out branch))
        { // Commit has no branches and no children, but has a merge child.
            // The commit is a tip of a deleted branch. It might be a deleted remote branch.
            // Lets try determine branch name based on merge child's subject
            // or use a generic branch name based on commit id
            return branch!;
        }
        else if (rules.TryIsStrangeDeletedBranchTip(repo, commit, out branch))
        { // Commit has no branches and no children, but may have merge children.
            // The commit is a tip of a deleted remote branch.
            // Lets try determine branch name based on merge child's subject
            // or use a generic branch name based on commit id
            return branch!;
        }
        else if (rules.TryHasBranchNameInSubject(repo, commit, out branch))
        { // A branch name could be parsed form the commit subject or a child subject.
            // The commit will be set to that branch and also if above (first child) commits have
            // ambiguous branches, the will be reset to same branch as well. This will 'repair' branch
            // when a parsable commit subjects are encountered.
            return branch!;
        }
        else if (rules.TryHasOnlyOneChild(commit, out branch))
        { // Commit has one child commit reuse that child commit branch
            return branch!;
        }
        else if (rules.TryHasOneChildWithLikelyBranch(commit, out branch))
        { // Commit multiple possible git branches but has one child, which has a likely known branch, use same branch
            return branch!;
        }
        else if (rules.TryHasMultipleChildrenWithOneLikelyBranch(commit, out branch))
        { // Commit multiple possible git branches but has a child, which has a likely known branch, use same branch
            return branch!;
        }
        else if (rules.TrySameChildrenBranches(commit, out branch))
        { // For e.g. pull merges, a commit can have two children with same logical branch
            return branch!;
        }
        else if (rules.TryIsMergedBranchesToParent(repo, commit, out branch))
        { // Checks if a commit with 2 children and if the one child branch is merged into the
            // other child branch. E.g. like a pull request or feature branch
            return branch!;
        }
        else if (rules.TryIsChildAmbiguousCommit(commit, out branch))
        { // If one of the commit children is a an ambiguous commit, reuse same branch
            // Log.Info($"Commit {commit.Sid} has ambiguous child commit {branchNames}");
            return branch!;
        }
        // Log.Warn($"Ambiguous branch {commit}");

        // Commit, has several possible branches, and we could not determine which branch is best,
        // create a new ambiguous branch. Later commits may fix this by parsing subjects of later
        // commits, or the user has to manually set the branch.
        return BranchAmbiguity.AddAmbiguousCommit(repo, commit);
    }

    static void SetMasterBackbone(WorkCommit c)
    {
        if (c.FirstParent == null || c.Branch == null)
        { // Reached the end of the repository or commit has no branch (which it always has now)
            return;
        }

        if (WellKnownBranches.MainNamePriority.Contains(c.Branch.Name))
        { // main and develop are special and will make a "backbone" for other branches to depend on
            // Adding this branch to the first parent branches will make it likely to be set as
            // branch for the parent as well, and so on up to the first (oldest/last) commit.
            c.FirstParent.Branches.TryAdd(c.Branch);
        }
    }
}
