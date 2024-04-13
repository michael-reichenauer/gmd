using gmd.Cui.RepoView;
using gmd.Git;
using GitStatus = gmd.Git.Status;


namespace gmd.Server.Private.Augmented.Private;

// AugmentedRepoService returns augmented repos of git repo information, The augmentations 
// adds information not available in git directly, but can be inferred by parsing the 
// git information. 
// Examples of augmentation is which branch a commits belongs to and the hierarchical structure
// of branches. 
[SingleInstance]
class AugmentedService : IAugmentedService
{
    const int maxCommitCount = 30000; // Increase performance in case of very large repos

    readonly IGit git;
    readonly IAugmenter augmenter;
    readonly IConverter converter;
    readonly IFileMonitor fileMonitor;
    readonly IMetaDataService metaDataService;

    internal AugmentedService(
        IGit git,
        IAugmenter augmenter,
        IConverter converter,
        IFileMonitor fileMonitor,
        IMetaDataService metaDataService)
    {
        this.git = git;
        this.augmenter = augmenter;
        this.converter = converter;
        this.fileMonitor = fileMonitor;
        this.metaDataService = metaDataService;
        fileMonitor.FileChanged += e => StatusChange?.Invoke(e);
        fileMonitor.RepoChanged += e => RepoChange?.Invoke(e);
    }

    public event Action<ChangeEvent>? RepoChange;
    public event Action<ChangeEvent>? StatusChange;


    // GetRepoAsync returns an augmented repo based on new git info like branches, commits, ...
    public async Task<R<Repo>> GetRepoAsync(string path)
    {
        if (!Try(out var rootPath, out var e, git.RootPath(path))) return e;

        // Get a fresh new git repo (branches, commits, tags, status, ...)
        if (!Try(out var gitRepo, out e, await GetGitRepoAsync(rootPath))) return e;

        // Return an augmented repo
        return await GetAugmentedRepoAsync(gitRepo);
    }


    // GetRepoAsync returns the updated augmented repo with git status .
    public async Task<R<Repo>> UpdateRepoStatusAsync(Repo repo)
    {
        // Get latest git status
        if (!Try(out var gitStatus, out var e, await GetGitStatusAsync(repo.Path))) return e;
        Log.Info($"Git status {gitStatus}");

        // Returns the augmented repo with the new status
        return GetUpdatedAugmentedRepoStatus(repo, gitStatus);
    }

