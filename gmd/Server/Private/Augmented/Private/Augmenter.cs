using GitCommit = gmd.Git.Commit;
using GitBranch = gmd.Git.Branch;

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
    readonly IBranchNameService branchNameService;
    private readonly IBranchStructureService branchStructureService;


    internal Augmenter(
        IBranchNameService branchNameService,
        IBranchStructureService branchStructureService)
    {
        this.branchNameService = branchNameService;
        this.branchStructureService = branchStructureService;
    }

    public Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo)
    {   // Run in background thread to not block the main thread since 
        // this can take a long time for large repos and could be CPU intensive 
        return Task.Run(() => GetAugRepo(gitRepo));
    }


    WorkRepo GetAugRepo(GitRepo gitRepo)
    {
        var status = ToStatus(gitRepo);
        WorkRepo repo = new WorkRepo(gitRepo.TimeStamp, gitRepo.Path, status);

        AddAugStashes(repo, gitRepo); // Must be done before adding augmented commits
        AddAugBranches(repo, gitRepo);
        AddAugCommits(repo, gitRepo);
        AddAugTags(repo, gitRepo);

        branchStructureService.DetermineCommitBranches(repo, gitRepo);
        SetBranchViewNames(repo);
        return repo;
    }


    void AddAugBranches(WorkRepo repo, GitRepo gitRepo)
    {
        // Convert git branches to initial augmented branches
        gitRepo.Branches.ForEach(b => repo.Branches[b.Name] = new WorkBranch(b));

        // Set local name of all remote branches, that have a corresponding local branch as well
        // Unset RemoteName of local branch if no corresponding remote branch (deleted on remote server)
        foreach (var b in repo.Branches.Values)
        {
            if (b.RemoteName != "")
            {   // A local branch which has a corresponding remote branch             
                if (repo.Branches.TryGetValue(b.RemoteName, out var remoteBranch))
                {
                    b.PrimaryName = b.RemoteName;
                    b.IsPrimary = false;
                    remoteBranch.RelatedBranches.Add(b);  // Adds itself to primary branch related branches
                    remoteBranch.LocalName = b.Name;
                    if (b.IsCurrent)
                    {   // Local branch is current, set property on remote branch as well
                        remoteBranch.IsLocalCurrent = true;
                    }
                }
                else
                {   // No corresponding remote branch for local branch (deleted), unset property
                    b.RemoteName = "";
                    b.PrimaryName = b.Name;
                    b.IsPrimary = true;
                    b.RelatedBranches.Add(b);  // Adds itself to related branches
                }
            }
            else
            {   // Remote branch or a local branch without a corresponding remote branch
                b.PrimaryName = b.Name;
                b.IsPrimary = true;
                b.RelatedBranches.Add(b);  // Adds itself to related branches
            }
        }
    }


    void AddAugCommits(WorkRepo repo, GitRepo gitRepo)
    {
        IReadOnlyList<GitCommit> gitCommits = gitRepo.Commits;
        // For repositories with a lot of commits, only the latest 'truncateLimit' number of commits

        // are used, i.w. truncated commits, which should have parents, but they are unknown
        bool isTruncatedPossible = gitRepo.IsTruncated;
        bool isTruncatedNeeded = false;
        repo.Commits.Capacity = gitCommits.Count;

        // Iterate git commits in reverse, to ensure parents are added before children
        for (var i = gitCommits.Count - 1; i >= 0; i--)
        {
            GitCommit gc = gitCommits[i];
            if (IsStashCommit(repo, gc)) continue;   // not shown in log view

            WorkCommit commit = new WorkCommit(gc);

            if (isTruncatedPossible)
            {   // Check if parents need to be replaced with truncated commit
                isTruncatedNeeded = FixTruncatedParents(repo, isTruncatedNeeded, commit);
            }

            commit.GitIndex = i;
            repo.Commits.Add(commit);
            repo.CommitsById[commit.Id] = commit;
        }

        // Set current commit if there is a current branch with an existing tip
        GitBranch? currentBranch = gitRepo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {
            if (repo.CommitsById.TryGetValue(currentBranch.TipID, out var currentCommit))
            {
                currentCommit.IsCurrent = true;
                currentCommit.IsDetached = currentBranch.IsDetached;
            }
        }

        repo.Commits.Reverse();

        if (isTruncatedNeeded)
        {   // Add a virtual truncated commit, which some commits will have as a parent
            AddTruncatedVirtualCommit(repo);
        }
    }

    bool IsStashCommit(WorkRepo repo, GitCommit gc) => repo.StashById.ContainsKey(gc.Id);


    void AddAugTags(WorkRepo repo, GitRepo gitRepo)
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


    void AddAugStashes(WorkRepo repo, GitRepo gitRepo)
    {
        gitRepo.Stashes.ForEach(s =>
        {
            var stash = new Stash(s.Id, s.Name, s.Branch, s.parentId, s.indexId, s.Message);
            repo.Stashes.Add(stash);
            repo.StashById[s.Id] = stash;
            repo.StashById[s.indexId] = stash;
        });
    }


    void SetBranchViewNames(WorkRepo repo)
    {
        Dictionary<string, int> branchNameCount = new Dictionary<string, int>();

        repo.Branches.Values
            .Where(b => b.IsPrimary)
            .OrderBy(b => b.IsGitBranch ? 0 : 1)
            .ThenBy(b => repo.CommitsById[b.BottomID].AuthorTime)
            .ForEach(b =>
        {
            // Common name is the name of the branch based on bottom commit id (stable if branch is renamed)
            var bottom = repo.CommitsById[b.BottomID];
            b.PrimaryBaseName = bottom.Branch?.Name == b.Name ? $"{b.BottomID.Sid()}" : b.PrimaryName;

            if (branchNameCount.TryGetValue(b.NiceName, out var count))
            {   // Multiple branches with same human name, add a counter to the human name
                branchNameCount[b.NiceName] = ++count;
                b.NiceNameUnique = $"{b.NiceName}({count})";
            }
            else
            {   // First branch with this human name, setting view name to same
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


    Status ToStatus(GitRepo repo)
    {
        var s = repo.Status;
        return new Status(s.Modified, s.Added, s.Deleted, s.Conflicted, s.Renamed,
            s.IsMerging, s.MergeMessage, s.MergeHeadId, s.ModifiedFiles, s.AddedFiles,
            s.DeletedFiles, s.ConflictsFiles, s.RenamedSourceFiles, s.RenamedTargetFiles);
    }

    static bool FixTruncatedParents(WorkRepo repo, bool isTruncatedNeeded, WorkCommit commit)
    {
        // The repo was truncated, check if commits have missing parents, which will be set
        // to a virtual "truncated commit"
        if (commit.ParentIds.Count > 0)
        {   // Check if first parent is missing and need a truncated commit parent
            if (!repo.CommitsById.TryGetValue(commit.ParentIds[0], out var parent))
            {
                isTruncatedNeeded = true;
                commit.ParentIds[0] = Repo.TruncatedLogCommitID;
            }
        }

        if (commit.ParentIds.Count > 1)
        {   // Merge commit, check if second parent is missing and need a truncated commit parent
            if (!repo.CommitsById.TryGetValue(commit.ParentIds[1], out var parent))
            {
                isTruncatedNeeded = true;
                commit.ParentIds[1] = Repo.TruncatedLogCommitID;
            }
        }

        return isTruncatedNeeded;
    }

    static void AddTruncatedVirtualCommit(WorkRepo repo)
    {
        // Add a virtual truncated commit, which some commits will have as a parent
        string msg = "< ... log truncated, more commits exists ... >";
        WorkCommit pc = new WorkCommit(
            id: Repo.TruncatedLogCommitID, subject: msg, message: msg,
            author: "", authorTime: new DateTime(1, 1, 1), parentIds: new string[0]);
        pc.IsTruncatedLogCommit = true;
        pc.GitIndex = repo.Commits.Count;
        repo.Commits.Add(pc);
        repo.CommitsById[pc.Id] = pc;
    }
}

