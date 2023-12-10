using System.Text;
using gmd.Git;
using gmd.Server.Private.Augmented;

namespace gmd.Server.Private;

[SingleInstance]
class Server : IServer
{
    readonly IGit git;
    readonly IAugmentedService augmentedService;
    readonly IConverter converter;
    readonly IViewRepoCreater viewRepoCreater;

    public Server(
        IGit git,
        IAugmentedService augmentedService,
        IConverter converter,
        IViewRepoCreater viewRepoCreater)
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


    public async Task<R<Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        if (!Try(out var augmentedRepo, out var e,
            await augmentedService.GetRepoAsync(path))) return e;

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, showBranches);
    }

    public async Task<R<Repo>> GetUpdateStatusRepoAsync(Repo repo)
    {
        var branches = repo.ViewBranches.Select(b => b.Name).ToArray();

        if (!Try(out var augmentedRepo, out var e, await augmentedService.UpdateRepoStatusAsync(repo))) return e;

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, branches);
    }

    public async Task<R<Repo>> GetFilteredRepoAsync(Repo repo, string filter, int maxCount)
    {
        await Task.CompletedTask;
        return viewRepoCreater.GetFilteredViewRepoAsync(repo, filter, maxCount);
    }


    public IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId, bool isAll = true)
    {
        if (commitId == Repo.UncommittedId) return new List<Branch>();

        bool FilterOnShown(Commit cc) => isAll || !cc.IsInView;
        // Getting all branches that are not the same as the commit branch
        // Also exclude branches that are shown if isNotShown is true
        var commit = repo.CommitById[commitId];
        var branch = repo.BranchByName[commit.BranchName];

        return
            commit.AllChildIds.Concat(commit.ParentIds)                    // All children and parents commit ids         
            .Select(id => repo.CommitById[id])               // As commits
            .Where(cc => cc.BranchPrimaryName != commit.BranchPrimaryName) // Skip same branch
            .Concat(commit.Id == branch.TipId ? new[] { commit } : new Commit[0])                                       // Add commit branch if tip
            .Where(FilterOnShown)                                          // Exclude shown branches (or not)
            .Select(cc => cc.BranchPrimaryName)
            .Distinct()
            .Select(n => repo.BranchByName[n])
            .ToList();
    }

    public IReadOnlyList<string> GetPossibleBranchNames(Repo repo, string commitId, int maxCount)
    {
        if (commitId == Repo.UncommittedId) return new List<string>();

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

                if (child.ParentIds[0] != commit.Id ||  // Skip merge children (not have commit as first parent)
                    child.IsBranchSetByUser)            // Skip children where branch is  set by user
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


    public Repo ShowBranch(Repo repo, string branchName, bool includeAmbiguous, ShowBranches show = ShowBranches.Specified, int count = 1)
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

        if (hideAllBranches) return viewRepoCreater.GetViewRepoAsync(repo, new[] { "main" });

        var branch = repo.BranchByName[name];
        branch = repo.BranchByName[branch.PrimaryName];

        var branchNames = repo.ViewBranches
            .Where(b => b.Name != branch.Name && !b.AncestorNames.Contains(branch.Name))
            .Select(b => b.Name)
            .ToArray();

        return viewRepoCreater.GetViewRepoAsync(repo, branchNames);
    }

    public Task<R> FetchAsync(string wd) => augmentedService.FetchAsync(wd);


    public Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
          augmentedService.CommitAllChangesAsync(message, isAmend, wd);

    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd)
    {
        var diffTask = commitId == Repo.UncommittedId
            ? git.GetUncommittedDiff(wd)
            : git.GetCommitDiffAsync(commitId, wd);

        if (!Try(out var gitCommitDiff, out var e, await diffTask)) return e;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<R<CommitDiff>> GetPreviewMergeDiffAsync(string sha1, string sha2, string message, string wd)
    {
        if (!Try(out var gitCommitDiff, out var e, await git.GetPreviewMergeDiffAsync(sha1, sha2, message, wd))) return e;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<R<CommitDiff>> GetDiffRangeAsync(string sha1, string sha2, string message, string wd)
    {
        if (!Try(out var gitCommitDiff, out var e, await git.GetDiffRangeAsync(sha1, sha2, message, wd))) return e;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd)
    {
        if (!Try(out var gitCommitDiffs, out var e, await git.GetFileDiffAsync(path, wd))) return e;
        return converter.ToCommitDiffs(gitCommitDiffs);
    }


    public Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd) =>
      augmentedService.CreateBranchAsync(repo, newBranchName, isCheckout, wd);

    public Task<R> CreateBranchFromBranchAsync(Repo repo, string newBranchName, string sourceBranch, bool isCheckout, string wd) =>
        augmentedService.CreateBranchFromBranchAsync(repo, newBranchName, sourceBranch, isCheckout, wd);


    public Task<R> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd) =>
        augmentedService.CreateBranchFromCommitAsync(repo, newBranchName, sha, isCheckout, wd);

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

    public Task<R> PushCurrentBranchAsync(bool isForce, string wd) =>
        git.PushCurrentBranchAsync(isForce, wd);

    public Task<R> PullCurrentBranchAsync(string wd) =>
        git.PullCurrentBranchAsync(wd);

    public Task<R> PullBranchAsync(string name, string wd) =>
        git.PullBranchAsync(name, wd);

    public Task<R> SwitchToAsync(Repo repo, string branchName) =>
        augmentedService.SwitchToAsync(repo, branchName);

    public async Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName)
    {
        if (!Try(out var commits, out var e, await augmentedService.MergeBranchAsync(repo, branchName))) return e;
        return converter.ToViewCommits(commits).ToList();
    }

    public Task<R> RebaseBranchAsync(Repo repo, string branchName)
    {
        return augmentedService.RebaseBranchAsync(repo, branchName);
    }

    public Task<R> CherryPickAsync(string sha, string wd) =>
        git.CherryPickAsync(sha, wd);

    public Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        git.DeleteLocalBranchAsync(name, isForced, wd);

    public Task<R> DeleteRemoteBranchAsync(string name, string wd) =>
        git.DeleteRemoteBranchAsync(name, wd);

    public Task<R> UndoAllUncommittedChangesAsync(string wd) =>
        git.UndoAllUncommittedChangesAsync(wd);

    public Task<R> UndoUncommittedFileAsync(string path, string wd) =>
        git.UndoUncommittedFileAsync(path, wd);

    public Task<R> CleanWorkingFolderAsync(string wd) =>
        git.CleanWorkingFolderAsync(wd);

    public Task<R> UndoCommitAsync(string id, int parentIndex, string wd) =>
        git.UndoCommitAsync(id, parentIndex, wd);

    public Task<R> UncommitLastCommitAsync(string wd) =>
        git.UncommitLastCommitAsync(wd);

    public Task<R> UncommitUntilCommitAsync(string id, string wd) =>
        git.UncommitUntilCommitAsync(id, wd);

    public Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName) =>
        augmentedService.ResolveAmbiguityAsync(repo, branchName, setHumanName);

    public Task<R> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName) =>
        augmentedService.SetBranchManuallyAsync(repo, commitId, setHumanName);

    public Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId) =>
        augmentedService.UnresolveAmbiguityAsync(repo, commitId);

    public Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) =>
        git.GetFileAsync(reference, wd);

    public async Task<R> CloneAsync(string uri, string path, string wd)
    {
        using (Timing.Start()) return await git.CloneAsync(uri, path, wd);
    }

    public async Task<R> InitRepoAsync(string path, string wd) =>
     await git.InitRepoAsync(path, wd);



    public Task<R> StashAsync(string message, string wd) => git.StashAsync(message, wd);

    public Task<R> StashPopAsync(string name, string wd) => git.StashPopAsync(name, wd);

    public async Task<R<CommitDiff>> GetStashDiffAsync(string name, string wd)
    {
        if (!Try(out var diff, out var e, await git.GetStashDiffAsync(name, wd))) return e;
        return converter.ToCommitDiff(diff);
    }

    public Task<R> StashDropAsync(string name, string wd) => git.StashDropAsync(name, wd);

    public async Task<R<string>> GetChangeLogAsync()
    {
        if (!Try(out var repo, out var e, await GetRepoAsync("", new[] { "main" }))) return e;

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
            message = message.Split('\n').Select(l =>
            {
                if (l.StartsWith("- Fix ")) l = $"- Fixed {l[6..]}";
                if (l.StartsWith("- Add ")) l = $"- Added {l[6..]}";
                if (l.StartsWith("- Update ")) l = $"- Updated {l[9..]}";
                return l;
            }).Join("\n");

            var tag = c.Tags.FirstOrDefault(t => t.Name.StartsWith('v') && Version.TryParse(t.Name[1..], out var _));
            if (tag != null)
            {   // New version
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

    public Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd) =>
        augmentedService.RemoveTagAsync(name, hasRemoteBranch, wd);

    public Task<R> SwitchToCommitAsync(string commitId, string wd) =>
        git.CheckoutAsync(commitId, wd);
}

