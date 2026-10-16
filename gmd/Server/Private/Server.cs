using gmd.Git;
using gmd.Server.Private.Augmented;

namespace gmd.Server.Private;

[SingleInstance]
class Server : IServer
{
    readonly IGit git;
    readonly IAugmentedService augmentedService;
    readonly IBranchWriteService branchWriteService;
    readonly ICommitRewriteService commitRewriteService;
    readonly IUndoService undoService;
    readonly IDeletedBranchService deletedBranchService;
    readonly IViewRepoConverter converter;
    readonly IViewRepoCreater viewRepoCreater;
    readonly IFileMonitor fileMonitor;
    readonly DiscardedChanges discarded;

    public Server(
        IGit git,
        IAugmentedService augmentedService,
        IBranchWriteService branchWriteService,
        ICommitRewriteService commitRewriteService,
        IUndoService undoService,
        IDeletedBranchService deletedBranchService,
        IViewRepoConverter converter,
        IViewRepoCreater viewRepoCreater,
        IFileMonitor fileMonitor,
        DiscardedChanges discarded
    )
    {
        this.discarded = discarded;
        this.git = git;
        this.augmentedService = augmentedService;
        this.branchWriteService = branchWriteService;
        this.commitRewriteService = commitRewriteService;
        this.undoService = undoService;
        this.deletedBranchService = deletedBranchService;
        this.converter = converter;
        this.viewRepoCreater = viewRepoCreater;
        this.fileMonitor = fileMonitor;
        augmentedService.RepoChange += e => RepoChange?.Invoke(e);
        augmentedService.StatusChange += e => StatusChange?.Invoke(e);
    }

    public event Action<ChangeEvent>? RepoChange;
    public event Action<ChangeEvent>? StatusChange;

    // Every write the user asks for runs with the file monitor paused, here and nowhere else, so that
    // what it writes is not told of as a change while it runs: the command refreshes when it is done,
    // and that read takes the changes told of meanwhile as seen (FileMonitor.ReadingRepo), while one
    // made by something else during the pause is told of after it. Some writes were paused and some
    // not, by whether they happened to pass through the augmented service, and a pull, a rebase or a
    // cherry-pick refreshed the log while it ran.
    //
    // The fetch is not paused: it runs in the background, so a pause would hold back the user's own
    // changes while it runs, and what it fetched is told of like any other change.
    async Task<Result> Paused(Func<Task<Result>> write)
    {
        using (fileMonitor.Pause())
        {
            return await write();
        }
    }

    async Task<Result<T>> Paused<T>(Func<Task<Result<T>>> write)
        where T : notnull
    {
        using (fileMonitor.Pause())
        {
            return await write();
        }
    }

