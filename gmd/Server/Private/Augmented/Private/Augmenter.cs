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
    Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo, int truncatedLimit);
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

    public Task<WorkRepo> GetAugRepoAsync(GitRepo gitRepo, int truncateLimit)
    {
        return Task.Run(() => GetAugRepo(gitRepo, truncateLimit));
    }

    WorkRepo GetAugRepo(GitRepo gitRepo, int truncateLimit)
    {
        WorkRepo repo = new WorkRepo(gitRepo.TimeStamp, gitRepo.Path, ToStatus(gitRepo));

        SetAugStashes(repo, gitRepo);
        SetAugBranches(repo, gitRepo);
        SetAugCommits(repo, gitRepo, truncateLimit);
        branchStructureService.SetCommitBranches(repo, gitRepo);
        SetAugTags(repo, gitRepo);
        SetBranchByName(repo);
        AdjustDisplayNames(repo);

        return repo;
    }

    private void SetBranchByName(WorkRepo repo)
    {
        repo.Branches.ForEach(b => repo.BranchByName[b.Name] = b);
    }

    void AdjustDisplayNames(WorkRepo repo)
    {
        Dictionary<string, int> branchNameCount = new Dictionary<string, int>();

        repo.Branches
            .Where(b => b.RemoteName == "" && b.PullMergeParentBranch == null)
            .OrderBy(b => repo.CommitsById[b.BottomID].AuthorTime)
            .ForEach(b =>
        {
            var bottom = repo.CommitsById[b.BottomID];
            var parentCommitSid = bottom.FirstParent?.Sid ?? "";
            b.CommonBaseName = $"{b.BottomID.Sid()}:{parentCommitSid}";
            if (branchNameCount.TryGetValue(b.DisplayName, out var count))
            {   // Multiple branches with same display name, add a counter to the display name
                branchNameCount[b.DisplayName] = ++count;
                b.DisplayName = $"{b.DisplayName}({count})";
            }
            else
            {   // First branch with this display name
                branchNameCount[b.DisplayName] = 1;
            }

            // Make sure local and pull merge branches have same display and base name
            if (b.LocalName != "")
            {
                var localBranch = repo.BranchByName[b.LocalName]!;
                localBranch.DisplayName = b.DisplayName;
                localBranch.CommonBaseName = b.CommonBaseName;
            }
            b.PullMergeChildBranches.ForEach(pmb =>
            {
                var br = repo.BranchByName[pmb.Name]!;
                br.DisplayName = b.DisplayName;
                br.CommonBaseName = b.CommonBaseName;
            });
        });
    }

    Status ToStatus(GitRepo repo)
    {
        var s = repo.Status;
        return new Status(s.Modified, s.Added, s.Deleted, s.Conflicted,
            s.IsMerging, s.MergeMessage, s.MergeHeadId, s.ModifiedFiles, s.AddedFiles,
            s.DeletedFiles, s.ConflictsFiles);
    }

    void SetAugBranches(WorkRepo repo, GitRepo gitRepo)
    {
        repo.Branches.AddRange(gitRepo.Branches.Select(b => new WorkBranch(b)));

        // Set local name of all remote branches, that have a corresponding local branch as well
        // Unset RemoteName of local branch if no corresponding remote branch (deleted on remote server)
        foreach (var b in repo.Branches)
        {
            if (b.RemoteName != "")
            {
                var remoteBranch = repo.Branches.Find(bb => bb.Name == b.RemoteName);
                if (remoteBranch != null)
                {   // Corresponding remote branch, set local branch name property
                    remoteBranch.LocalName = b.Name;
                    if (b.IsCurrent)
                    {
                        remoteBranch.IsLocalCurrent = true;
                    }
                }
                else
                {   // No remote corresponding remote branch, unset property
                    b.RemoteName = "";
                }
            }
        }
    }

    void SetAugCommits(WorkRepo repo, GitRepo gitRepo, int truncateLimit)
    {
        IReadOnlyList<GitCommit> gitCommits = gitRepo.Commits;
        // For repositories with a lot of commits, only the latest 'truncateLimit' number of commits

        // are used, i.w. truncated commits, which should have parents, but they are unknown
        bool isTruncatedPossible = gitCommits.Count >= truncateLimit;
        bool isTruncatedNeeded = false;
        repo.Commits.Capacity = gitCommits.Count;

        // Iterate git commits in reverse
        for (var i = gitCommits.Count - 1; i >= 0; i--)
        {
            GitCommit gc = gitCommits[i];
            if (repo.StashById.ContainsKey(gc.Id))
            {
                // Skip stash commits, will not be shown in log view 
                continue;
            }
            WorkCommit commit = new WorkCommit(gc);

            if (isTruncatedPossible)
            {
                // The repo was truncated, check if commits have missing parents, which will be set
                // to a virtual/fake "truncated commit"
                if (commit.ParentIds.Count > 0)
                {
                    // Not a merge commit but check if parent is missing and need a truncated commit parent
                    if (!repo.CommitsById.TryGetValue(commit.ParentIds[0], out var parent))
                    {
                        isTruncatedNeeded = true;
                        commit.ParentIds[0] = Repo.TruncatedLogCommitID;
                    }
                }

                if (commit.ParentIds.Count > 1)
                {
                    // Merge commit, check if parents are missing and need a truncated commit parent
                    if (!repo.CommitsById.TryGetValue(commit.ParentIds[1], out var parent))
                    {
                        isTruncatedNeeded = true;
                        commit.ParentIds[1] = Repo.TruncatedLogCommitID;
                    }
                }
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
        {
            // Add a virtual/fake truncated commit, which some commits will have as a parent
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


    void SetAugTags(WorkRepo repo, GitRepo gitRepo)
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


    void SetAugStashes(WorkRepo repo, GitRepo gitRepo)
    {
        gitRepo.Stashes.ForEach(s =>
        {
            var stash = new Stash(s.Id, s.Name, s.Branch, s.parentId, s.indexId, s.Message);
            repo.Stashes.Add(stash);
            repo.StashById[s.Id] = stash;
            repo.StashById[s.indexId] = stash;
        });
    }
}

