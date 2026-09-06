namespace gmd.Server.Private.Augmented.Private;

// Augmenter augments repos of git repo information, The augmentations
// adds information not available in git directly, but can be inferred by parsing the
// git information.
// Examples of augmentation is which branch a commits belongs to and the hierarchical structure
// of branches.
interface IAugmenter
{
    Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo);
}

class Augmenter : IAugmenter
{
    private readonly IBranchStructureService branchStructureService;

    internal Augmenter(IBranchStructureService branchStructureService)
    {
        this.branchStructureService = branchStructureService;
    }

    public Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo)
    { // Run in background thread to not block the main thread since
        // this can take a long time for large repos and could be CPU intensive
        return Task.Run(() => GetAugRepo(gitRepo));
    }

    WorkRepo GetAugRepo(GitRepo gitRepo)
    {
        var status = StatusConverter.ToStatus(gitRepo.Status);
        WorkRepo repo = new WorkRepo(gitRepo.TimeStamp, gitRepo.Path, status);

        AddAugStashes(repo, gitRepo); // Must be done before adding augmented commits
        AddAugBranches(repo, gitRepo);
        AddAugWorktrees(repo, gitRepo); // After the branches, which it marks
        AddAugCommits(repo, gitRepo);
        AddAugTags(repo, gitRepo);
        SetCommitHasStash(repo);

        branchStructureService.DetermineCommitBranches(repo, gitRepo);
        SetBranchViewNames(repo);
        return repo;
    }

    static void AddAugBranches(WorkRepo repo, GitRepo gitRepo)
    {
        // Convert git branches to initial augmented branches
        gitRepo.Branches.ForEach(b => repo.Branches[b.Name] = new WorkBranch(b));

        // Set local name of all remote branches, that have a corresponding local branch as well
        // Unset RemoteName of local branch if no corresponding remote branch (deleted on remote server)
        foreach (var b in repo.Branches.Values)
        {
            if (b.RemoteName != "")
            { // A local branch which has a corresponding remote branch
                if (repo.Branches.TryGetValue(b.RemoteName, out var remoteBranch))
                {
                    b.PrimaryName = b.RemoteName;
                    b.IsPrimary = false;
                    remoteBranch.RelatedBranches.Add(b); // Adds itself to primary branch related branches
                    remoteBranch.LocalName = b.Name;
                    if (b.IsCurrent)
                    { // Local branch is current, set property on remote branch as well
                        remoteBranch.IsLocalCurrent = true;
                    }
                }
                else
                { // No corresponding remote branch for local branch (deleted), unset property
                    b.RemoteName = "";
                    b.PrimaryName = b.Name;
                    b.IsPrimary = true;
                    b.RelatedBranches.Add(b); // Adds itself to related branches
                }
            }
            else
            { // Remote branch or a local branch without a corresponding remote branch
                b.PrimaryName = b.Name;
                b.IsPrimary = true;
                b.RelatedBranches.Add(b); // Adds itself to related branches
            }
        }
    }

    // The worktrees of the repository, and which of them holds each branch. The one the repo was
    // read from is found by its path, or failing that (a symlinked temp folder, a different
    // spelling of the drive) by holding the current branch; every other worktree's branch is
    // marked with the folder it is checked out in, which is what the UI acts on.
    static void AddAugWorktrees(WorkRepo repo, GitRepo gitRepo)
    {
        var current = CurrentWorktree(repo, gitRepo);
        foreach (var w in gitRepo.Worktrees)
        {
            var isCurrent = w == current;
            var changes =
                isCurrent ? repo.Status.ChangesCount
                : gitRepo.WorktreeChanges.TryGetValue(w.Path, out var count) ? count
                : -1;
            repo.Worktrees.Add(
                new Worktree(
                    w.Path,
                    w.Branch,
                    w.HeadId,
                    w.IsMain,
                    isCurrent,
                    w.IsDetached,
                    w.IsLocked,
                    w.LockReason,
                    w.IsPrunable,
                    w.PruneReason,
                    changes
                )
            );

            if (!isCurrent && w.Branch != "" && repo.Branches.TryGetValue(w.Branch, out var branch))
            {
                branch.WorktreePath = w.Path;
            }
        }

        // 'git branch' and 'git worktree list' are two readings of the same state, taken a moment
        // apart, so they can disagree; the list is what is acted on, this only says so in the log
        foreach (
            var b in repo.Branches.Values.Where(b => !b.IsRemote && b.IsCheckedOutElsewhere != (b.WorktreePath != ""))
        )
        {
            Log.Warn($"Branch {b.Name} checked out elsewhere: {b.IsCheckedOutElsewhere}, worktree: '{b.WorktreePath}'");
        }
    }

    static Git.Worktree? CurrentWorktree(WorkRepo repo, GitRepo gitRepo)
    {
        var byPath = gitRepo.Worktrees.FirstOrDefault(w => Files.IsSamePath(w.Path, gitRepo.Path));
        if (byPath != null)
            return byPath;

        var currentBranch = repo.Branches.Values.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch == null)
            return null;
        if (currentBranch.IsDetached)
            return gitRepo.Worktrees.FirstOrDefault(w => w.IsDetached && w.HeadId == currentBranch.TipID);
        return gitRepo.Worktrees.FirstOrDefault(w => w.Branch == currentBranch.Name);
    }

    static void AddAugCommits(WorkRepo repo, GitRepo gitRepo)
    {
        IReadOnlyList<Git.Commit> gitCommits = gitRepo.Commits;
        // For repositories with a lot of commits, only the latest 'truncateLimit' number of commits

        // are used, i.w. truncated commits, which should have parents, but they are unknown
        bool isTruncatedPossible = gitRepo.IsTruncated;
        bool isTruncatedNeeded = false;
        repo.Commits.Capacity = gitCommits.Count;

        // Iterate git commits in reverse, to ensure parents are added before children
        for (var i = gitCommits.Count - 1; i >= 0; i--)
        {
            Git.Commit gc = gitCommits[i];
            if (IsStashCommit(repo, gc))
                continue; // not shown in log view

            WorkCommit commit = new WorkCommit(gc);

            if (isTruncatedPossible)
            { // Check if parents need to be replaced with truncated commit
                isTruncatedNeeded = FixTruncatedParents(repo, isTruncatedNeeded, commit);
            }

            commit.GitIndex = i;
            repo.Commits.Add(commit);
            repo.CommitsById[commit.Id] = commit;
        }

        // Set current commit if there is a current branch with an existing tip
        Git.Branch? currentBranch = gitRepo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {
            if (repo.CommitsById.TryGetValue(currentBranch.TipID, out var currentCommit))
            {
                currentCommit.IsCurrent = true;
                currentCommit.IsDetached = currentBranch.IsDetached;
            }
        }

        // Since we added git commits from first to last, we need to reverse the list to get last on top
        repo.Commits.Reverse();

        if (isTruncatedNeeded)
        { // Add a virtual truncated commit, which some commits will have as a parent
            AddTruncatedVirtualCommit(repo);
        }
    }

    static bool IsStashCommit(WorkRepo repo, Git.Commit gc) => repo.StashById.ContainsKey(gc.Id);

    static void AddAugTags(WorkRepo repo, GitRepo gitRepo)
    {
        gitRepo.Tags.ForEach(t =>
        {
            var tag = new Tag(t.Name, t.CommitId);
            if (repo.CommitsById.TryGetValue(t.CommitId, out var c))
            {
                c.Tags.Add(tag);
            }
        });
    }

    static void AddAugStashes(WorkRepo repo, GitRepo gitRepo)
    {
        gitRepo.Stashes.ForEach(s =>
        {
            var stash = new Stash(s.Id, s.Name, s.Branch, s.ParentId, s.IndexId, s.Message);
            repo.Stashes.Add(stash);
            repo.StashById[s.Id] = stash;
            repo.StashById[s.IndexId] = stash;
        });
    }

    static void SetCommitHasStash(WorkRepo repo)
    {
        repo.Stashes.ForEach(s =>
        {
            if (repo.CommitsById.TryGetValue(s.ParentId, out var commit))
            {
                commit.HasStash = true;
            }
        });
    }

    void SetBranchViewNames(WorkRepo repo)
    {
        Dictionary<string, int> branchNameCount = [];

        repo.Branches.Values.Where(b => b.IsPrimary)
            .OrderBy(b => b.IsGitBranch ? 0 : 1)
            .ThenBy(b => repo.CommitsById[b.BottomID].AuthorTime)
            .ForEach(b =>
            {
                // Common name is the name of the branch based on bottom commit id (stable if branch is renamed)
                var bottom = repo.CommitsById[b.BottomID];
                b.PrimaryBaseName = bottom.Branch?.Name == b.Name ? $"{b.BottomID.Sid()}" : b.PrimaryName;

                if (branchNameCount.TryGetValue(b.NiceName, out var count))
                { // Multiple branches with same human name, add a counter to the human name
                    branchNameCount[b.NiceName] = ++count;
                    b.NiceNameUnique = $"{b.NiceName}({count})";
                }
                else
                { // First branch with this human name, setting view name to same
                    branchNameCount[b.NiceName] = 1;
                    b.NiceNameUnique = b.NiceName;
                }

                // Make sure local and pull merge branches have same view and base name as well
                if (b.LocalName != "")
                {
                    var localBranch = repo.Branches[b.LocalName]!;
                    localBranch.NiceNameUnique = b.NiceNameUnique;
                    localBranch.PrimaryBaseName = b.PrimaryBaseName;
                }
                SetNamesOnPullMergeChildren(repo, b, b);
            });
    }

    void SetNamesOnPullMergeChildren(WorkRepo repo, WorkBranch baseBranch, WorkBranch childBranch)
    {
        childBranch.PullMergeChildBranches.ForEach(pmb =>
        {
            pmb.NiceNameUnique = baseBranch.NiceNameUnique;
            pmb.PrimaryBaseName = baseBranch.PrimaryBaseName;
            SetNamesOnPullMergeChildren(repo, baseBranch, pmb);
        });
    }

    static bool FixTruncatedParents(WorkRepo repo, bool isTruncatedNeeded, WorkCommit commit)
    {
        // The repo was truncated, check if commits have missing parents, which will be set
        // to a virtual "truncated commit"
        if (commit.ParentIds.Count > 0)
        { // Check if first parent is missing and need a truncated commit parent
            if (!repo.CommitsById.TryGetValue(commit.ParentIds[0], out var _))
            {
                isTruncatedNeeded = true;
                commit.ParentIds[0] = gmd.Server.Repo.TruncatedLogCommitId;
            }
        }

        if (commit.ParentIds.Count > 1)
        { // Merge commit, check if second parent is missing and need a truncated commit parent
            if (!repo.CommitsById.TryGetValue(commit.ParentIds[1], out var _))
            {
                isTruncatedNeeded = true;
                commit.ParentIds[1] = gmd.Server.Repo.TruncatedLogCommitId;
            }
        }

        return isTruncatedNeeded;
    }

    static void AddTruncatedVirtualCommit(WorkRepo repo)
    {
        // Add a virtual truncated commit, which some commits will have as a parent
        string msg = "< ... log truncated, more commits exists ... >";
        WorkCommit pc = new WorkCommit(
            id: Repo.TruncatedLogCommitId,
            subject: msg,
            message: msg,
            author: "",
            authorTime: new DateTime(1, 1, 1),
            parentIds: []
        )
        {
            IsTruncatedLogCommit = true,
            GitIndex = repo.Commits.Count,
        };
        repo.Commits.Add(pc);
        repo.CommitsById[pc.Id] = pc;
    }
}