    public async Task<Result<Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        var repoResult = await augmentedService.GetRepoAsync(path);
        if (repoResult is not Repo augmentedRepo)
            return repoResult.Error;

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, showBranches);
    }

    public async Task<Result<Repo>> GetUpdateStatusRepoAsync(Repo repo)
    {
        var branches = repo.ViewBranches.Select(b => b.Name).ToArray();

        var updated = await augmentedService.UpdateRepoStatusAsync(repo);
        if (updated is not Repo augmentedRepo)
            return updated.Error;
        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, branches);
    }

    public async Task<Result<Repo>> GetFilteredRepoAsync(Repo repo, string filter, int maxCount)
    {
        var terms = SearchTerms.Parse(filter);
        if (!terms.IsAskingGit)
            return viewRepoCreater.GetFilteredViewRepoAsync(repo, filter, maxCount);

        var idsResult = await GetIdsAskingGitAsync(repo, terms, maxCount);
        if (idsResult is not IReadOnlySet<string> ids)
            return new Error("Failed to search the changed files and the changes", idsResult.Error);

        return viewRepoCreater.GetFilteredViewRepoAsync(repo, filter, maxCount, ids);
    }

    // The commits that changed files matching every one of the paths, and whose changes added or
    // removed every one of the texts, which git is asked for. The last answer is kept, since the
    // search asks again with every key typed after them, and git takes a moment in a large repo.
    async Task<Result<IReadOnlySet<string>>> GetIdsAskingGitAsync(Repo repo, SearchTerms terms, int maxCount)
    {
        var asked = string.Join(
            '\n',
            terms.Files.Select(f => $"file:{f}").Concat(terms.Changes.Select(c => $"change:{c}"))
        );
        var key = $"{repo.Path}\n{repo.RepoTimeStamp.Ticks}\n{maxCount}\n{asked}";
        if (lastFileSearch is var (lastKey, lastIds) && lastKey == key)
            return new Result<IReadOnlySet<string>>(lastIds);

        // All at once, since each is a walk of the whole history
        var results = await Task.WhenAll(
            terms
                .Files.Select(file => git.GetIdsChangingFilesAsync(file, maxCount, repo.Path))
                .Concat(terms.Changes.Select(text => git.GetIdsChangingTextAsync(text, maxCount, repo.Path)))
        );
        HashSet<string>? ids = null;
        foreach (var result in results)
        {
            if (result is not IReadOnlyList<string> fileIds)
                return result.Error;
            ids = ids == null ? fileIds.ToHashSet() : ids.Intersect(fileIds).ToHashSet();
        }

        IReadOnlySet<string> found = ids ?? [];
        lastFileSearch = (key, found);
        // Built rather than converted: C# has no user-defined conversion from an interface type
        return new Result<IReadOnlySet<string>>(found);
    }

    (string Key, IReadOnlySet<string> Ids)? lastFileSearch;

    public IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId, bool isAll)
    {
        if (commitId == Repo.UncommittedId)
            return new List<Branch>();

        // The branches of the commits next to it but its own, and its own when it is its tip, all of
        // them or only those not shown
        bool FilterOnShown(Commit cc) => isAll || !cc.IsInView;
        var commit = repo.CommitById[commitId];
        var branch = repo.BranchByName[commit.BranchName];

        var branchNames = commit
            .AllChildIds.Concat(commit.ParentIds) // All children and parents commit ids
            .Select(id => repo.CommitById[id]) // As commits
            .Where(cc => cc.BranchPrimaryName != commit.BranchPrimaryName) // Skip same branch
            .Concat(commit.Id == branch.TipId ? [commit] : []) // Add commit branch if tip
            .Where(FilterOnShown) // Exclude shown branches (or not)
            .Select(cc => cc.BranchPrimaryName);

        // And the branches starting here with no commits of their own, which no child commit tells of
        var startingNames = repo.BranchesStartingAt(commitId)
            .Where(b => isAll || !b.IsInView)
            .Select(b => b.PrimaryName);

        return branchNames.Concat(startingNames).Distinct().Select(n => repo.BranchByName[n]).ToList();
    }

    public IReadOnlyList<string> GetPossibleBranchNames(Repo repo, string commitId, int maxCount)
    {
        if (commitId == Repo.UncommittedId)
            return new List<string>();

        var specifiedCommit = repo.CommitById[commitId];

        var branches = new Queue<string>();
        var branchesSeen = new HashSet<string>();
        var commitQueue = new Queue<Commit>();
        var commitSeen = new HashSet<Commit>();

        commitQueue.Enqueue(specifiedCommit);
        commitSeen.Add(specifiedCommit);

        while (commitQueue.Any() && branches.Count < maxCount)
        {
            var commit = commitQueue.Dequeue();
            var branch = repo.BranchByName[commit.BranchName];

            if (!branchesSeen.Contains(branch.NiceName))
            {
                branches.Enqueue(branch.NiceName);
                branchesSeen.Add(branch.NiceName);
            }

            commit.BranchTips.ForEach(t =>
            {
                branch = repo.BranchByName[t];
                if (!branchesSeen.Contains(branch.NiceName))
                {
                    branches.Enqueue(branch.NiceName);
                    branchesSeen.Add(branch.NiceName);
                }
            });

            foreach (var id in commit.AllChildIds)
            {
                var child = repo.CommitById[id];

                if (
                    child.ParentIds[0] != commit.Id
                    || // Skip merge children (not have commit as first parent)
                    child.IsBranchSetByUser
                ) // Skip children where branch is  set by user
                {
                    continue;
                }

                if (!commitSeen.Contains(child))
                {
                    commitQueue.Enqueue(child);
                    commitSeen.Add(child);
                }
            }
        }

        return branches.ToList();
    }

    public Repo ShowBranch(
        Repo repo,
        string branchName,
        bool includeAmbiguous,
        ShowBranches show = ShowBranches.Specified,
        int count = 1
    )
    {
        var branchNames = repo.ViewBranches.Select(b => b.Name).Append(branchName);
        if (includeAmbiguous)
        {
            var branch = repo.BranchByName[branchName];
            branchNames = branchNames.Concat(branch.AmbiguousBranchNames);
        }

        return viewRepoCreater.GetViewRepoAsync(repo, branchNames.ToArray(), show, count);
    }

    public Repo HideBranch(Repo repo, string name, bool hideAllBranches = false)
    {
        Log.Info($"Hide {name}, HideAllBranches: {hideAllBranches}");

        // The main branch is always shown, so it alone is left
        if (hideAllBranches)
            return viewRepoCreater.GetViewRepoAsync(repo, [repo.AllBranches.First(b => b.IsMainBranch).Name]);

        var branch = repo.BranchByName[name];
        branch = repo.BranchByName[branch.PrimaryName];

        var branchNames = repo
            .ViewBranches.Where(b => b.Name != branch.Name && !b.AncestorNames.Contains(branch.Name))
            .Select(b => b.Name)
            .ToArray();

        return viewRepoCreater.GetViewRepoAsync(repo, branchNames);
    }

    public Repo SetShownBranches(Repo repo, IReadOnlyList<string> branchNames) =>
        viewRepoCreater.GetViewRepoAsync(repo, branchNames);

    // Not paused, see Paused
    public Task<Result> FetchAsync(string wd) => augmentedService.FetchAsync(wd);

    public Task<Result<string>> GetRemoteUrlAsync(string wd) => git.GetRemoteUrlAsync(wd);

    public Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
        Paused(() => git.CommitAllChangesAsync(message, isAmend, wd));

    public Task<Result> CommitFilesAsync(string message, bool isAmend, IReadOnlyList<string> paths, string wd) =>
        Paused(() => git.CommitFilesAsync(message, isAmend, paths, wd));

    // Commits the paths with the lines left out of them left out, in the working tree, uncommitted:
    // a file with none whole, and one with some as the patch of the rest of its changes, the file's
    // patch read again and the left-out lines found in it as the diff showed them (ChosenLines). One
    // not found, the file having changed since, stops the commit, with nothing committed, rather than
    // a line left out going in.
    public Task<Result> CommitLinesAsync(
        string message,
        bool isAmend,
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, IReadOnlyList<ChosenLine>> leftOut,
        string wd
    ) =>
        Paused(async () =>
        {
            Dictionary<string, FilePatch> patches = [];
            foreach (var (path, lines) in leftOut)
            {
                var patchResult = await git.GetUncommittedPatchAsync(path, wd);
                if (patchResult is not FilePatch patch)
                    return patchResult.Error;
                var foundResult = ChosenLines.Find(patch, lines, path);
                if (foundResult is not IReadOnlySet<PatchLine> found)
                    return foundResult.Error;
                var chosenResult = patch.ForApply(l => !found.Contains(l));
                if (chosenResult is not FilePatch chosen)
                    return chosenResult.Error;
                patches[path] = chosen;
            }

            // A renamed file's old path is in its patch's rename, so it is not committed whole too
            var renamedFrom = patches.Values.Select(p => p.RenamedFrom).ToHashSet();
            var whole = paths.Where(p => !leftOut.ContainsKey(p) && !renamedFrom.Contains(p)).ToList();
            return await git.CommitPatchesAsync(message, isAmend, whole, patches, wd);
        });

    public async Task<Result<CommitDiff>> GetCommitDiffAsync(string commitId, DiffOptions options, string wd)
    {
        var diffTask =
            commitId == Repo.UncommittedId
                ? git.GetUncommittedDiffAsync(options, wd)
                : git.GetCommitDiffAsync(commitId, options, wd);

        var diff = await diffTask;
        if (diff is not Git.CommitDiff gitCommitDiff)
            return diff.Error;

        return GitConverter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<Result<IReadOnlyList<ChangedFile>>> GetChangedFilesAsync(string commitId, string wd)
    {
        var result = await git.GetChangedFilesAsync(commitId, wd);
        if (result is not IReadOnlyList<Git.ChangedFile> files)
            return result.Error;

        return files.Select(GitConverter.ToChangedFile).ToList();
    }

    public async Task<Result<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        DiffOptions options,
        string wd
    )
    {
        var diff = await git.GetPreviewMergeDiffAsync(sha1, sha2, message, options, wd);
        if (diff is not Git.CommitDiff gitCommitDiff)
            return diff.Error;

        return GitConverter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<Result<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        DiffOptions options,
        string wd
    )
    {
        var diff = await git.GetDiffRangeAsync(sha1, sha2, message, options, wd);
        if (diff is not Git.CommitDiff gitCommitDiff)
            return diff.Error;

        return GitConverter.ToCommitDiff(gitCommitDiff);
    }

    public Task<Result> RunDiffToolAsync(string path, string wd) => git.RunDiffToolAsync(path, wd);

    public Task<Result> RunMergeToolAsync(string path, string wd) => Paused(() => git.RunMergeToolAsync(path, wd));

    public async Task<Result<CommitDiff[]>> GetFileDiffAsync(string path, DiffOptions options, string wd)
    {
        var diffs = await git.GetFileDiffAsync(path, options, wd);
        if (diffs is not Git.CommitDiff[] gitCommitDiffs)
            return diffs.Error;
        return GitConverter.ToCommitDiffs(gitCommitDiffs);
    }

    public async Task<Result<CommitDiff[]>> GetLineHistoryAsync(
        string path,
        int firstLine,
        int lastLine,
        string reference,
        DiffOptions options,
        string wd
    )
    {
        var diffs = await git.GetLineHistoryAsync(path, firstLine, lastLine, reference, options, wd);
        if (diffs is not Git.CommitDiff[] gitCommitDiffs)
            return diffs.Error;
        return GitConverter.ToCommitDiffs(gitCommitDiffs);
    }

    public async Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd)
    {
        var blame = await git.GetBlameAsync(path, reference, wd);
        if (blame is not Git.Blame gitBlame)
            return blame.Error;
        return GitConverter.ToBlame(gitBlame);
    }

    public Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout) =>
        Paused(() => branchWriteService.CreateBranchAsync(repo, newBranchName, isCheckout));

    public Task<Result> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout
    ) => Paused(() => branchWriteService.CreateBranchFromBranchAsync(repo, newBranchName, sourceBranch, isCheckout));

    public Task<Result> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout) =>
        Paused(() => branchWriteService.CreateBranchFromCommitAsync(repo, newBranchName, sha, isCheckout));

    public Task<Result> RenameBranchAsync(string oldName, string newName, string wd) =>
        Paused(() => branchWriteService.RenameBranchAsync(oldName, newName, wd));

    public async Task<Result> PushBranchAsync(string name, string wd)
    {
        using (fileMonitor.Pause())
        using (Timing.Start($"Pushed {name}"))
        {
            var metadataTask = augmentedService.PushMetaDataAsync(wd);
            var pushTask = git.PushBranchAsync(name, wd);

            await Task.WhenAll(metadataTask, pushTask);
            return pushTask.Result;
        }
    }

    public Task<Result> ForcePushBranchAsync(Repo repo, string name)
    {
        var branch = repo.BranchByName[name];
        var remoteTipId = repo.BranchByName.TryGetValue(branch.RemoteName, out var remote) ? remote.TipId : "";
        return Paused(() => git.PushForceAsync(branch.Name, branch.RemoteName, remoteTipId, repo.Path));
    }

    public Task<Result> PullCurrentBranchAsync(string wd) => Paused(() => git.PullCurrentBranchAsync(wd));

    public Task<Result<bool>> IsPullWayConfiguredAsync(string branchName, string wd) =>
        git.IsPullWayConfiguredAsync(branchName, wd);

    public Task<Result> SetPullRebaseAsync(bool isRebase, string wd) =>
        Paused(() => git.SetPullRebaseAsync(isRebase, wd));

    public Task<Result> PullBranchAsync(string name, string wd) => Paused(() => git.PullBranchAsync(name, wd));

    public Task<Result> SwitchToAsync(Repo repo, string branchName) =>
        Paused(() => branchWriteService.SwitchToAsync(repo, branchName));

    public async Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName)
    {
        var merged = await Paused(() => branchWriteService.MergeBranchAsync(repo, branchName));
        if (merged is not IReadOnlyList<Commit> commits)
            return merged.Error;
        return converter.ToViewCommits(commits).ToList();
    }

    public async Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName)
    {
        var merged = await Paused(() => branchWriteService.MergeToBranchAsync(repo, targetName));
        if (merged is not IReadOnlyList<Commit> commits)
            return merged.Error;
        return converter.ToViewCommits(commits).ToList();
    }

    public Task<Result> RebaseBranchAsync(Repo repo, string branchName) =>
        Paused(() => branchWriteService.RebaseBranchAsync(repo, branchName));

    public Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd) =>
        Paused(() => git.RebaseOntoAsync(newBase, oldBase, wd));

    public Task<Result> CherryPickAsync(string sha, string wd) => Paused(() => git.CherryPickAsync(sha, wd));

    // These take no operation: the Git layer probes which one is in progress itself, so the UI
    // cannot act on a stale one and no enum has to be converted back down through the layers
    public Task<Result> AbortOperationAsync(string wd) => Paused(() => git.AbortOperationAsync(wd));

    public Task<Result> ContinueOperationAsync(string wd) => Paused(() => git.ContinueOperationAsync(wd));

    public Task<Result> SkipOperationAsync(string wd) => Paused(() => git.SkipOperationAsync(wd));

    public Task<Result<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd) =>
        git.GetLeftoverMarkerPathsAsync(wd);

    // The conflicted paths and what kind of conflict each is, which is what decides what can be
    // offered for it. Read from the status rather than from a diff, so a conflict git wrote no
    // markers for — a modify/delete, a binary file — is in the list like any other.
    public async Task<Result<ConflictState>> GetConflictStateAsync(string wd)
    {
        var statusResult = await git.GetStatusAsync(wd);
        if (statusResult is not Git.Status status)
            return statusResult.Error;

        return new ConflictState(
            GitConverter.ToOperation(status.Operation),
            status.Conflicts.Select(GitConverter.ToConflictedFile).ToList()
        );
    }

    // isWithBase also recovers the common ancestor of each conflict, which costs five git commands
    // and is only wanted when the base pane is actually shown. Enriched down here rather than in the
    // Cui layer because the model that comes up is narrowed and cannot be converted back down.
    public async Task<Result<ConflictFile>> GetConflictFileAsync(
        string path,
        ConflictKind kind,
        bool isWithBase,
        string wd
    )
    {
        var fileResult = await git.GetConflictFileAsync(path, GitConverter.ToGitConflictKind(kind), wd);
        if (fileResult is not Git.ConflictFile file)
            return fileResult.Error;

        if (isWithBase)
        {
            var withBase = await git.WithBaseAsync(file, wd);
            if (withBase is not Git.ConflictFile fileWithBase)
                return withBase.Error;
            file = fileWithBase;
        }

        return GitConverter.ToConflictFile(file);
    }

    public async Task<Result<ConflictSides>> GetConflictSidesAsync(string wd) =>
        await git.GetConflictSidesAsync(wd) switch
        {
            Git.ConflictSides sides => GitConverter.ToConflictSides(sides),
            Error e => e,
        };

    public Task<Result> ResolveConflictFileAsync(
        string path,
        ConflictKind kind,
        IReadOnlyList<HunkResolution> choices,
        string wd
    ) =>
        Paused(() =>
            git.ResolveConflictFileAsync(
                path,
                GitConverter.ToGitConflictKind(kind),
                choices
                    .Select(c => new Git.HunkResolution(c.Index, GitConverter.ToGitChoice(c.Choice), c.ManualText))
                    .ToList(),
                wd
            )
        );

    public Task<Result> UnresolveAsync(string path, string wd) => Paused(() => git.UnresolveAsync(path, wd));

    public Task<Result> UseWholeFileAsync(string path, bool isOurs, string wd) =>
        Paused(() => git.UseWholeFileAsync(path, isOurs, wd));

    // Keeping a file one side deleted is the same act as marking any other conflict resolved
    public Task<Result> KeepConflictedFileAsync(string path, string wd) =>
        Paused(() => git.MarkResolvedAsync(path, wd));

    public Task<Result> DeleteConflictedAsync(string path, string wd) =>
        Paused(() => git.DeleteConflictedAsync(path, wd));

    public Task<Result> DeleteBranchAsync(Repo repo, string localName, string remoteName, bool isForce) =>
        Paused(() => deletedBranchService.DeleteBranchAsync(repo, localName, remoteName, isForce));

    public Task<Result> DeleteRemoteBranchAsync(string name, string wd) =>
        Paused(() => git.DeleteRemoteBranchAsync(name, wd));

    // The worktrees need nothing of the inferred model; the list is read with the repo, and the
    // changes in each after it is shown
    public Task<Result<Repo>> GetUpdatedWorktreesRepoAsync(Repo repo) =>
        augmentedService.GetUpdatedWorktreesRepoAsync(repo);

    public Task<Result> AddWorktreeAsync(
        string path,
        string branchName,
        bool isNewBranch,
        string startPoint,
        string wd
    ) => Paused(() => git.AddWorktreeAsync(path, branchName, isNewBranch, startPoint, wd));

    public Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd) =>
        Paused(() => git.RemoveWorktreeAsync(path, isForce, wd));

    public Task<Result> PruneWorktreesAsync(string wd) => Paused(() => git.PruneWorktreesAsync(wd));

    public Task<Result<IReadOnlyList<string>>> GetIgnoredPathsAsync(IReadOnlyList<string> paths, string wd) =>
        git.GetIgnoredPathsAsync(paths, wd);

    // Each discard keeps what it throws away first, for Restore Discarded Changes (DiscardedChanges)
    public Task<Result> UndoAllUncommittedChangesAsync(string wd) =>
        Paused(() => discarded.DiscardAsync("All changes", () => git.UndoAllUncommittedChangesAsync(wd), wd));

    public Task<Result> UndoUncommittedFileAsync(string path, string wd) =>
        Paused(() => discarded.DiscardAsync(path, () => git.UndoUncommittedFileAsync(path, wd), wd));

    // Takes the chosen lines of a file's uncommitted changes back in the working tree, and only those:
    // the file's patch is read again, the lines found in it as the diff showed them (ChosenLines), and
    // the patch of them alone applied in reverse
    public Task<Result> DiscardLinesAsync(string path, IReadOnlyList<ChosenLine> lines, string wd) =>
        Paused(() =>
            discarded.DiscardAsync(
                $"{lines.Count} line{(lines.Count == 1 ? "" : "s")} of {path}",
                () => DiscardChosenLinesAsync(path, lines, wd),
                wd
            )
        );

    async Task<Result> DiscardChosenLinesAsync(string path, IReadOnlyList<ChosenLine> lines, string wd)
    {
        var patchResult = await git.GetUncommittedPatchAsync(path, wd);
        if (patchResult is not FilePatch patch)
            return patchResult.Error;
        var chosenResult = ChosenLines.Find(patch, lines, path);
        if (chosenResult is not IReadOnlySet<PatchLine> chosen)
            return chosenResult.Error;
        var discardResult = patch.ForReverseApply(chosen.Contains);
        if (discardResult is not FilePatch discard)
            return discardResult.Error;

        return await git.ApplyPatchReversedAsync(discard, wd);
    }

    // The ignored files it deletes are not kept, being the ones the uncommitted diff leaves out
    public Task<Result> CleanWorkingFolderAsync(string wd) =>
        Paused(() =>
            discarded.DiscardAsync("All changes, not the ignored files", () => git.CleanWorkingFolderAsync(wd), wd)
        );

    public Task<Result<IReadOnlyList<string>>> GetFilesToCleanAsync(string wd) => git.GetFilesToCleanAsync(wd);

    public Task<Result> UndoCommitAsync(string id, int parentIndex, string wd) =>
        Paused(() => git.UndoCommitAsync(id, parentIndex, wd));

    public Task<Result> UncommitLastCommitAsync(string wd) => Paused(() => git.UncommitLastCommitAsync(wd));

    public Task<Result> UncommitUntilCommitAsync(string id, string wd) =>
        Paused(() => git.UncommitUntilCommitAsync(id, wd));

    public Task<Result> UndoStepAsync(Repo repo, UndoStep step) => Paused(() => undoService.UndoStepAsync(repo, step));

    public Task<Result<IReadOnlyList<LostWork>>> GetLostWorkAsync(Repo repo) => augmentedService.GetLostWorkAsync(repo);

    public Task<Result<IReadOnlyList<DeletedBranch>>> GetDeletedBranchesAsync(Repo repo) =>
        deletedBranchService.GetDeletedBranchesAsync(repo);

    public Task<Result> RestoreBranchAsync(Repo repo, DeletedBranch deleted, bool isLocal, bool isRemote) =>
        Paused(() => deletedBranchService.RestoreBranchAsync(repo, deleted, isLocal, isRemote));

    public Task<Result> PullRewrittenAsync(Repo repo, RemoteRewrite rewrite) =>
        Paused(() => undoService.PullRewrittenAsync(repo, rewrite));

    public Task<Result> RestoreOriginAsync(RemoteRewrite rewrite, string wd) =>
        Paused(() => git.PushRestoreAsync(rewrite.RemoteName, rewrite.OldTipId, rewrite.NewTipId, wd));

    public Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName) =>
        Paused(() => branchWriteService.ResolveAmbiguityAsync(repo, branchName, setHumanName));

    public Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName) =>
        Paused(() => branchWriteService.SetBranchManuallyAsync(repo, commitId, setHumanName));

    public Task<Result> UnresolveAmbiguityAsync(Repo repo, string commitId) =>
        Paused(() => branchWriteService.UnresolveAmbiguityAsync(repo, commitId));

    public Task<Result<IReadOnlyList<string>>> GetFilePathsAsync(string reference, string wd) =>
        git.GetFilePathsAsync(reference, wd);

    public async Task<Result> CloneAsync(string uri, string path, string wd)
    {
        using (fileMonitor.Pause())
        using (Timing.Start())
            return await git.CloneAsync(uri, path, wd);
    }

    public Task<Result> InitRepoAsync(string path, string wd) => Paused(() => git.InitRepoAsync(path, wd));

    public Task<Result> StashAsync(string message, string wd) => Paused(() => git.StashAsync(message, wd));

    public Task<Result> StashPopAsync(string name, string wd) => Paused(() => git.StashPopAsync(name, wd));

    public Task<Result> StashApplyAsync(string name, string wd) => Paused(() => git.StashApplyAsync(name, wd));

    public async Task<Result<CommitDiff>> GetStashDiffAsync(string name, DiffOptions options, string wd)
    {
        var diffResult = await git.GetStashDiffAsync(name, options, wd);
        if (diffResult is not Git.CommitDiff diff)
            return diffResult.Error;
        return GitConverter.ToCommitDiff(diff);
    }

    // Kept first, for Restore Discarded Changes, as a discard is
    public Task<Result> StashDropAsync(string name, string wd) =>
        Paused(async () =>
        {
            var stashesResult = await git.GetStashesAsync(wd);
            if (stashesResult is not IReadOnlyList<Git.Stash> stashes)
                return stashesResult.Error;
            if (stashes.FirstOrDefault(s => s.Name == name) is not Git.Stash stash)
                return new Error($"There is no stash {name}");
            return await discarded.DropStashAsync(stash, wd);
        });

    public Task<Result<IReadOnlyList<DiscardedChange>>> GetDiscardedAsync(string wd) => discarded.GetAsync(wd);

    public Task<Result> RestoreDiscardedAsync(DiscardedChange change, string wd) =>
        Paused(() => discarded.RestoreAsync(change, wd));

    public async Task<Result<CommitDiff>> GetDiscardedDiffAsync(DiscardedChange change, DiffOptions options, string wd)
    {
        var diffResult = await discarded.GetDiffAsync(change, options, wd);
        if (diffResult is not Git.CommitDiff diff)
            return diffResult.Error;
        return GitConverter.ToCommitDiff(diff);
    }

    public async Task<Result<string>> GetChangeLogAsync(string? newRelease = null)
    {
        var repoResult = await GetRepoAsync("", ["main"]);
        if (repoResult is not Repo repo)
            return repoResult.Error;

        return ChangeLog.Create(repo, newRelease, DateTime.UtcNow);
    }

    public Task<Result> AddTagAsync(string name, string commitId, string wd) =>
        Paused(() => git.AddTagAsync(name, commitId, wd));

    public Task<Result> AddAnnotatedTagAsync(string name, string message, string commitId, string wd) =>
        Paused(() => git.AddAnnotatedTagAsync(name, message, commitId, wd));

    public Task<Result> RemoveTagAsync(string name, bool isOnOrigin, string wd) =>
        Paused(() => RemoveTagHereAndOnOriginAsync(name, isOnOrigin, wd));

    async Task<Result> RemoveTagHereAndOnOriginAsync(string name, bool isOnOrigin, string wd)
    {
        if (await git.RemoveTagAsync(name, wd) is Error e)
            return e;
        return isOnOrigin ? await git.DeleteRemoteTagAsync(name, wd) : Result.Ok;
    }

    // Every tag, and whether origin has it. A record of origin's tags that cannot be read is taken as
    // none on origin, which a Push then corrects, rather than as no tags at all.
    public async Task<Result<IReadOnlyList<RepoTag>>> GetTagsAsync(string wd)
    {
        var tagsTask = git.GetTagsAsync(wd);
        var originTask = git.GetTrackedRemoteTagsAsync(wd);
        await Task.WhenAll(tagsTask, originTask);
        if (tagsTask.Result is not IReadOnlyList<Git.Tag> tags)
            return tagsTask.Result.Error;
        var origin = originTask.Result is IReadOnlyDictionary<string, string> o ? o : new Dictionary<string, string>();

        return new Result<IReadOnlyList<RepoTag>>(RepoTag.Of(tags, origin));
    }

    public Task<Result> PushTagAsync(string name, string wd) => Paused(() => git.PushTagAsync(name, wd));

    public Task<Result> SwitchToCommitAsync(string commitId, string wd) =>
        Paused(() => git.CheckoutAsync(commitId, wd));

    public Task<Result> SquashCommitsAsync(Repo repo, string id1, string id2, string msg) =>
        Paused(() => commitRewriteService.SquashCommitsAsync(repo, id1, id2, msg));

    public Task<Result> AmendOlderCommitAsync(Repo repo, string id, string message, IReadOnlyList<string> paths) =>
        Paused(() => commitRewriteService.AmendOlderCommitAsync(repo, id, message, paths));

    public Task<Result> DropCommitAsync(Repo repo, string id) =>
        Paused(() => commitRewriteService.DropCommitAsync(repo, id));
}
