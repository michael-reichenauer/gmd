using gmd.Common;
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

    // The commits the reflogs mention that are looked up as maybe lost, the newest: each ref's
    // reflog is latest first, and a reflog of years can name many thousands
    const int maxLostCandidates = 1200;

    readonly IGit git;
    readonly IAugmenter augmenter;
    readonly IWorkRepoConverter converter;
    readonly IFileMonitor fileMonitor;
    readonly IMetaDataService metaDataService;
    readonly IBranchWriteService branchWriteService;
    readonly IRepoConfig repoConfig;

    internal AugmentedService(
        IGit git,
        IAugmenter augmenter,
        IWorkRepoConverter converter,
        IFileMonitor fileMonitor,
        IMetaDataService metaDataService,
        IBranchWriteService branchWriteService,
        IRepoConfig repoConfig
    )
    {
        this.repoConfig = repoConfig;
        this.git = git;
        this.augmenter = augmenter;
        this.converter = converter;
        this.fileMonitor = fileMonitor;
        this.metaDataService = metaDataService;
        this.branchWriteService = branchWriteService;
        fileMonitor.FileChanged += e => StatusChange?.Invoke(e);
        fileMonitor.RepoChanged += e => RepoChange?.Invoke(e);
    }

    public event Action<ChangeEvent>? RepoChange;
    public event Action<ChangeEvent>? StatusChange;

    // GetRepoAsync returns an augmented repo based on new git info like branches, commits, ...
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

    // GetRepoAsync returns the updated augmented repo with git status .
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

    public async Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.CommitAllChangesAsync(message, isAmend, wd);
        }
    }

    public async Task<Result> CommitFilesAsync(string message, bool isAmend, IReadOnlyList<string> paths, string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.CommitFilesAsync(message, isAmend, paths, wd);
        }
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
                return new Worktree(
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
                    count
                );
            })
            .ToList();

        return repo with
        {
            Worktrees = worktrees,
        };
    }

    // The worktree writes pause the file monitor like the other writes do: adding one writes into
    // the common dir's 'worktrees/', which is watched, and that reload is made by the caller
    public async Task<Result> AddWorktreeAsync(
        string path,
        string branchName,
        bool isNewBranch,
        string startPoint,
        string wd
    )
    {
        using (fileMonitor.Pause())
        {
            return await git.AddWorktreeAsync(path, branchName, isNewBranch, startPoint, wd);
        }
    }

    public async Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.RemoveWorktreeAsync(path, isForce, wd);
        }
    }

    public async Task<Result> PruneWorktreesAsync(string wd)
    {
        using (fileMonitor.Pause())
        {
            return await git.PruneWorktreesAsync(wd);
        }
    }

    public async Task<Result> FetchAsync(string path)
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

    public Task<Result> FetchMetaDataAsync(string path) => metaDataService.FetchMetaDataAsync(path);

    // The branch write operations, which need the augmented repo to work out what git to run
    public Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd) =>
        branchWriteService.CreateBranchAsync(repo, newBranchName, isCheckout, wd);

    public Task<Result> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout,
        string wd
    ) => branchWriteService.CreateBranchFromBranchAsync(repo, newBranchName, sourceBranch, isCheckout, wd);

    public Task<Result> CreateBranchFromCommitAsync(
        Repo repo,
        string newBranchName,
        string sha,
        bool isCheckout,
        string wd
    ) => branchWriteService.CreateBranchFromCommitAsync(repo, newBranchName, sha, isCheckout, wd);

    public Task<Result> RenameBranchAsync(string oldName, string newName, string wd) =>
        branchWriteService.RenameBranchAsync(oldName, newName, wd);

    public Task<Result> SwitchToAsync(Repo repo, string branchName) =>
        branchWriteService.SwitchToAsync(repo, branchName);

    public Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name) =>
        branchWriteService.MergeBranchAsync(repo, name);

    public Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName) =>
        branchWriteService.MergeToBranchAsync(repo, targetName);

    public Task<Result> RebaseBranchAsync(Repo repo, string name) => branchWriteService.RebaseBranchAsync(repo, name);

    // GetGitRepoAsync returns a fresh git repo info object with commits, branches, ...
    async Task<Result<GitRepo>> GetGitRepoAsync(string path)
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
        var worktreesTask = git.GetWorktreesAsync(path);
        var reflogTask = git.GetReflogAsync(path);
        await Task.WhenAll(
            logTask,
            branchesTask,
            tagsTask,
            statusTask,
            metaDataTask,
            stashesTask,
            worktreesTask,
            reflogTask
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

        var isTruncated = log.Count == maxCommitCount;
        if (log.Count == 0)
            return EmptyGitRepo(path, tags, status, metaData);

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
            remoteReflog: remoteReflog
        );
        Log.Info($"GitRepo {t} {gitRepo}");

        return gitRepo;
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

    public async Task<Result> SquashCommits(Repo repo, string id1, string id2, string message)
    {
        using (fileMonitor.Pause())
        {
            var c1 = repo.CommitById[id1];
            var c2 = repo.CommitById[id2];
            if (!c2.ParentIds.Any())
                return new Error("Last commit does not have a parent");
            if (c1.BranchName != c2.BranchName)
                return new Error("Commits are not on the same branch");
            var branch = repo.BranchByName[c1.BranchName];

            // Both flags: IsLocalCurrent is only ever set on a *remote* branch whose local branch
            // is current, so asking for it alone refused every commit that had not been pushed yet.
            // The same check is made before the dialog is shown, in CommitCommands.SquashCommits.
            if (!branch.IsCurrent && !branch.IsLocalCurrent)
                return new Error("Commits not on current branch");

            // The local branch, which the squash moves, and where it was before, for Undo, which the
            // squash is recorded for below. A squash waits for a clean tree, so the local branch's tip
            // is git's own, not the uncommitted row.
            var local = repo.AllBranches.FirstOrDefault(b => b.IsCurrent && !b.IsRemote);

            // The commits on top of the squashed ones, to be picked back onto the squash, walked from
            // the local branch's tip. Not from the tip of the branch the commits are on: commits
            // already pushed are on the remote branch, whose tip is below the local one's when there
            // are commits on top not pushed yet, and those were squashed in with the others. A commit
            // that is not on the local branch's line, e.g. one only origin has when the two have
            // diverged, is refused before anything is changed, rather than the branch reset onto it.
            var preCommits = new List<Commit>();
            var c = repo.CommitById[local?.TipId ?? branch.TipId];
            while (c.Id != c1.Id)
            {
                preCommits.Add(c);
                if (c.ParentIds.Count == 0 || !repo.CommitById.TryGetValue(c.ParentIds[0], out var parent))
                    return new Error(
                        $"Only commits on the current branch can be squashed, and {c1.Sid} is not on "
                            + $"'{local?.NiceNameUnique ?? branch.NiceNameUnique}'"
                    );
                c = parent;
            }

            // Create a backup branch (in case of errors)
            var tmpName = $"squash-backup-{Guid.NewGuid().ToString()[..6]}";
            if (await git.CreateBranchAsync(tmpName, false, repo.Path) is Error backupError)
                return new Error("Failed to create backup branch", backupError);

            // Remove all prefix commits on current branch until the first commit to squash
            if (preCommits.Any())
            {
                if (await git.ResetHardUntilCommitAsync(id1, repo.Path) is Error resetError)
                    return new Error("Failed to prepare for squash", resetError);
            }

            // Squash commits and commit
            if (await git.UncommitUntilCommitAsync(c2.ParentIds[0], repo.Path) is Error uncommitError)
                return new Error("Failed to squash commits", uncommitError);
            if (await git.CommitAllChangesAsync(message, false, repo.Path) is Error commitError)
                return new Error("Failed to commit squashed commits", commitError);

            // Cherry pick prefix commits back to current branch
            foreach (var commit in preCommits.AsEnumerable().Reverse())
            {
                if (await git.CherryPickAsync(commit.Id, repo.Path) is Error pickError)
                    return new Error($"Failed to cherry pick {commit.Sid}", pickError);
                if (await git.CommitAllChangesAsync(commit.Message, false, repo.Path) is Error pickCommitError)
                    return new Error($"Failed to commit cherry pick {commit.Sid}", pickCommitError);
            }

            // The squash is several moves of the branch, one entry each in its reflog: the reset of
            // the newer commits, if any, the reset of the squashed ones, their commit and the commit of
            // each newer one picked back. Recorded as one change, so that Undo takes it back whole.
            if (local != null)
            {
                RecordStep(
                    repo,
                    local.Name,
                    new RecordedStep
                    {
                        Kind = nameof(StepKind.Squash),
                        Name = message.Split('\n')[0].Trim(),
                        BeforeId = local.TipId,
                        Moves = (preCommits.Any() ? 1 : 0) + 2 + preCommits.Count,
                    }
                );
            }

            // Remove temp backup branch
            if (await git.DeleteLocalBranchAsync(tmpName, true, repo.Path) is Error deleteError)
                return new Error("Failed to delete backup branch", deleteError);
        }

        return Result.Ok;
    }

    // Gives an older commit of the current branch, one not pushed yet, a new message and the changes
    // in these files, none for a new message alone. It is git's own way of doing it: an 'amend!'
    // commit carrying both is made on top, and a rebase with --autosquash folds it into the commit
    // and replays the newer ones after it. The changes left out stay as they are, put aside and back
    // by the rebase. A later commit that conflicts with the changes stops the rebase there, to be
    // continued or aborted as any rebase; an abort leaves the 'amend!' commit on top, which Undo
    // takes back to uncommitted changes.
    //
    // What the menu checks (CommitRewrite) is checked here against git too, since the commits are
    // rewritten: a commit on origin already, or one another branch or a tag is on, is left as it is.
    public async Task<Result> AmendOlderCommitAsync(Repo repo, string id, string message, IReadOnlyList<string> paths)
    {
        var commit = repo.CommitById[id];
        var checkResult = await CheckRewritableAsync(repo, commit);
        if (checkResult is not Branch local)
            return new Error($"Cannot amend {commit.Sid}", checkResult.Error);

        using (fileMonitor.Pause())
        {
            // git matches the 'amend!' subject with the first commit of the rebase of that subject,
            // which is this one, since the rebase starts at it
            var amendMessage = $"amend! {commit.Subject}\n\n{message}";
            if (await git.CommitFilesAsync(amendMessage, false, paths, repo.Path) is Error commitError)
                return new Error($"Failed to commit the changes to amend {commit.Sid} with", commitError);

            var baseId = commit.ParentIds.Count == 0 ? "" : $"{id}^";
            if (await git.AutosquashAsync(baseId, repo.Path) is Error rebaseError)
                return new Error($"Failed to amend {commit.Sid}", rebaseError);

            // Two moves of the branch, the 'amend!' commit and the rebase, recorded as one change
            RecordStep(
                repo,
                local.Name,
                new RecordedStep
                {
                    Kind = nameof(StepKind.Amend),
                    Name = message.Split('\n')[0].Trim(),
                    BeforeId = repo.CurrentCommit().Id,
                    Moves = 2,
                }
            );
        }
        return Result.Ok;
    }

    // Takes an older commit of the current branch, one not pushed yet, out of the branch: the newer
    // commits are replayed onto its parent. One that conflicts without it stops the rebase there.
    public async Task<Result> DropCommitAsync(Repo repo, string id)
    {
        var commit = repo.CommitById[id];
        var checkResult = await CheckRewritableAsync(repo, commit);
        if (checkResult is not Branch local)
            return new Error($"Cannot drop {commit.Sid}", checkResult.Error);
        if (commit.ParentIds.Count == 0)
            return new Error("The first commit cannot be dropped");

        using (fileMonitor.Pause())
        {
            if (await git.DropCommitAsync(id, repo.Path) is Error dropError)
                return new Error($"Failed to drop {commit.Sid}", dropError);

            RecordStep(
                repo,
                local.Name,
                new RecordedStep
                {
                    Kind = nameof(StepKind.Drop),
                    Name = commit.Subject.Trim(),
                    BeforeId = repo.CurrentCommit().Id,
                    Moves = 1,
                }
            );
        }
        return Result.Ok;
    }

    // The current local branch, when only it has the commit: no remote branch, i.e. it is not pushed,
    // and no other branch or tag, which would be left on the old commits. Asked of git rather than
    // taken from the repo read, since the commits are rewritten.
    async Task<Result<Branch>> CheckRewritableAsync(Repo repo, Commit commit)
    {
        var local = repo.AllBranches.FirstOrDefault(b => b.IsCurrent && !b.IsRemote && !b.IsDetached);
        if (local == null)
            return new Error("Not on a branch");

        var refsResult = await git.GetRefsContainingAsync(commit.Id, repo.Path);
        if (refsResult is not IReadOnlyList<string> refs)
            return refsResult.Error;
        if (!refs.Contains($"refs/heads/{local.Name}"))
            return new Error($"{commit.Sid} is not on '{local.NiceNameUnique}'");
        if (refs.FirstOrDefault(r => r.StartsWith("refs/remotes/")) is string remote)
            return new Error($"{commit.Sid} is on '{remote["refs/remotes/".Length..]}' already, i.e. pushed");
        if (refs.FirstOrDefault(r => r.StartsWith("refs/tags/")) is string tag)
            return new Error($"The tag '{tag["refs/tags/".Length..]}' is on {commit.Sid} or a commit after it");
        if (refs.FirstOrDefault(r => r != $"refs/heads/{local.Name}") is string other)
            return new Error($"'{other["refs/heads/".Length..]}' is on {commit.Sid} or a commit after it");
        return local;
    }

    // Takes back the last change of a branch, or redoes the change an undo took back. The current
    // branch is reset in the way that loses nothing uncommitted, see UndoStep.Mode, and any other is
    // just moved, unless it is checked out in another worktree, whose files would no longer match.
    // Either only from where the step found it, so a commit made since is not taken back with it.
    // The reset is an entry in the branch's reflog like any other, so the change it took back is
    // recorded with it, and Undo again names that change and redoes it, rather than undoing "a reset".
    public async Task<Result> UndoStepAsync(Repo repo, UndoStep step)
    {
        if (!repo.BranchByName.TryGetValue(step.BranchName, out var branch) || branch.IsRemote)
            return new Error($"There is no local branch '{step.BranchName}'");
        if (branch.WorktreePath != "")
            return new Error($"'{step.BranchName}' is checked out in another worktree, so it is undone there");

        using (fileMonitor.Pause())
        {
            if (branch.IsCurrent)
            {
                var isKeep = step.Mode == UndoMode.Keep || (step.Mode == UndoMode.KeepWhenClean && repo.Status.IsOk);
                if (
                    await git.ResetBranchAsync(step.BranchName, step.TargetId, step.TipId, isKeep, repo.Path) is Error e
                )
                    return e;
            }
            else if (await MoveBranchAsync(step, repo.Path) is Error moveError)
            {
                return moveError;
            }

            RecordStep(
                repo,
                step.BranchName,
                new RecordedStep
                {
                    Kind = step.Kind.ToString(),
                    Name = step.Name,
                    BeforeId = step.TipId,
                    AfterId = step.TargetId,
                    Moves = 1,
                    IsRedo = !step.IsRedo,
                }
            );
            return Result.Ok;
        }
    }

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

    // Deletes a branch on origin, here or both, origin's first, and records each side once it is gone,
    // so that Restore Deleted Branch can bring it back: git deletes a branch's reflog with the branch,
    // so nothing else would say where it was. A name is "" for a side not to delete. The branches are
    // looked up in the repo given, which can be newer than the one they were picked in, since a
    // refresh runs while a dialog is shown, so one can be gone.
    public async Task<Result> DeleteBranchAsync(Repo repo, string localName, string remoteName, bool isForce)
    {
        Branch? GitBranch(string name) => repo.BranchByName.TryGetValue(name, out var b) && b.IsGitBranch ? b : null;
        string SubjectOf(string id) => repo.CommitById.TryGetValue(id, out var c) ? c.Subject : "";

        var local = localName != "" ? GitBranch(localName) : null;
        var remote = remoteName != "" ? GitBranch(remoteName) : null;
        var missingName =
            localName != "" && local == null ? localName
            : remoteName != "" && remote == null ? remoteName
            : "";
        if (missingName != "")
            return new Error($"There is no branch '{missingName}' any more, so nothing was deleted");

        using (fileMonitor.Pause())
        {
            if (remote != null)
            {
                if (await git.DeleteRemoteBranchAsync(remote.Name, repo.Path) is Error e)
                    return new Error($"Failed to delete remote branch {remote.Name}", e);
                RecordDelete(
                    repo,
                    new RecordedDelete
                    {
                        Name = remote.LocalName,
                        RemoteName = remote.Name,
                        RemoteTipId = remote.TipId,
                        RemoteSubject = SubjectOf(remote.TipId),
                        Time = DateTime.Now,
                    }
                );
            }

            // Both sides deleted are one record, which takes the place of origin's recorded above
            if (local != null)
            {
                if (await git.DeleteLocalBranchAsync(local.Name, isForce, repo.Path) is Error e)
                    return new Error($"Failed to delete local branch {local.Name}", e);
                RecordDelete(
                    repo,
                    new RecordedDelete
                    {
                        Name = local.Name,
                        TipId = local.TipId,
                        Subject = SubjectOf(local.TipId),
                        RemoteName = remote?.Name ?? local.RemoteName,
                        RemoteTipId = remote?.TipId ?? "",
                        RemoteSubject = remote != null ? SubjectOf(remote.TipId) : "",
                        Time = DateTime.Now,
                    }
                );
            }
        }

        return Result.Ok;
    }

    // The branches gmd deleted that can be restored, newest first, see DeletedBranchRecords.Restorable.
    // Read when asked, since whether git still has their commits is a question for git. The sides
    // that cannot be restored are forgotten.
    public async Task<Result<IReadOnlyList<DeletedBranch>>> GetDeletedBranchesAsync(Repo repo)
    {
        var records = repoConfig.Get(repo.Path).DeletedBranches.ToList();
        if (records.Count == 0)
            return new List<DeletedBranch>();

        var existingResult = await git.GetExistingCommitIdsAsync(DeletedBranchRecords.TipIds(records), repo.Path);
        if (existingResult is not IReadOnlySet<string> existing)
            return new Error("Failed to look up the commits of the deleted branches", existingResult.Error);

        var restorable = records
            .Select(r => (Record: r, Deleted: DeletedBranchRecords.Restorable(r, repo, existing)))
            .ToList();
        var unrestorable = restorable
            .Select(r => DeletedBranchRecords.Unrestorable(r.Record, r.Deleted))
            .OfType<RecordedDelete>()
            .ToList();
        if (unrestorable.Count > 0)
        {
            repoConfig.Set(
                repo.Path,
                c => unrestorable.ForEach(sides => DeletedBranchRecords.Forget(c.DeletedBranches, sides))
            );
        }

        return restorable.Select(r => r.Deleted).OfType<DeletedBranch>().ToList();
    }

    // Brings a deleted branch back, the sides asked for: origin's pushed back at its tip, but only
    // while origin has no branch of the name, so one someone pushed since is not overwritten, and the
    // local branch at its tip, tracking the remote branch it tracked. Origin's first, since that is
    // what can be refused, and a refusal then leaves nothing half done. Each side is forgotten in the
    // records once it is back, so a later delete of the name is not joined with it.
    public async Task<Result> RestoreBranchAsync(Repo repo, DeletedBranch deleted, bool isLocal, bool isRemote)
    {
        isLocal = isLocal && deleted.IsLocal;
        isRemote = isRemote && deleted.IsRemote;

        using (fileMonitor.Pause())
        {
            if (isRemote)
            {
                if (
                    await git.PushRestoreAsync(deleted.RemoteName, deleted.RemoteTipId, "", repo.Path)
                    is Error pushError
                )
                {
                    return pushError is CmdError cmdError && cmdError.Output.Contains("stale info")
                        ? new Error(
                            $"Origin has a branch '{deleted.RemoteName}' again, which was left as it is, so nothing was restored",
                            pushError
                        )
                        : new Error($"Failed to push '{deleted.RemoteName}' back, so nothing was restored", pushError);
                }
                ForgetRestored(repo, deleted, false, true);
            }

            if (isLocal)
            {
                if (
                    await git.CreateBranchFromCommitAsync(deleted.Name, deleted.TipId, false, repo.Path)
                    is Error createError
                )
                {
                    return new Error(
                        isRemote
                            ? $"Restored '{deleted.RemoteName}', but failed to create '{deleted.Name}' again"
                            : $"Failed to create '{deleted.Name}' again",
                        createError
                    );
                }
                ForgetRestored(repo, deleted, true, false);

                // Git deleted what the branch tracked with it, which is written back whether the remote
                // branch is there or not: deleted too and not restored yet, the branch tracks it once it is
                if (
                    deleted.RemoteName != ""
                    && await git.SetUpstreamAsync(deleted.Name, deleted.RemoteName, repo.Path) is Error upstreamError
                )
                {
                    var where = isRemote ? " here and on origin" : "";
                    return new Error(
                        $"Restored '{deleted.Name}'{where}, but failed to make it track '{deleted.RemoteName}'",
                        upstreamError
                    );
                }
            }
        }

        return Result.Ok;
    }

    // Records a branch gmd deleted, see RepoConfig.DeletedBranches
    void RecordDelete(Repo repo, RecordedDelete deleted) =>
        repoConfig.Set(repo.Path, c => DeletedBranchRecords.Add(c.DeletedBranches, deleted));

    void ForgetRestored(Repo repo, DeletedBranch deleted, bool isLocal, bool isRemote) =>
        repoConfig.Set(
            repo.Path,
            c =>
                DeletedBranchRecords.Forget(
                    c.DeletedBranches,
                    DeletedBranchRecords.Restored(deleted, isLocal, isRemote)
                )
        );

    // Pulls a branch whose remote branch a force push rewrote: its own commits are moved onto the new
    // version, and the old version's commits are left out (Recover Lost Commits finds them), rather
    // than merged in, which would put every commit in twice. The current branch is rebased, and the
    // pull is recorded, so that Undo names it a pull; a branch that is not checked out can only take
    // the new version, i.e. when it has no commits of its own, which moves it only if it is still
    // where it was read.
    public async Task<Result> PullRewrittenAsync(Repo repo, RemoteRewrite rewrite)
    {
        if (!repo.BranchByName.TryGetValue(rewrite.BranchName, out var branch) || branch.IsRemote)
            return new Error($"There is no local branch '{rewrite.BranchName}'");
        if (branch.WorktreePath != "")
            return new Error($"'{rewrite.BranchName}' is checked out in another worktree, so it is pulled there");

        using (fileMonitor.Pause())
        {
            if (branch.IsCurrent)
            {
                if (await git.RebaseOntoRemoteAsync(rewrite.RemoteName, rewrite.ForkPointId, repo.Path) is Error e)
                    return e;

                RecordStep(
                    repo,
                    rewrite.BranchName,
                    new RecordedStep
                    {
                        Kind = nameof(StepKind.Pull),
                        BeforeId = rewrite.LocalTipId,
                        Moves = 1,
                    }
                );
                return Result.Ok;
            }

            if (rewrite.OwnCount > 0)
                return new Error($"'{rewrite.BranchName}' has commits of its own, so it is pulled when checked out");

            var message = $"pull: rewritten {rewrite.RemoteName}";
            var moved = await git.MoveBranchAsync(
                rewrite.BranchName,
                rewrite.NewTipId,
                rewrite.LocalTipId,
                message,
                repo.Path
            );
            return moved is CmdError moveError && moveError.ErrorOutput.Contains("but expected")
                ? new Error(
                    $"'{rewrite.BranchName}' has moved since it was read, so it was left where it is",
                    moveError
                )
                : moved;
        }
    }

    // A branch that is not checked out has no files to take back, so it is just moved, and only if
    // it is still where the step found it, which git checks as it moves it
    async Task<Result> MoveBranchAsync(UndoStep step, string wd)
    {
        var message = $"undo: moving to {step.TargetId}";
        var result = await git.MoveBranchAsync(step.BranchName, step.TargetId, step.TipId, message, wd);
        return result is CmdError e && e.ErrorOutput.Contains("but expected")
            ? new Error($"'{step.BranchName}' has moved since it was read, so it was left where it is", e)
            : result;
    }

    // Records a change gmd made to a branch as its latest, see RepoConfig.UndoSteps, and forgets the
    // records of branches that are gone
    void RecordStep(Repo repo, string branchName, RecordedStep step) =>
        repoConfig.Set(
            repo.Path,
            c =>
            {
                foreach (var name in c.UndoSteps.Keys.Where(n => !repo.BranchByName.ContainsKey(n)).ToList())
                {
                    c.UndoSteps.Remove(name);
                }
                c.UndoSteps[branchName] = step;
            }
        );

    public async Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setNiceName)
    {
        Log.Info($"Set {commitId.Sid()} to {setNiceName} ...");

        using (fileMonitor.Pause())
        {
            return await metaDataService.UpdateMetaDataAsync(
                repo.Path,
                m => m.SetCommitBranch(commitId.Sid(), setNiceName)
            );
        }
    }

    public async Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName)
    {
        var branch = repo.BranchByName[branchName];
        var ambiguousTip = branch.AmbiguousTipId;
        Log.Info($"Resolve {ambiguousTip.Sid()} of {branchName} to {setHumanName} ...");

        using (fileMonitor.Pause())
        {
            return await metaDataService.UpdateMetaDataAsync(
                repo.Path,
                m => m.SetCommitBranch(ambiguousTip.Sid(), setHumanName)
            );
        }
    }

    public async Task<Result> UnresolveAmbiguityAsync(Repo repo, string commitId)
    {
        using (fileMonitor.Pause())
        {
            return await metaDataService.UpdateMetaDataAsync(repo.Path, m => m.RemoveCommitBranch(commitId.Sid()));
        }
    }

    public Task<Result> PushMetaDataAsync(string wd) => metaDataService.PushMetaDataAsync(wd);

    public async Task<Result> AddTagAsync(string name, string commitId, bool isPush, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (await git.AddTagAsync(name, commitId, wd) is Error e)
                return e;
            if (!isPush)
                return Result.Ok;
            return await git.PushTagAsync(name, wd);
        }
    }

    public async Task<Result> AddAnnotatedTagAsync(string name, string message, string commitId, bool isPush, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (await git.AddAnnotatedTagAsync(name, message, commitId, wd) is Error e)
                return e;
            if (!isPush)
                return Result.Ok;
            return await git.PushTagAsync(name, wd);
        }
    }

    public async Task<Result> RemoveTagAsync(string name, bool isOnOrigin, string wd)
    {
        using (fileMonitor.Pause())
        {
            if (await git.RemoveTagAsync(name, wd) is Error e)
                return e;
            if (!isOnOrigin)
                return Result.Ok;
            return await git.DeleteRemoteTagAsync(name, wd);
        }
    }

    // GetGitStatusAsync returns a fresh git status
    async Task<Result<GitStatus>> GetGitStatusAsync(string path)
    {
        fileMonitor.SetReadStatusTime(DateTime.UtcNow);
        return await git.GetStatusAsync(path);
    }

    // GetAugmentedRepoAsync returns an augmented git repo, and monitors working folder changes
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

    // What the reflog witnessed and decided is kept in the metadata, since the reflog expires. The
    // file monitor is not paused for it, since it takes no metadata write for a change of the repo.
    Task<Result> KeepWitnessedAsync(string path, IReadOnlyList<WitnessedBranch> witnessed) =>
        metaDataService.AddWitnessedAsync(path, witnessed);

    // GetUpdatedAugmentedRepoStatus an updated augmented repo with new status
    Repo GetUpdatedAugmentedRepoStatus(Repo repo, GitStatus gitStatus)
    {
        Timing t = Timing.Start();
        var status = converter.ToStatus(gitStatus);
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
