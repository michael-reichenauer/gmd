using System.Text;
using gmd.Git;
using gmd.Server.Private.Augmented;

namespace gmd.Server.Private;

[SingleInstance]
class Server : IServer
{
    readonly IGit git;
    readonly IAugmentedService augmentedService;
    readonly IViewRepoConverter converter;
    readonly IViewRepoCreater viewRepoCreater;

    public Server(
        IGit git,
        IAugmentedService augmentedService,
        IViewRepoConverter converter,
        IViewRepoCreater viewRepoCreater
    )
    {
        this.git = git;
        this.augmentedService = augmentedService;
        this.converter = converter;
        this.viewRepoCreater = viewRepoCreater;
        augmentedService.RepoChange += e => RepoChange?.Invoke(e);
        augmentedService.StatusChange += e => StatusChange?.Invoke(e);
    }

    public event Action<ChangeEvent>? RepoChange;
    public event Action<ChangeEvent>? StatusChange;

    public string CurrentAuthor => git.CurrentAuthor;

    public async Task<R<Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        if (!Try(out var augmentedRepo, out var e, await augmentedService.GetRepoAsync(path)))
            return e;

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, showBranches);
    }

    public async Task<R<Repo>> GetUpdateStatusRepoAsync(Repo repo)
    {
        var branches = repo.ViewBranches.Select(b => b.Name).ToArray();

        if (!Try(out var augmentedRepo, out var e, await augmentedService.UpdateRepoStatusAsync(repo)))
            return e;
        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, branches);
    }

    public async Task<R<Repo>> GetFilteredRepoAsync(Repo repo, string filter, int maxCount)
    {
        await Task.CompletedTask;
        return viewRepoCreater.GetFilteredViewRepoAsync(repo, filter, maxCount);
    }

    public IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId, bool isAll = true)
    {
        if (commitId == Repo.UncommittedId)
            return new List<Branch>();

        bool FilterOnShown(Commit cc) => isAll || !cc.IsInView;
        // Getting all branches that are not the same as the commit branch.
        // Also exclude branches that are shown if isNotShown is true
        var commit = repo.CommitById[commitId];
        var branch = repo.BranchByName[commit.BranchName];

        return commit
            .AllChildIds.Concat(commit.ParentIds) // All children and parents commit ids
            .Select(id => repo.CommitById[id]) // As commits
            .Where(cc => cc.BranchPrimaryName != commit.BranchPrimaryName) // Skip same branch
            .Concat(commit.Id == branch.TipId ? [commit] : []) // Add commit branch if tip
            .Where(FilterOnShown) // Exclude shown branches (or not)
            .Select(cc => cc.BranchPrimaryName)
            .Distinct()
            .Select(n => repo.BranchByName[n])
            .ToList();
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

        if (hideAllBranches)
            return viewRepoCreater.GetViewRepoAsync(repo, new[] { "main" });

        var branch = repo.BranchByName[name];
        branch = repo.BranchByName[branch.PrimaryName];

        var branchNames = repo
            .ViewBranches.Where(b => b.Name != branch.Name && !b.AncestorNames.Contains(branch.Name))
            .Select(b => b.Name)
            .ToArray();

        return viewRepoCreater.GetViewRepoAsync(repo, branchNames);
    }

    public Task<R> FetchAsync(string wd) => augmentedService.FetchAsync(wd);

    public Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
        augmentedService.CommitAllChangesAsync(message, isAmend, wd);

    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd)
    {
        var diffTask =
            commitId == Repo.UncommittedId
                ? git.GetUncommittedDiff(contextLines, wd)
                : git.GetCommitDiffAsync(commitId, contextLines, wd);

        if (!Try(out var gitCommitDiff, out var e, await diffTask))
            return e;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<R<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    )
    {
        if (
            !Try(
                out var gitCommitDiff,
                out var e,
                await git.GetPreviewMergeDiffAsync(sha1, sha2, message, contextLines, wd)
            )
        )
            return e;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<R<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    )
    {
        if (!Try(out var gitCommitDiff, out var e, await git.GetDiffRangeAsync(sha1, sha2, message, contextLines, wd)))
            return e;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public Task<R> RunDiffToolAsync(string path, string wd) => git.RunDiffToolAsync(path, wd);

    public Task<R> RunMergeToolAsync(string path, string wd) => git.RunMergeToolAsync(path, wd);

    public async Task<R<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd)
    {
        if (!Try(out var gitCommitDiffs, out var e, await git.GetFileDiffAsync(path, contextLines, wd)))
            return e;
        return converter.ToCommitDiffs(gitCommitDiffs);
    }

    public async Task<R<Blame>> GetBlameAsync(string path, string reference, string wd)
    {
        if (!Try(out var gitBlame, out var e, await git.GetBlameAsync(path, reference, wd)))
            return e;
        return converter.ToBlame(gitBlame);
    }

    public Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd) =>
        augmentedService.CreateBranchAsync(repo, newBranchName, isCheckout, wd);

    public Task<R> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout,
        string wd
    ) => augmentedService.CreateBranchFromBranchAsync(repo, newBranchName, sourceBranch, isCheckout, wd);

    public Task<R> CreateBranchFromCommitAsync(
        Repo repo,
        string newBranchName,
        string sha,
        bool isCheckout,
        string wd
    ) => augmentedService.CreateBranchFromCommitAsync(repo, newBranchName, sha, isCheckout, wd);

    public Task<R> RenameBranchAsync(string oldName, string newName, string wd) =>
        augmentedService.RenameBranchAsync(oldName, newName, wd);

    public async Task<R> PushBranchAsync(string name, string wd)
    {
        using (Timing.Start($"Pushed {name}"))
        {
            var metadataTask = augmentedService.PushMetaDataAsync(wd);
            var pushTask = git.PushBranchAsync(name, wd);

            await Task.WhenAll(metadataTask, pushTask);
            return pushTask.Result;
        }
    }

    public Task<R> PushCurrentBranchAsync(bool isForce, string wd) => git.PushCurrentBranchAsync(isForce, wd);

    public Task<R> PullCurrentBranchAsync(string wd) => git.PullCurrentBranchAsync(wd);

    public Task<R> PullBranchAsync(string name, string wd) => git.PullBranchAsync(name, wd);

    public Task<R> SwitchToAsync(Repo repo, string branchName) => augmentedService.SwitchToAsync(repo, branchName);

    public async Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName)
    {
        if (!Try(out var commits, out var e, await augmentedService.MergeBranchAsync(repo, branchName)))
            return e;
        return converter.ToViewCommits(commits).ToList();
    }

    public async Task<R<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName)
    {
        if (!Try(out var commits, out var e, await augmentedService.MergeToBranchAsync(repo, targetName)))
            return e;
        return converter.ToViewCommits(commits).ToList();
    }

    public Task<R> RebaseBranchAsync(Repo repo, string branchName) =>
        augmentedService.RebaseBranchAsync(repo, branchName);

    public Task<R> RebaseOntoAsync(string newBase, string oldBase, string wd) =>
        git.RebaseOntoAsync(newBase, oldBase, wd);

    public Task<R> CherryPickAsync(string sha, string wd) => git.CherryPickAsync(sha, wd);

    // These take no operation: the Git layer probes which one is in progress itself, so the UI
    // cannot act on a stale one and no enum has to be converted back down through the layers
    public Task<R> AbortOperationAsync(string wd) => git.AbortOperationAsync(wd);

    public Task<R> ContinueOperationAsync(string wd) => git.ContinueOperationAsync(wd);

    public Task<R> SkipOperationAsync(string wd) => git.SkipOperationAsync(wd);

    public Task<R<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd) => git.GetLeftoverMarkerPathsAsync(wd);

    // The conflicted paths and what kind of conflict each is, which is what decides what can be
    // offered for it. Read from the status rather than from a diff, so a conflict git wrote no
    // markers for — a modify/delete, a binary file — is in the list like any other.
    public async Task<R<ConflictState>> GetConflictStateAsync(string wd)
    {
        if (!Try(out var status, out var e, await git.GetStatusAsync(wd)))
            return e;

        return new ConflictState(
            Augmented.Private.StatusConverter.ToOperation(status.Operation),
            status.Conflicts.Select(c => new ConflictedFile(c.Path, ToConflictKind(c.Kind))).ToList()
        );
    }

    // isWithBase also recovers the common ancestor of each conflict, which costs five git commands
    // and is only wanted when the base pane is actually shown. Enriched down here rather than in the
    // Cui layer because the model that comes up is narrowed and cannot be converted back down.
    public async Task<R<ConflictFile>> GetConflictFileAsync(string path, ConflictKind kind, bool isWithBase, string wd)
    {
        if (!Try(out var file, out var e, await git.GetConflictFileAsync(path, ToGitConflictKind(kind), wd)))
            return e;

        if (isWithBase && !Try(out file, out e, await git.WithBaseAsync(file, wd)))
            return e;

        return converter.ToConflictFile(file);
    }

    public Task<R> ResolveConflictFileAsync(
        string path,
        ConflictKind kind,
        IReadOnlyList<HunkResolution> choices,
        string wd
    ) =>
        git.ResolveConflictFileAsync(
            path,
            ToGitConflictKind(kind),
            choices
                .Select(c => new Git.HunkResolution(c.Index, ViewRepoConverter.ToGitChoice(c.Choice), c.ManualText))
                .ToList(),
            wd
        );

    public Task<R> UnresolveAsync(string path, string wd) => git.UnresolveAsync(path, wd);

    public Task<R> UseWholeFileAsync(string path, bool isOurs, string wd) => git.UseWholeFileAsync(path, isOurs, wd);

    // Keeping a file one side deleted is the same act as marking any other conflict resolved
    public Task<R> KeepConflictedFileAsync(string path, string wd) => git.MarkResolvedAsync(path, wd);

    public Task<R> DeleteConflictedAsync(string path, string wd) => git.DeleteConflictedAsync(path, wd);

    // Member for member switches rather than a cast between the two enums: a cast would keep
    // compiling and start lying the day either one gains or reorders a member
    static ConflictKind ToConflictKind(Git.ConflictKind kind) => ViewRepoConverter.ToConflictKind(kind);

    static Git.ConflictKind ToGitConflictKind(ConflictKind kind) => ViewRepoConverter.ToGitConflictKind(kind);

    public Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        git.DeleteLocalBranchAsync(name, isForced, wd);

    public Task<R> DeleteRemoteBranchAsync(string name, string wd) => git.DeleteRemoteBranchAsync(name, wd);

    // The worktrees need nothing of the inferred model, so they go straight through, except that
    // the writes pause the file monitor in the augmented service like the other writes do
    public Task<R<Repo>> GetUpdatedWorktreesRepoAsync(Repo repo) => augmentedService.GetUpdatedWorktreesRepoAsync(repo);

    public Task<R> AddWorktreeAsync(string path, string branchName, bool isNewBranch, string startPoint, string wd) =>
        augmentedService.AddWorktreeAsync(path, branchName, isNewBranch, startPoint, wd);

    public Task<R> RemoveWorktreeAsync(string path, bool isForce, string wd) =>
        augmentedService.RemoveWorktreeAsync(path, isForce, wd);

    public Task<R> PruneWorktreesAsync(string wd) => augmentedService.PruneWorktreesAsync(wd);

    public Task<R<IReadOnlyList<string>>> GetIgnoredPathsAsync(IReadOnlyList<string> paths, string wd) =>
        git.GetIgnoredPathsAsync(paths, wd);

    public Task<R> UndoAllUncommittedChangesAsync(string wd) => git.UndoAllUncommittedChangesAsync(wd);

    public Task<R> UndoUncommittedFileAsync(string path, string wd) => git.UndoUncommittedFileAsync(path, wd);

    public Task<R> CleanWorkingFolderAsync(string wd) => git.CleanWorkingFolderAsync(wd);

    public Task<R> UndoCommitAsync(string id, int parentIndex, string wd) => git.UndoCommitAsync(id, parentIndex, wd);

    public Task<R> UncommitLastCommitAsync(string wd) => git.UncommitLastCommitAsync(wd);

    public Task<R> UncommitUntilCommitAsync(string id, string wd) => git.UncommitUntilCommitAsync(id, wd);

    public Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName) =>
        augmentedService.ResolveAmbiguityAsync(repo, branchName, setHumanName);

    public Task<R> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName) =>
        augmentedService.SetBranchManuallyAsync(repo, commitId, setHumanName);

    public Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId) =>
        augmentedService.UnresolveAmbiguityAsync(repo, commitId);

    public Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) => git.GetFileAsync(reference, wd);

    public async Task<R> CloneAsync(string uri, string path, string wd)
    {
        using (Timing.Start())
            return await git.CloneAsync(uri, path, wd);
    }

    public async Task<R> InitRepoAsync(string path, string wd) => await git.InitRepoAsync(path, wd);

    public Task<R> StashAsync(string message, string wd) => git.StashAsync(message, wd);

    public Task<R> StashPopAsync(string name, string wd) => git.StashPopAsync(name, wd);

    public async Task<R<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd)
    {
        if (!Try(out var diff, out var e, await git.GetStashDiffAsync(name, contextLines, wd)))
            return e;
        return converter.ToCommitDiff(diff);
    }

    public Task<R> StashDropAsync(string name, string wd) => git.StashDropAsync(name, wd);

    public async Task<R<string>> GetChangeLogAsync()
    {
        if (!Try(out var repo, out var e, await GetRepoAsync("", new[] { "main" })))
            return e;

        var nextTag = "Current";
        var nextTagDate = DateTime.UtcNow;
        var totalText = new StringBuilder();
        var text = "";
        var count = 0;
        foreach (Commit c in repo.ViewCommits)
        {
            var message = c.Message;
            var parts = c.Message.Split('\n');
            if (c.ParentIds.Count > 1 && parts.Length > 2 && parts[1].Trim() == "")
            {
                message = string.Join('\n', parts.Skip(2));
            }
            else if (parts.Length == 1)
            {
                message = $"- {parts[0]}";
            }

            // Adjust some message lines
            message = message
                .Split('\n')
                .Select(l =>
                {
                    if (l.StartsWith("- Fix "))
                        l = $"- Fixed {l[6..]}";
                    if (l.StartsWith("- Add "))
                        l = $"- Added {l[6..]}";
                    if (l.StartsWith("- Update "))
                        l = $"- Updated {l[9..]}";
                    return l;
                })
                .Join("\n");

            var tag = c.Tags.FirstOrDefault(t => t.Name.StartsWith('v') && Version.TryParse(t.Name[1..], out var _));
            if (tag != null)
            { // New version
                if (text.Trim() != "")
                {
                    if (nextTag == "Current")
                    {
                        totalText.Append($"\n## [{nextTag}] - {nextTagDate.IsoDate()}\n{text}\n");
                    }
                    else
                    {
                        totalText.Append($"\n## [{nextTag}] - {nextTagDate.IsoDate()}\n{text}\n");
                    }
                }

                nextTag = tag.Name;
                nextTagDate = c.AuthorTime;
                text = "";
                count++;
            }

            text += message;
        }

        return $"\n{count} releases:\n{totalText}";
    }

    public Task<R> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd) =>
        augmentedService.AddTagAsync(name, commitId, hasRemoteBranch, wd);

    public Task<R> AddAnnotatedTagAsync(
        string name,
        string message,
        string commitId,
        bool hasRemoteBranch,
        string wd
    ) => augmentedService.AddAnnotatedTagAsync(name, message, commitId, hasRemoteBranch, wd);

    public Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd) =>
        augmentedService.RemoveTagAsync(name, hasRemoteBranch, wd);

    public Task<R> SwitchToCommitAsync(string commitId, string wd) => git.CheckoutAsync(commitId, wd);

    public Task<R> SquashCommits(Repo repo, string id1, string id2, string msg) =>
        augmentedService.SquashCommits(repo, id1, id2, msg);
}