    public async Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.CommitAllChangesAsync(message, isAmend, wd);
        }
    }

    public async Task<R> FetchAsync(string path)
    {
        // using (Timing.Start("Fetched"))
        {
            // pull meta data, but ignore error, if error is key not exist, it can be ignored,
            // if error is remote error, the following fetch will handle that
            // Start both tasks in parallel and await later
            var metaDataTask = metaDataService.FetchMetaDataAsync(path);
            var fetchTask = git.FetchAsync(path);

            await Task.WhenAll(metaDataTask, fetchTask);

            // Return the result of the fetch task (ignoring the metaData result)
            return fetchTask.Result;
        }
    }

    public Task<R> FetchMetaDataAsync(string path) => metaDataService.FetchMetaDataAsync(path);


    // GetGitRepoAsync returns a fresh git repo info object with commits, branches, ...
    async Task<R<GitRepo>> GetGitRepoAsync(string path)
    {
        Timing t = Timing.Start();

        var timeStamp = DateTime.UtcNow;
        fileMonitor.SetReadRepoTime(timeStamp);

        // Start some git commands in parallel to get commits, branches, status, ...
        var logTask = git.GetLogAsync(maxCommitCount, path);
        var branchesTask = git.GetBranchesAsync(path);
        var tagsTask = git.GetTagsAsync(path);
        var statusTask = git.GetStatusAsync(path);
        var metaDataTask = metaDataService.GetMetaDataAsync(path);
        var stashesTask = git.GetStashesAsync(path);
        await Task.WhenAll(logTask, branchesTask, tagsTask, statusTask, metaDataTask, stashesTask);

        // Check all tasks for errors
        if (!Try(out var log, out var e, logTask.Result)) return e;
        if (!Try(out var branches, out e, branchesTask.Result)) return e;
        if (!Try(out var tags, out e, tagsTask.Result)) return e;
        if (!Try(out var status, out e, statusTask.Result)) return e;
        if (!Try(out var metaData, out e, metaDataTask.Result)) return e;
        if (!Try(out var stashes, out e, stashesTask.Result)) return e;

        var isTruncated = log.Count == maxCommitCount;
        if (log.Count == 0) return EmptyGitRepo(path, tags, status, metaData);

        // Combine all git info into one git repo info object
        var gitRepo = new GitRepo(timeStamp, path, log, branches, tags, status, metaData, stashes, isTruncated);
        Log.Info($"GitRepo {t} {gitRepo}");

        return gitRepo;
    }

    public async Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd)
    {
        Log.Info($"Create branch {newBranchName} ...");
        Commit? currentCommit = null;
        var currentBranch = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        if (currentBranch != null)
        {
            currentCommit = repo.CommitById[currentBranch.TipId];
        }

        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.CreateBranchAsync(newBranchName, isCheckout, wd))) return e;

            if (currentCommit == null || currentBranch == null) return R.Ok;

            // Get the latest meta data
            if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd))) return e;

            metaData.SetBranched(currentCommit.Sid, currentBranch.NiceName);
            return await metaDataService.SetMetaDataAsync(wd, metaData);
        }
    }

    public async Task<R> CreateBranchFromBranchAsync(Repo repo, string newBranchName, string sourceBranch, bool isCheckout, string wd)
    {
        Log.Info($"Create branch {newBranchName} ...");

        var source = repo.BranchByName[sourceBranch];

        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.CreateBranchFromCommitAsync(newBranchName, source.TipId, isCheckout, wd))) return e;

            // Get the latest meta data
            if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd))) return e;

            metaData.SetBranched(source.TipId, source.NiceName);
            return await metaDataService.SetMetaDataAsync(wd, metaData);
        }
    }

    public async Task<R> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd)
    {
        Log.Info($"Create branch {newBranchName} from {sha} ...");
        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.CreateBranchFromCommitAsync(newBranchName, sha, isCheckout, wd))) return e;

            Commit commit = repo.CommitById[sha];
            var branch = repo.BranchByName[commit.BranchName];

            // Get the latest meta data
            if (!Try(out var metaData, out e, await metaDataService.GetMetaDataAsync(wd))) return e;

            metaData.SetBranched(commit.Sid, branch.NiceName);
            return await metaDataService.SetMetaDataAsync(wd, metaData);
        }
    }

    public async Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name)
    {
        if (repo.CommitById.TryGetValue(name, out var commit))
        {   // Merging from a commit
            if (!Try(out var e2, await git.MergeBranchAsync(commit.Id, repo.Path))) return e2;
            if (!Try(out var commits2, out e2, await git.GetMergeLogAsync(commit.Id, repo.Path))) return e2;
            return ToMergeCommits(repo, commits2).ToList();
        }

        var branch = repo.BranchByName[name];
        var tip = repo.CommitById[branch.TipId];
        var mergeName = branch.Name;

        if (branch.LocalName != "")
        {   // Branch is a remote branch with an existing local branch, which might have a younger tip
            var localBranch = repo.BranchByName[branch.LocalName];
            var localTip = repo.CommitById[localBranch.TipId];
            if (localTip.AuthorTime >= tip.AuthorTime)
            {   // The local branch is younger or same, use that.
                mergeName = localBranch.Name;
            }
        }
        else if (branch.RemoteName != "")
        {   // Branch is a local branch with an existing remote branch, which might have a younger tip
            var remoteBranch = repo.BranchByName[branch.RemoteName];
            var remoteTip = repo.CommitById[remoteBranch.TipId];
            if (remoteTip.AuthorTime >= tip.AuthorTime)
            {   // The remote branch is younger or same, use that.
                mergeName = remoteBranch.Name;
            }
        }

        if (!Try(out var e, await git.MergeBranchAsync(mergeName, repo.Path))) return e;
        if (!Try(out var commits, out e, await git.GetMergeLogAsync(mergeName, repo.Path))) return e;
        return ToMergeCommits(repo, commits).ToList();
    }

    public async Task<R> RebaseBranchAsync(Repo repo, string name)
    {
        using (fileMonitor.Pause())
        {
            var cb = repo.CurrentBranch();
            var primaryCurrent = repo.BranchByName[cb.PrimaryName];
            var oldBase = primaryCurrent.BottomId;

            var ontoBranch = repo.BranchByName[name];
            var ontoTip = repo.CommitById[ontoBranch.TipId];

            var newBase = ontoBranch.Name;

            if (ontoBranch.LocalName != "")
            {   // Branch is a remote branch with an existing local branch, which might have a younger tip
                var localBranch = repo.BranchByName[ontoBranch.LocalName];
                var localTip = repo.CommitById[localBranch.TipId];
                if (localTip.AuthorTime >= ontoTip.AuthorTime)
                {   // The local branch is younger or same, use that.
                    newBase = localBranch.Name;
                }
            }
            else if (ontoBranch.RemoteName != "")
            {   // Branch is a local branch with an existing remote branch, which might have a younger tip
                var remoteBranch = repo.BranchByName[ontoBranch.RemoteName];
                var remoteTip = repo.CommitById[remoteBranch.TipId];
                if (remoteTip.AuthorTime >= ontoTip.AuthorTime)
                {   // The remote branch is younger or same, use that.
                    newBase = remoteBranch.Name;
                }
            }

            if (!Try(out var e, await git.RebaseOntoAsync(newBase, $"{oldBase}~", repo.Path))) return e;

            if (cb.RemoteName != "")
            {   // Current Branch is local branch with a remote branch, push it with force
                if (!Try(out e, await git.PushCurrentBranchAsync(true, repo.Path))) return e;
            }

            return R.Ok;
        }
    }

    public async Task<R> SwitchToAsync(Repo repo, string branchName)
    {
        var branch = repo.BranchByName[branchName];
        if (branch.IsGitBranch)
        {
            return await git.CheckoutAsync(branchName, repo.Path);
        }

        // Not a git branch so the branch was deleted, lets recreate it
        var tip = repo.CommitById[branch.TipId];

        return await CreateBranchFromCommitAsync(repo, branch.NiceName, tip.Id, true, repo.Path);
    }

    public async Task<R> SetBranchManuallyAsync(Repo repo, string commitId, string setNiceName)
    {
        Log.Info($"Set {commitId.Sid()} to {setNiceName} ...");

        using (fileMonitor.Pause())
        {
            // Get the latest meta data
            if (!Try(out var metaData, out var e, await metaDataService.GetMetaDataAsync(repo.Path))) return e;

            metaData.SetCommitBranch(commitId.Sid(), setNiceName);
            return await metaDataService.SetMetaDataAsync(repo.Path, metaData);
        }
    }

    public async Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName)
    {
        var branch = repo.BranchByName[branchName];
        var ambiguousTip = branch.AmbiguousTipId;
        Log.Info($"Resolve {ambiguousTip.Sid()} of {branchName} to {setHumanName} ...");

        using (fileMonitor.Pause())
        {
            // Get the latest meta data
            if (!Try(out var metaData, out var e, await metaDataService.GetMetaDataAsync(repo.Path))) return e;

            metaData.SetCommitBranch(ambiguousTip.Sid(), setHumanName);
            return await metaDataService.SetMetaDataAsync(repo.Path, metaData);
        }
    }


    public async Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId)
    {
        using (fileMonitor.Pause())
        {
            // Get the latest meta data
            if (!Try(out var metaData, out var e, await metaDataService.GetMetaDataAsync(repo.Path))) return e;

            metaData.RemoveCommitBranch(commitId.Sid());

            return await metaDataService.SetMetaDataAsync(repo.Path, metaData);
        }
    }


    public Task<R> PushMetaDataAsync(string wd) =>
        metaDataService.PushMetaDataAsync(wd);


    public async Task<R> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.AddTagAsync(name, commitId, wd))) return e;
            if (!hasRemoteBranch) return R.Ok;
            return await git.PushTagAsync(name, wd);
        }
    }


    public async Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (!Try(out var e, await git.RemoveTagAsync(name, wd))) return e;
            if (!hasRemoteBranch) return R.Ok;
            return await git.DeleteRemoteTagAsync(name, wd);
        }
    }


    IEnumerable<Commit> ToMergeCommits(Repo repo, IReadOnlyList<Git.Commit> commits)
    {
        if (commits.Count == 0) return Enumerable.Empty<Commit>();
        var branchName = repo.CommitById[commits[0].Id].BranchPrimaryName;

        return commits
            .Select(c => repo.CommitById[c.Id])
            .Where(c => c.BranchPrimaryName == branchName);
    }

    // GetGitStatusAsync returns a fresh git status
    async Task<R<GitStatus>> GetGitStatusAsync(string path)
    {
        fileMonitor.SetReadStatusTime(DateTime.UtcNow);
        if (!Try(out var gitStatus, out var e, await git.GetStatusAsync(path))) return e;
        return gitStatus;
    }

    // GetAugmentedRepoAsync returns an augmented git repo, and monitors working folder changes
    async Task<R<Repo>> GetAugmentedRepoAsync(GitRepo gitRepo)
    {
        fileMonitor.Monitor(gitRepo.Path);

        Timing t = Timing.Start();
        WorkRepo augRepo = await augmenter.GetAugRepoAsync(gitRepo);

        var repo = converter.ToRepo(augRepo);
        repo = AdjustUncommitted(repo);
        Log.Info($"Augmented {t} {repo}");
        return repo;
    }


    // GetUpdatedAugmentedRepoStatus an updated augmented repo with new status
    Repo GetUpdatedAugmentedRepoStatus(Repo repo, GitStatus gitStatus)
    {
        Timing t = Timing.Start();
        var status = converter.ToStatus(gitStatus);
        repo = repo with { Status = status, ViewBranches = new List<Branch>(), ViewCommits = new List<Commit>() };
        repo = AdjustUncommitted(repo);
        Log.Info($"Augmented {t} {repo}");
        return repo;
    }

    Repo AdjustUncommitted(Repo repo)
    {
        if (repo.Status.IsOk && !repo.CommitById.ContainsKey(Repo.UncommittedId)) return repo;

        if (repo.Status.IsOk)
        {   // Need to remove uncommitted commit
            var uncommitted = repo.CommitById[Repo.UncommittedId];
            Commit? parent = uncommitted.ParentIds.Any() ? repo.CommitById[uncommitted.ParentIds[0]] : null;
            if (parent != null)
            {   // Uncommitted has a parent, need to adjust parents children
                parent = parent with
                {
                    AllChildIds = parent.AllChildIds.Where(c => c != uncommitted.Id).ToList(),
                    FirstChildIds = parent.FirstChildIds.Where(c => c != uncommitted.Id).ToList(),
                    MergeChildIds = parent.MergeChildIds.Where(c => c != uncommitted.Id).ToList()
                };
            }

            var commits = repo.AllCommits
                .Where(c => c.Id != Repo.UncommittedId)
                .Select(c => c.Id == parent?.Id ? parent : c)
                .ToList();
            var commitsById = commits.ToDictionary(c => c.Id);

            var branches = repo.AllBranches
                .Select(b => b.TipId == Repo.UncommittedId ? b with { TipId = parent?.Id ?? "" } : b)
                .ToList();
            var branchByName = branches.ToDictionary(b => b.Name);

            return repo with { AllCommits = commits, CommitById = commitsById, AllBranches = branches, BranchByName = branchByName };
        }

        var newUncommitted = GetUncommittedCommit(repo);

        // Status is not ok, need to add or update commit
        if (repo.CommitById.TryGetValue(Repo.UncommittedId, out var uncommitted2))
        {   // Uncommitted commit exists, need to update it
            var commits = repo.AllCommits
                .Select(c => c.Id == Repo.UncommittedId ? newUncommitted : c)
                .ToList();
            var commitsById = commits.ToDictionary(c => c.Id);

            return repo with { AllCommits = commits, CommitById = commitsById, };
        }

        // Uncommitted commit does not exist, need to add it
        var commits2 = repo.AllCommits.Prepend(newUncommitted).ToList();
        var commitsById2 = commits2.ToDictionary(c => c.Id);

        var branches2 = repo.AllBranches
            .Select(b =>
            {
                if (b.Name != newUncommitted.BranchName) return b;
                if (b.TipId == b.BottomId) return b with { TipId = Repo.UncommittedId, BottomId = Repo.UncommittedId };
                return b with { TipId = Repo.UncommittedId };
            })
            .ToList();
        var branchByName2 = branches2.ToDictionary(b => b.Name);

        return repo with { AllCommits = commits2, CommitById = commitsById2, AllBranches = branches2, BranchByName = branchByName2 };
    }


    static Commit GetUncommittedCommit(Repo repo)
    {
        var currentBranch = repo.AllBranches.First(b => b.IsCurrent);

        var current = repo.CommitById[currentBranch.TipId];
        if (current.IsUncommitted)
        {   // First commit is uncommitted, so use its parent as local tip
            current = repo.CommitById[current.ParentIds[0]];
        }

        var parentIds = new List<string>() { current.Id };

        if (repo.Status.MergeHeadId != "")
        {   // Merge in progress, add the source merge id as a merge parent to the uncommitted commit
            var mergeHead = repo.CommitById[repo.Status.MergeHeadId];
            parentIds.Add(repo.Status.MergeHeadId);
        }

        string subject = $"{repo.Status.ChangesCount} uncommitted changes";
        if (repo.Status.IsMerging && repo.Status.MergeMessage != "")
        {   // Merge in progress
            subject = $"{repo.Status.MergeMessage}, {subject}";
        }
        if (repo.Status.Conflicted > 0)
        {   // Conflicts exists
            subject = $"CONFLICTS: {repo.Status.Conflicted}, {subject}";
        }

        // Create a new virtual uncommitted commit
        return new Commit(
            Id: Repo.UncommittedId, Sid: Repo.UncommittedId.Sid(),
            Subject: subject, Message: subject, Author: "", AuthorTime: DateTime.Now,
            IsInView: true, ViewIndex: 0, GitIndex: 0, currentBranch.Name, currentBranch.PrimaryName, currentBranch.NiceNameUnique,
            ParentIds: parentIds, AllChildIds: new List<string>(), FirstChildIds: new List<string>(), MergeChildIds: new List<string>(),
            Tags: new List<Tag>(), BranchTips: new List<string>(),
            IsCurrent: false, IsDetached: false, IsUncommitted: true, IsConflicted: repo.Status.Conflicted > 0,
            IsAhead: false, IsBehind: false,
            IsTruncatedLogCommit: false, IsAmbiguous: false, IsAmbiguousTip: false,
            IsBranchSetByUser: false, HasStash: false, More.None);
    }

    R<GitRepo> EmptyGitRepo(string path, IReadOnlyList<Git.Tag> tags, GitStatus status, MetaData metaData)
    {
        Timing t = Timing.Start();
        var id = Repo.EmptyRepoCommitId;
        var msg = "<... empty repo ...>";
        var branchName = "main";
        var commit = new Git.Commit(id, id.Sid(), new string[0], msg, msg, "", DateTime.UtcNow, DateTime.UtcNow);
        var branch = new Git.Branch(branchName, id, true, false, "", false, 0, 0);
        var commits = new List<Git.Commit>() { commit };
        var branches = new List<Git.Branch>() { branch };
        var stashes = new List<Git.Stash>();

        var gitRepo = new GitRepo(DateTime.UtcNow, path, commits, branches, tags, status, metaData, stashes, false);
        Log.Info($"GitRepo {t} {gitRepo}");
        return gitRepo;
    }
}
