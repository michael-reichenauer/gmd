using gmd.Common;
using gmd.Git;
using GitStatus = gmd.Git.Status;

namespace gmd.Server.Private.Augmented.Private;

// The augmented repo: what git has, read with one git command per kind in parallel, plus what gmd
// infers from it and git does not record, i.e. which branch each commit is on and how the branches
// relate (BranchStructureService), and what the reflogs say (undo steps, remote rewrites).
[SingleInstance]
class AugmentedService : IAugmentedService
{
    // The most commits read, the newest, which bounds the cost of every read of a large repo: a
    // refresh costs some 1.2 to 2.6 s per 100,000 commits, and each commit kept 2 to 3 KB, measured
    // over git, kubernetes, rust and Linux (see RefreshTimingTest). 50,000 reach 13 years back in git,
    // 5 in kubernetes and some months in Linux.
    internal const int MaxCommitCount = 50000;

    // The repos whose commit-graph file was looked for this session, see WriteCommitGraphIfMissing
    readonly HashSet<string> commitGraphPaths = [];

    // The commits the reflogs mention that are looked up as maybe lost, the newest: each ref's
    // reflog is latest first, and a reflog of years can name many thousands
    const int maxLostCandidates = 1200;

    readonly IGit git;
    readonly IAugmenter augmenter;
    readonly IWorkRepoConverter converter;
    readonly IFileMonitor fileMonitor;
    readonly IMetaDataService metaDataService;
    readonly IRepoConfigService repoConfig;

