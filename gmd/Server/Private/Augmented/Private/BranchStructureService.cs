namespace gmd.Server.Private.Augmented.Private;

interface IBranchStructureService
{
    void DetermineCommitBranches(WorkRepo repo, GitRepo gitRepo);
}

// Determines the branch structure of a repo, i.e. which branch each commit belongs to and how the
// branches relate to each other. This is the information git itself does not record and which gmd
// therefore has to infer.
//
// The work is a fixed pipeline of stages, one class per stage:
//   CommitGraphService     - makes the commit graph traversable (branch tips, parents and children)
//   CommitBranchService    - assigns a branch to every commit
//   BranchHierarchyService - relates the branches to each other (parent, root and ancestors)
class BranchStructureService : IBranchStructureService
{
    // The stages share the one BranchNameService (a single instance), whose names are one read's
    static readonly object syncRoot = new();

    readonly IBranchNameService branchNameService;
    readonly ICommitGraphService commitGraphService;
    readonly ICommitBranchService commitBranchService;
    readonly IBranchHierarchyService branchHierarchyService;

    public BranchStructureService(
        IBranchNameService branchNameService,
        ICommitGraphService commitGraphService,
        ICommitBranchService commitBranchService,
        IBranchHierarchyService branchHierarchyService
    )
    {
        this.branchNameService = branchNameService;
        this.commitGraphService = commitGraphService;
        this.commitBranchService = commitBranchService;
        this.branchHierarchyService = branchHierarchyService;
    }

    public void DetermineCommitBranches(WorkRepo repo, GitRepo gitRepo)
    {
        lock (syncRoot)
        {
            DetermineCommitBranchesOnce(repo, gitRepo);
        }
    }

    void DetermineCommitBranchesOnce(WorkRepo repo, GitRepo gitRepo)
    {
        // Forget the names parsed from the merge subjects of the read before, which a stage changes
        // for the graph of its own read, e.g. a foxtrot merge's names are swapped with its parents
        branchNameService.StartRead(repo);

        // Start be setting branch tips on tip commits, this will be starting point for determining branches
        commitGraphService.SetGitBranchTipsOnCommits(repo);

        // Set parents and children on commits to be able to traverse the commit graph easier
        commitGraphService.SetCommitParentsAndChildren(repo);

        // Not iterate all commits from the latest (top of log) and down to the first commit
        // If multiple possible branches exist for a commit, try to determine the most likely branch
        commitBranchService.DetermineAllCommitsBranches(repo, gitRepo);

        // Determine parent/child branch relationships, where a child branch is branch out of a parent
        branchHierarchyService.DetermineBranchHierarchy(repo);

        // Determine the most likely root/main branch of the repository
        branchHierarchyService.DetermineRootBranch(repo);

        // Determine the ancestors of each branch (i.e. parents, grandparents, etc.)
        branchHierarchyService.DetermineAncestors(repo);
    }
}
