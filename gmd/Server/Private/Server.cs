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

    public Repo SetShownBranches(Repo repo, IReadOnlyList<string> branchNames) =>
        viewRepoCreater.GetViewRepoAsync(repo, branchNames);

    public Task<Result> FetchAsync(string wd) => augmentedService.FetchAsync(wd);

    public Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
        augmentedService.CommitAllChangesAsync(message, isAmend, wd);

    public async Task<Result<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd)
    {
        var diffTask =
            commitId == Repo.UncommittedId
                ? git.GetUncommittedDiff(contextLines, wd)
                : git.GetCommitDiffAsync(commitId, contextLines, wd);

        var diff = await diffTask;
        if (diff is not Git.CommitDiff gitCommitDiff)
            return diff.Error;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<Result<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    )
    {
        var diff = await git.GetPreviewMergeDiffAsync(sha1, sha2, message, contextLines, wd);
        if (diff is not Git.CommitDiff gitCommitDiff)
            return diff.Error;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<Result<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    )
    {
        var diff = await git.GetDiffRangeAsync(sha1, sha2, message, contextLines, wd);
        if (diff is not Git.CommitDiff gitCommitDiff)
            return diff.Error;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public Task<Result> RunDiffToolAsync(string path, string wd) => git.RunDiffToolAsync(path, wd);

    public Task<Result> RunMergeToolAsync(string path, string wd) => git.RunMergeToolAsync(path, wd);

    public async Task<Result<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd)
    {
        var diffs = await git.GetFileDiffAsync(path, contextLines, wd);
        if (diffs is not Git.CommitDiff[] gitCommitDiffs)
            return diffs.Error;
        return converter.ToCommitDiffs(gitCommitDiffs);
    }

    public async Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd)
    {
        var blame = await git.GetBlameAsync(path, reference, wd);
        if (blame is not Git.Blame gitBlame)
            return blame.Error;
        return converter.ToBlame(gitBlame);
    }

    public Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd) =>
        augmentedService.CreateBranchAsync(repo, newBranchName, isCheckout, wd);

    public Task<Result> CreateBranchFromBranchAsync(
        Repo repo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout,
        string wd
    ) => augmentedService.CreateBranchFromBranchAsync(repo, newBranchName, sourceBranch, isCheckout, wd);

    public Task<Result> CreateBranchFromCommitAsync(
        Repo repo,
        string newBranchName,
        string sha,
        bool isCheckout,
        string wd
    ) => augmentedService.CreateBranchFromCommitAsync(repo, newBranchName, sha, isCheckout, wd);

    public Task<Result> RenameBranchAsync(string oldName, string newName, string wd) =>
        augmentedService.RenameBranchAsync(oldName, newName, wd);

    public async Task<Result> PushBranchAsync(string name, string wd)
    {
        using (Timing.Start($"Pushed {name}"))
        {
            var metadataTask = augmentedService.PushMetaDataAsync(wd);
            var pushTask = git.PushBranchAsync(name, wd);

            await Task.WhenAll(metadataTask, pushTask);
            return pushTask.Result;
        }
    }

    public Task<Result> PushCurrentBranchAsync(bool isForce, string wd) => git.PushCurrentBranchAsync(isForce, wd);

    public Task<Result> PullCurrentBranchAsync(string wd) => git.PullCurrentBranchAsync(wd);

    public Task<Result> PullBranchAsync(string name, string wd) => git.PullBranchAsync(name, wd);

    public Task<Result> SwitchToAsync(Repo repo, string branchName) => augmentedService.SwitchToAsync(repo, branchName);

    public async Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName)
    {
        var merged = await augmentedService.MergeBranchAsync(repo, branchName);
        if (merged is not IReadOnlyList<Commit> commits)
            return merged.Error;
        return converter.ToViewCommits(commits).ToList();
    }

    public async Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName)
    {
        var merged = await augmentedService.MergeToBranchAsync(repo, targetName);
        if (merged is not IReadOnlyList<Commit> commits)
            return merged.Error;
        return converter.ToViewCommits(commits).ToList();
    }

    public Task<Result> RebaseBranchAsync(Repo repo, string branchName) =>
        augmentedService.RebaseBranchAsync(repo, branchName);

    public Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd) =>
        git.RebaseOntoAsync(newBase, oldBase, wd);

    public Task<Result> CherryPickAsync(string sha, string wd) => git.CherryPickAsync(sha, wd);

    // These take no operation: the Git layer probes which one is in progress itself, so the UI
    // cannot act on a stale one and no enum has to be converted back down through the layers
    public Task<Result> AbortOperationAsync(string wd) => git.AbortOperationAsync(wd);

    public Task<Result> ContinueOperationAsync(string wd) => git.ContinueOperationAsync(wd);

    public Task<Result> SkipOperationAsync(string wd) => git.SkipOperationAsync(wd);

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
            Augmented.Private.StatusConverter.ToOperation(status.Operation),
            status.Conflicts.Select(c => new ConflictedFile(c.Path, ToConflictKind(c.Kind))).ToList()
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
        var fileResult = await git.GetConflictFileAsync(path, ToGitConflictKind(kind), wd);
        if (fileResult is not Git.ConflictFile file)
            return fileResult.Error;

        if (isWithBase)
        {
            var withBase = await git.WithBaseAsync(file, wd);
            if (withBase is not Git.ConflictFile fileWithBase)
                return withBase.Error;
            file = fileWithBase;
        }

        return converter.ToConflictFile(file);
    }

    public Task<Result> ResolveConflictFileAsync(
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

    public Task<Result> UnresolveAsync(string path, string wd) => git.UnresolveAsync(path, wd);

    public Task<Result> UseWholeFileAsync(string path, bool isOurs, string wd) =>
        git.UseWholeFileAsync(path, isOurs, wd);

    // Keeping a file one side deleted is the same act as marking any other conflict resolved
    public Task<Result> KeepConflictedFileAsync(string path, string wd) => git.MarkResolvedAsync(path, wd);

    public Task<Result> DeleteConflictedAsync(string path, string wd) => git.DeleteConflictedAsync(path, wd);

    // Member for member switches rather than a cast between the two enums: a cast would keep
    // compiling and start lying the day either one gains or reorders a member
    static ConflictKind ToConflictKind(Git.ConflictKind kind) => ViewRepoConverter.ToConflictKind(kind);

    static Git.ConflictKind ToGitConflictKind(ConflictKind kind) => ViewRepoConverter.ToGitConflictKind(kind);

    public Task<Result> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        git.DeleteLocalBranchAsync(name, isForced, wd);

    public Task<Result> DeleteRemoteBranchAsync(string name, string wd) => git.DeleteRemoteBranchAsync(name, wd);

    // The worktrees need nothing of the inferred model, so they go straight through, except that
    // the writes pause the file monitor in the augmented service like the other writes do
    public Task<Result<Repo>> GetUpdatedWorktreesRepoAsync(Repo repo) =>
        augmentedService.GetUpdatedWorktreesRepoAsync(repo);

    public Task<Result> AddWorktreeAsync(
        string path,
        string branchName,
        bool isNewBranch,
        string startPoint,
        string wd
    ) => augmentedService.AddWorktreeAsync(path, branchName, isNewBranch, startPoint, wd);

    public Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd) =>
        augmentedService.RemoveWorktreeAsync(path, isForce, wd);

    public Task<Result> PruneWorktreesAsync(string wd) => augmentedService.PruneWorktreesAsync(wd);

    public Task<Result<IReadOnlyList<string>>> GetIgnoredPathsAsync(IReadOnlyList<string> paths, string wd) =>
        git.GetIgnoredPathsAsync(paths, wd);

    public Task<Result> UndoAllUncommittedChangesAsync(string wd) => git.UndoAllUncommittedChangesAsync(wd);

    public Task<Result> UndoUncommittedFileAsync(string path, string wd) => git.UndoUncommittedFileAsync(path, wd);

    public Task<Result> CleanWorkingFolderAsync(string wd) => git.CleanWorkingFolderAsync(wd);

    public Task<Result> UndoCommitAsync(string id, int parentIndex, string wd) =>
        git.UndoCommitAsync(id, parentIndex, wd);

    public Task<Result> UncommitLastCommitAsync(string wd) => git.UncommitLastCommitAsync(wd);

    public Task<Result> UncommitUntilCommitAsync(string id, string wd) => git.UncommitUntilCommitAsync(id, wd);

    public Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName) =>
        augmentedService.ResolveAmbiguityAsync(repo, branchName, setHumanName);

    public Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName) =>
        augmentedService.SetBranchManuallyAsync(repo, commitId, setHumanName);

    public Task<Result> UnresolveAmbiguityAsync(Repo repo, string commitId) =>
        augmentedService.UnresolveAmbiguityAsync(repo, commitId);

    public Task<Result<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) =>
        git.GetFileAsync(reference, wd);

    public async Task<Result> CloneAsync(string uri, string path, string wd)
    {
        using (Timing.Start())
            return await git.CloneAsync(uri, path, wd);
    }

    public async Task<Result> InitRepoAsync(string path, string wd) => await git.InitRepoAsync(path, wd);

    public Task<Result> StashAsync(string message, string wd) => git.StashAsync(message, wd);

    public Task<Result> StashPopAsync(string name, string wd) => git.StashPopAsync(name, wd);

    public async Task<Result<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd)
    {
        var diffResult = await git.GetStashDiffAsync(name, contextLines, wd);
        if (diffResult is not Git.CommitDiff diff)
            return diffResult.Error;
        return converter.ToCommitDiff(diff);
    }

    public Task<Result> StashDropAsync(string name, string wd) => git.StashDropAsync(name, wd);

    public async Task<Result<string>> GetChangeLogAsync()
    {
        var repoResult = await GetRepoAsync("", ["main"]);
        if (repoResult is not Repo repo)
            return repoResult.Error;

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

    public Task<Result> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd) =>
        augmentedService.AddTagAsync(name, commitId, hasRemoteBranch, wd);

    public Task<Result> AddAnnotatedTagAsync(
        string name,
        string message,
        string commitId,
        bool hasRemoteBranch,
        string wd
    ) => augmentedService.AddAnnotatedTagAsync(name, message, commitId, hasRemoteBranch, wd);

    public Task<Result> RemoveTagAsync(string name, bool hasRemoteBranch, string wd) =>
        augmentedService.RemoveTagAsync(name, hasRemoteBranch, wd);

    public Task<Result> SwitchToCommitAsync(string commitId, string wd) => git.CheckoutAsync(commitId, wd);

    public Task<Result> SquashCommits(Repo repo, string id1, string id2, string msg) =>
        augmentedService.SquashCommits(repo, id1, id2, msg);
}
