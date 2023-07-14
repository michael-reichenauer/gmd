using System.Text;
using System.Text.RegularExpressions;
using gmd.Common;
using gmd.Cui.Common;
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
    readonly IRepoState repoState;

    public Server(
        IGit git,
        IAugmentedService augmentedService,
        IConverter converter,
        IViewRepoCreater viewRepoCreater,
        IRepoState repoState)
    {
        this.git = git;
        this.augmentedService = augmentedService;
        this.converter = converter;
        this.viewRepoCreater = viewRepoCreater;
        this.repoState = repoState;
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
        var branches = repo.Branches.Select(b => b.Name).ToArray();

        if (!Try(out var augmentedRepo, out var e,
            await augmentedService.UpdateRepoStatusAsync(repo.AugmentedRepo)))
        {
            return e;
        }

        return viewRepoCreater.GetViewRepoAsync(augmentedRepo, branches);
    }

    public async Task<R<Repo>> GetFilteredRepoAsync(Repo repo, string filter, int maxCount)
    {
        await Task.CompletedTask;
        return viewRepoCreater.GetFilteredViewRepoAsync(repo.AugmentedRepo, filter, maxCount);
    }


    public IReadOnlyList<Branch> GetAllBranches(Repo repo) =>
        converter.ToBranches(repo.AugmentedRepo.Branches.Values);

    public Branch AllBranchByName(Repo repo, string name) =>
        converter.ToBranch(repo.AugmentedRepo.Branches[name]);

    public Commit GetCommit(Repo repo, string commitId) =>
        converter.ToCommit(repo.AugmentedRepo.CommitById[commitId]);

    public IReadOnlyList<Commit> GetFilterCommits(Repo repo, string filter, int maxCount)
    {
        var t = Timing.Start();
        filter = filter.Trim();
        if (filter == "") return repo.Commits.Take(maxCount).ToList();

        if (filter == "$") return converter.ToCommits(
            repo.AugmentedRepo.Commits.Where(c => c.IsBranchSetByUser).Take(maxCount).ToList());

        if (filter == "*") return converter.ToCommits(
            repo.AugmentedRepo.Branches.Values.Where(b => b.AmbiguousTipId != "")
                .Select(b => repo.AugmentedRepo.CommitById[b.AmbiguousTipId])
                .Where(c => c.IsAmbiguousTip)
                .Take(maxCount)
                .ToList());

        var sc = StringComparison.OrdinalIgnoreCase;

        // I need extract all text enclosed by double quotes.
        var matches = Regex.Matches(filter, "\"([^\"]*)\"");
        var quoted = matches.Select(m => m.Groups[1].Value).ToList();

        // Replace all quoted text, where space is replaced by newlines to make it easier to split on space below. 
        var modifiedFilter = filter;
        quoted.ForEach(q => modifiedFilter = modifiedFilter.Replace($"\"{q}\"", q.Replace(" ", "\n")));

        // Split on space to get all AND parts of the text (and fix newlines to spaces again)
        var andParts = modifiedFilter.Split(' ').Where(p => p != "")
            .Select(p => p.Replace("\n", " "))      // Replace newlines back to spaces again 
            .ToList();

        // Find all commits matching all AND parts.
        var commits = repo.AugmentedRepo.Commits
            .Where(c => andParts.All(p =>
                c.Id.Contains(p, sc) ||
                c.Subject.Contains(p, sc) ||
                c.BranchName.Contains(p, sc) ||
                c.Author.Contains(p, sc) ||
                c.AuthorTime.IsoDate().Contains(p, sc) ||
                c.BranchNiceUniqueName.Contains(p, sc) ||
                c.Tags.Any(t => t.Name.Contains(p, sc))))
            .Take(maxCount);
        var result = converter.ToCommits(commits.ToList());
        Log.Info($"Filtered on '{filter}' => {result.Count} results {t}");
        return result;
    }


    public IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId)
    {
        if (commitId == Repo.UncommittedId)
        {
            return new List<Branch>();
        }

        var c = repo.AugmentedRepo.CommitById[commitId];
        var ids = c.AllChildIds.Concat(c.ParentIds);
        var branches = ids.Select(id =>
        {
            var cc = repo.AugmentedRepo.CommitById[id];

            // Get not shown branches of either child or parent commits.
            if (!repo.BranchByName.TryGetValue(cc.BranchName, out var branch))
            {
                return repo.AugmentedRepo.Branches[cc.BranchName];
            }

            return null;
        })
        .Where(b => b != null)
        .Cast<Augmented.Branch>();

        return converter.ToBranches(branches.ToList());
    }

    public IReadOnlyList<string> GetPossibleBranchNames(Repo repo, string commitId, int maxCount)
    {
        if (commitId == Repo.UncommittedId) return new List<string>();

        var specifiedCommit = repo.AugmentedRepo.CommitById[commitId];

        var branches = new Queue<string>();
        var branchesSeen = new HashSet<string>();
        var commitQueue = new Queue<Augmented.Commit>();
        var commitSeen = new HashSet<Augmented.Commit>();

        commitQueue.Enqueue(specifiedCommit);
        commitSeen.Add(specifiedCommit);

        while (commitQueue.Any() && branches.Count < maxCount)
        {
            var commit = commitQueue.Dequeue();
            var branch = repo.AugmentedRepo.Branches[commit.BranchName];

            if (!branchesSeen.Contains(branch.NiceName))
            {
                branches.Enqueue(branch.NiceName);
                branchesSeen.Add(branch.NiceName);
            }

            foreach (var id in commit.AllChildIds)
            {
                var child = repo.AugmentedRepo.CommitById[id];

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


    public Repo ShowBranch(Repo repo, string branchName, bool includeAmbiguous, ShowBranches show = ShowBranches.Specified)
    {
        var branchNames = repo.Branches.Select(b => b.Name).Append(branchName);
        if (includeAmbiguous)
        {
            var branch = repo.AugmentedRepo.Branches[branchName];
            branchNames = branchNames.Concat(branch.AmbiguousBranchNames);
        }

        return viewRepoCreater.GetViewRepoAsync(repo.AugmentedRepo, branchNames.ToArray(), show);
    }


    public Repo HideBranch(Repo repo, string name, bool hideAllBranches = false)
    {
        Log.Info($"Hide {name}, HideAllBranches: {hideAllBranches}");

        if (hideAllBranches) return viewRepoCreater.GetViewRepoAsync(repo.AugmentedRepo, new[] { "main" });

        var branch = repo.AugmentedRepo.Branches[name];
        if (branch.RemoteName != "")
        {
            branch = repo.AugmentedRepo.Branches[branch.RemoteName];
        }

        var branchNames = repo.Branches
            .Where(b => b.Name != branch.Name && !b.AncestorNames.Contains(branch.Name))
            .Select(b => b.Name)
            .ToArray();

        return viewRepoCreater.GetViewRepoAsync(repo.AugmentedRepo, branchNames);
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


    public async Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd)
    {
        if (!Try(out var gitCommitDiffs, out var e, await git.GetFileDiffAsync(path, wd))) return e;
        return converter.ToCommitDiffs(gitCommitDiffs);
    }


    public Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd) =>
      augmentedService.CreateBranchAsync(repo.AugmentedRepo, newBranchName, isCheckout, wd);

    public Task<R> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd) =>
        augmentedService.CreateBranchFromCommitAsync(repo.AugmentedRepo, newBranchName, sha, isCheckout, wd);

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


    public Task<R> PullCurrentBranchAsync(string wd) =>
        git.PullCurrentBranchAsync(wd);

    public Task<R> PullBranchAsync(string name, string wd) =>
        git.PullBranchAsync(name, wd);

    public Task<R> SwitchToAsync(Repo repo, string branchName) =>
        augmentedService.SwitchToAsync(repo.AugmentedRepo, branchName);

    public async Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName)
    {
        if (!Try(out var commits, out var e, await augmentedService.MergeBranchAsync(repo.AugmentedRepo, branchName))) return e;
        return converter.ToCommits(commits).ToList();
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

    public Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName) =>
        augmentedService.ResolveAmbiguityAsync(repo.AugmentedRepo, branchName, setHumanName);

    public Task<R> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName) =>
        augmentedService.SetBranchManuallyAsync(repo.AugmentedRepo, commitId, setHumanName);

    public Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId) =>
        augmentedService.UnresolveAmbiguityAsync(repo.AugmentedRepo, commitId);

    public Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) =>
        git.GetFileAsync(reference, wd);

    public async Task<R> CloneAsync(string uri, string path, string wd)
    {
        using (Timing.Start()) return await git.CloneAsync(uri, path, wd);
    }

    public Task<R> StashAsync(string wd) => git.StashAsync(wd);

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
        foreach (Commit c in repo.Commits)
        {
            var message = c.Message;
            var parts = c.Message.Split('\n');
            if (c.ParentIds.Count() > 1 && parts.Length > 2 && parts[1].Trim() == "")
            {
                message = string.Join('\n', parts.Skip(2));
            }
            else if (parts.Length == 1)
            {
                message = $"- {parts[0]}";
            }

            var tag = c.Tags.FirstOrDefault(t => t.Name.StartsWith('v') && Version.TryParse(t.Name.Substring(1), out var _));
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