    internal AugmentedService(
        IGit git,
        IAugmenter augmenter,
        IWorkRepoConverter converter,
        IFileMonitor fileMonitor,
        IMetaDataService metaDataService,
        IRepoConfigService repoConfig
    )
    {
        this.repoConfig = repoConfig;
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

    public async Task<Result<Repo>> GetRepoAsync(string path)
    {
        var rootPathResult = git.RootPath(path);
        if (rootPathResult is not string rootPath)
            return rootPathResult.Error;

        // Get a fresh new git repo (branches, commits, tags, status, ...)
        var gitRepoResult = await GetGitRepoAsync(rootPath);
        if (gitRepoResult is not GitRepo gitRepo)
            return gitRepoResult.Error;

        // Return an augmented repo
        return await GetAugmentedRepoAsync(gitRepo);
    }

    public async Task<Result<Repo>> UpdateRepoStatusAsync(Repo repo)
    {
        // Get latest git status
        var statusResult = await GetGitStatusAsync(repo.Path);
        if (statusResult is not GitStatus gitStatus)
            return statusResult.Error;
        Log.Info($"Git status {gitStatus}");

        // Returns the augmented repo with the new status
        return GetUpdatedAugmentedRepoStatus(repo, gitStatus);
    }

    // The worktrees read on their own: the list, and the status of each of the others, which a
    // freshly read repo does not have (its counts are unknown until this is called), so that a
    // status run in another worktree never delays showing this one. Only the worktrees change; the
    // branches keep the worktree paths they have, since a worktree appearing or going is a repo
    // change the file monitor reloads everything for. Which worktree is the current one does not
    // change between two reads of the same repo, so it is kept from the last one.
    public async Task<Result<Repo>> GetUpdatedWorktreesRepoAsync(Repo repo)
    {
        var worktreesResult = await git.GetWorktreesAsync(repo.Path);
        if (worktreesResult is not IReadOnlyList<Git.Worktree> gitWorktrees)
            return worktreesResult.Error;
        var changes = await GetWorktreeChangesAsync(gitWorktrees, repo.Path);

        var currentPath = repo.Worktrees.FirstOrDefault(w => w.IsCurrent)?.Path ?? repo.Path;
        var worktrees = gitWorktrees
            .Select(w =>
            {
                var isCurrent = Files.IsSamePath(w.Path, currentPath);
                var count =
                    isCurrent ? repo.Status.ChangesCount
                    : changes.TryGetValue(w.Path, out var n) ? n
                    : -1;
                return GitConverter.ToWorktree(w, isCurrent, count);
            })
            .ToList();

        return repo with
        {
            Worktrees = worktrees,
        };
    }

    // Fetches the branches and, beside them, the metadata, whose failure is not the fetch's: there is
    // none on origin until someone pushes it, and a remote that fails fails the fetch as well
    public async Task<Result> FetchAsync(string path)
    {
        var metaDataTask = metaDataService.FetchMetaDataAsync(path);
        var fetchTask = git.FetchAsync(path);
        await Task.WhenAll(metaDataTask, fetchTask);

        return fetchTask.Result;
    }

    // What git has: the commits, branches, tags, status, stashes, worktrees and reflogs, read in parallel
    async Task<Result<GitRepo>> GetGitRepoAsync(string path)
    {
        Timing t = Timing.Start();

        var timeStamp = DateTime.UtcNow;
        fileMonitor.ReadingRepo();

        // Start some git commands in parallel to get commits, branches, status, ...
        var logTask = git.GetLogAsync(MaxCommitCount, path);
        var branchesTask = git.GetBranchesAsync(path);
        var tagsTask = git.GetTagsAsync(path);
        var statusTask = git.GetStatusAsync(path);
        var metaDataTask = metaDataService.GetMetaDataAsync(path);
        var stashesTask = git.GetStashesAsync(path);
        var worktreesTask = git.GetWorktreesAsync(path);
        var reflogTask = git.GetReflogAsync(path);
        var authorTask = git.GetCurrentAuthorAsync(path);
        await Task.WhenAll(
            logTask,
            branchesTask,
            tagsTask,
            statusTask,
            metaDataTask,
            stashesTask,
            worktreesTask,
            reflogTask,
            authorTask
        );

        // Check all tasks for errors
        if (logTask.Result is not IReadOnlyList<Git.Commit> log)
            return logTask.Result.Error;
        if (branchesTask.Result is not IReadOnlyList<Git.Branch> branches)
            return branchesTask.Result.Error;
        if (tagsTask.Result is not IReadOnlyList<Git.Tag> tags)
            return tagsTask.Result.Error;
        if (statusTask.Result is not GitStatus status)
            return statusTask.Result.Error;
        if (metaDataTask.Result is not MetaData metaData)
            return metaDataTask.Result.Error;
        if (stashesTask.Result is not IReadOnlyList<Git.Stash> stashes)
            return stashesTask.Result.Error;

        // The worktrees are extra: a git too old to list them must not keep the repo from opening.
        // Only the list is read here. The changes of the other worktrees are a status run in each
        // of their folders, and that is read after the repo is shown (GetUpdatedWorktreesRepoAsync)
        // rather than before, so a cold index in a large worktree cannot delay showing this one.
        if (worktreesTask.Result is not IReadOnlyList<Git.Worktree> worktrees)
        {
            Log.Warn($"Failed to list worktrees, {worktreesTask.Result.Error}");
            worktrees = [];
        }

        // The reflog is extra as well: it only makes the branch inference surer, and a repo can have none
        if (reflogTask.Result is not IReadOnlyList<ReflogEntry> reflog)
        {
            Log.Warn($"Failed to read the reflog, {reflogTask.Result.Error}");
            reflog = [];
        }

        // Who the user is only picks out their own branches in the branch menu
        if (authorTask.Result is not string currentAuthor)
        {
            Log.Warn($"Failed to read user.name, {authorTask.Result.Error}");
            currentAuthor = "";
        }

        var isTruncated = log.Count == MaxCommitCount;
        if (log.Count == 0)
            return EmptyGitRepo(path, tags, status, metaData);
        WriteCommitGraphIfMissing(path, isTruncated);

        var remoteReflog = await GetRemoteReflogAsync(branches, path);

        // Combine all git info into one git repo info object. The config's collections are copied: it
        // is the live one, which a command changes (RecordStep) while the augmentation reads the git
        // repo off the UI thread.
        var config = repoConfig.Get(path);
        var gitRepo = new GitRepo(
            timeStamp,
            path,
            log,
            branches,
            tags,
            status,
            metaData,
            stashes,
            isTruncated,
            worktrees,
            reflog: reflog,
            integrationNames: config.IntegrationBranches.ToList(),
            recordedSteps: config.UndoSteps.ToDictionary(),
            remoteReflog: remoteReflog,
            currentAuthor: currentAuthor
        );
        Log.Info($"GitRepo {t} {gitRepo}");

        return gitRepo;
    }

    // A log longer than gmd reads is only cut short quickly with a commit-graph file: without one, 'git
    // log --date-order' reads every commit of the history before it shows the first, which made every
    // read of rust's 350,000 commits take 1.7 s and of Linux's 1.5 million 8 s, against 0.4 s and
    // 1.2 s with one. Git writes it in 'gc', which a fresh clone has not run yet, so the first read of
    // such a repo without one writes it, in the background, once a session. A write that fails, e.g.
    // in a read-only repo, is only logged.
    internal void WriteCommitGraphIfMissing(string path, bool isTruncated)
    {
        if (!isTruncated)
            return;
        lock (commitGraphPaths)
        {
            if (!commitGraphPaths.Add(path))
                return;
        }
        if (git.HasCommitGraph(path))
            return;

        Log.Info($"Writing the commit-graph file of '{path}', which has more commits than are read");
        git.WriteCommitGraphAsync(path).RunInBackground();
    }

    // The reflogs of the remote branches whose local branches have commits not on them, read after the
    // branches since it is them that say which: whether origin was force pushed, see RemoteRewrites.
    // Not only the diverged ones, since a force push that only dropped commits leaves the local branch
    // just ahead, with a push that puts them back. With every branch pushed nothing is read. It is
    // extra, like the reflog, and a failure only means nothing is told.
    async Task<IReadOnlyList<ReflogEntry>> GetRemoteReflogAsync(IReadOnlyList<Git.Branch> branches, string path)
    {
        var refs = branches
            .Where(b => !b.IsRemote && b.RemoteName != "" && b.AheadCount > 0)
            .Select(b => $"refs/remotes/{b.RemoteName}")
            .ToList();
        if (refs.Count == 0)
            return [];

        if (await git.GetRefReflogsAsync(refs, path) is not IReadOnlyList<ReflogEntry> reflog)
        {
            Log.Warn($"Failed to read the reflogs of {string.Join(", ", refs)}");
            return [];
        }
        return reflog;
    }

    // The number of uncommitted changes in each of the other worktrees, read in parallel. Only
    // the ones that can be read: a worktree whose folder is gone has no status, and neither has
    // a bare one. A status that fails is left out, which the UI shows as unknown. Read without
    // locks: someone else is working in those folders, and a plain status would hold their index
    // lock while it rewrote their index, long enough for a commit there to fail on it.
    async Task<IReadOnlyDictionary<string, int>> GetWorktreeChangesAsync(
        IReadOnlyList<Git.Worktree> worktrees,
        string path
    )
    {
        var others = worktrees
            .Where(w => !w.IsPrunable && !w.IsBare && !Files.IsSamePath(w.Path, path) && Directory.Exists(w.Path))
            .ToList();
        var tasks = others.Select(w => git.GetStatusWithoutLocksAsync(w.Path)).ToList();
        await Task.WhenAll(tasks);

        var changes = new Dictionary<string, int>();
        others.ForEach(
            (w, i) =>
            {
                if (tasks[i].Result is GitStatus status)
                    changes[w.Path] =
                        status.Modified + status.Added + status.Deleted + status.Conflicted + status.Renamed;
                else
                    Log.Warn($"Failed to get status of worktree {w.Path}, {tasks[i].Result.Error}");
            }
        );
        return changes;
    }

    // The folders of the other worktrees, which the file monitor must not take changes in as
    // changes of this one — a worktree nested inside the working folder would otherwise refresh
    // the status here for every file written there
    static IReadOnlyList<string> OtherWorktreeFolders(GitRepo gitRepo) =>
        gitRepo.Worktrees.Where(w => !Files.IsSamePath(w.Path, gitRepo.Path)).Select(w => w.Path).ToList();

    // The lines of work no branch, tag or stash has any more, which the reflogs still mention. Read
    // when asked, not with the repo: it is a log of the commits the repo does not have, which is
    // nothing to wait for on every refresh. The reflog is read again for it, since the repo does not
    // keep it.
    public async Task<Result<IReadOnlyList<LostWork>>> GetLostWorkAsync(Repo repo)
    {
        var reflogResult = await git.GetReflogAsync(repo.Path);
        if (reflogResult is not IReadOnlyList<ReflogEntry> reflog)
            return new Error("Failed to read the reflog", reflogResult.Error);

        var candidates = reflog
            .OrderBy(e => e.Index)
            .Select(e => e.Id)
            .Distinct()
            .Where(id => !repo.CommitById.ContainsKey(id))
            .Take(maxLostCandidates)
            .ToList();
        if (candidates.Count == 0)
            return new List<LostWork>();

        var stashes = repo.Stashes.Select(s => s.Id).ToList();
        var lostResult = await git.GetUnreachableCommitsAsync(candidates, stashes, repo.Path);
        if (lostResult is not IReadOnlyList<Git.Commit> lost)
            return new Error("Failed to read the commits no branch has", lostResult.Error);

        var localNames = repo.AllBranches.Where(b => b.IsGitBranch && !b.IsRemote).Select(b => b.Name).ToHashSet();
        var reachable = repo.AllCommits.Select(c => (c.Author, c.AuthorTime));
        return LostWorkFinder.Find(reflog, lost, reachable, localNames).ToList();
    }

    public Task<Result> PushMetaDataAsync(string wd) => metaDataService.PushMetaDataAsync(wd);

    async Task<Result<GitStatus>> GetGitStatusAsync(string path)
    {
        fileMonitor.ReadingStatus();
        return await git.GetStatusAsync(path);
    }

    // The git repo augmented, and its working folder watched from now on
    async Task<Result<Repo>> GetAugmentedRepoAsync(GitRepo gitRepo)
    {
        fileMonitor.Monitor(gitRepo.Path, OtherWorktreeFolders(gitRepo));

        Timing t = Timing.Start();
        WorkRepo augRepo = await augmenter.GetAugRepoAsync(gitRepo);
        if (augRepo.WitnessedToKeep.Count > 0)
        { // Kept after the repo is shown, since nothing waits for it
            KeepWitnessedAsync(gitRepo.Path, augRepo.WitnessedToKeep).RunInBackground();
        }

        var repo = converter.ToRepo(augRepo);
        repo = Uncommitted.Adjust(repo);
        Log.Info($"Augmented {t} {repo}");
        return repo;
    }

    // What the reflog witnessed and decided is kept in the metadata, since the reflog expires. No
    // write the user asked for, so the file monitor is not paused, nor need it be: it takes no
    // metadata write for a change of the repo.
    Task<Result> KeepWitnessedAsync(string path, IReadOnlyList<WitnessedBranch> witnessed) =>
        metaDataService.AddWitnessedAsync(path, witnessed);

    Repo GetUpdatedAugmentedRepoStatus(Repo repo, GitStatus gitStatus)
    {
        Timing t = Timing.Start();
        var status = GitConverter.ToStatus(gitStatus);
        repo = repo with { Status = status, ViewBranches = new List<Branch>(), ViewCommits = new List<Commit>() };
        repo = Uncommitted.Adjust(repo);
        Log.Info($"Augmented {t} {repo}");
        return repo;
    }

    Result<GitRepo> EmptyGitRepo(string path, IReadOnlyList<Git.Tag> tags, GitStatus status, MetaData metaData)
    {
        Timing t = Timing.Start();
        var id = Repo.EmptyRepoCommitId;
        var msg = "<... empty repo ...>";
        var branchName = "main";
        var commit = new Git.Commit(id, id.Sid(), [], msg, msg, "", DateTime.UtcNow, DateTime.UtcNow);
        var branch = new Git.Branch(branchName, id, true, false, "", false, 0, 0);
        var commits = new List<Git.Commit>() { commit };
        var branches = new List<Git.Branch>() { branch };
        var stashes = new List<Git.Stash>();

        var gitRepo = new GitRepo(DateTime.UtcNow, path, commits, branches, tags, status, metaData, stashes, false);
        Log.Info($"GitRepo {t} {gitRepo}");
        return gitRepo;
    }
}
