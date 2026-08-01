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
    readonly ICommitGraphService commitGraphService;
    readonly ICommitBranchService commitBranchService;
    readonly IBranchHierarchyService branchHierarchyService;

    public BranchStructureService(
        ICommitGraphService commitGraphService,
        ICommitBranchService commitBranchService,
        IBranchHierarchyService branchHierarchyService
    )
    {
        this.commitGraphService = commitGraphService;
        this.commitBranchService = commitBranchService;
        this.branchHierarchyService = branchHierarchyService;
    }

    public void DetermineCommitBranches(WorkRepo repo, GitRepo gitRepo)
    {
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
