using System.Text;
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


    public IReadOnlyList<Branch> GetAllBranches(Repo repo) =>
        converter.ToBranches(repo.AugmentedRepo.Branches);

    public Branch AllBanchByName(Repo repo, string name) =>
        converter.ToBranch(repo.AugmentedRepo.BranchByName[name]);

    public Commit GetCommit(Repo repo, string commitId) =>
        converter.ToCommit(repo.AugmentedRepo.CommitById[commitId]);

    public IReadOnlyList<Commit> GetFilterCommits(Repo repo, string filter)
    {
        var sc = StringComparison.OrdinalIgnoreCase;
        var commits = repo.AugmentedRepo.Commits
         .Where(c =>
             c.Id.Contains(filter, sc) ||
             c.Subject.Contains(filter, sc) ||
             c.BranchName.Contains(filter, sc) ||
             c.Author.Contains(filter, sc));
        return converter.ToCommits(commits.ToList());

    }


    public IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId)
    {
        if (commitId == Repo.UncommittedId)
        {
            return new List<Branch>();
        }

        var c = repo.AugmentedRepo.CommitById[commitId];
        var ids = c.ChildIds.Concat(c.ParentIds);
        var branches = ids.Select(id =>
        {
            var cc = repo.AugmentedRepo.CommitById[id];

            // Get not shown branches of either child or parent commits.
            if (!repo.BranchByName.TryGetValue(cc.BranchName, out var branch))
            {
                return repo.AugmentedRepo.BranchByName[cc.BranchName];
            }

            return null;
        })
        .Where(b => b != null)
        .Cast<Augmented.Branch>();

        return converter.ToBranches(branches.ToList());
    }


    public Repo ShowBranch(Repo repo, string branchName, bool includeAmbiguous)
    {
        var branchNames = repo.Branches.Select(b => b.Name).Append(branchName);
        if (includeAmbiguous)
        {
            var branch = repo.AugmentedRepo.BranchByName[branchName];
            branchNames = branchNames.Concat(branch.AmbiguousBranchNames);
        }

        return viewRepoCreater.GetViewRepoAsync(repo.AugmentedRepo, branchNames.ToArray());
    }


    public Repo HideBranch(Repo repo, string name)
    {
        Log.Info($"Hide {name}");
        var branch = repo.AugmentedRepo.BranchByName[name];
        if (branch.RemoteName != "")
        {
            branch = repo.AugmentedRepo.BranchByName[branch.RemoteName];
        }

        var branchNames = repo.Branches
            .Where(b => b.Name != branch.Name &&
                !viewRepoCreater.IsFirstAncestorOfSecond(repo.AugmentedRepo, branch, repo.AugmentedRepo.BranchByName[b.Name]))
            .Select(b => b.Name)
            .ToArray();

        return viewRepoCreater.GetViewRepoAsync(repo.AugmentedRepo, branchNames);
    }

    public Task<R> FetchAsync(string wd) => augmentedService.FetchAsync(wd);


    public Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
          git.CommitAllChangesAsync(message, isAmend, wd);

    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd)
    {
        var diffTask = commitId == Repo.UncommittedId
            ? git.GetUncommittedDiff(wd)
            : git.GetCommitDiffAsync(commitId, wd);

        if (!Try(out var gitCommitDiff, out var e, await diffTask)) return e;

        return converter.ToCommitDiff(gitCommitDiff);
    }

    public async Task<R<CommitDiff>> GetPreviewMergeDiffAsync(string sha1, string sha2, string wd)
    {
        if (!Try(out var gitCommitDiff, out var e, await git.GetPreviewMergeDiffAsync(sha1, sha2, wd))) return e;

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
        Log.Info($"Pushing branch {name} ...");
        if (!Try(out var e, await augmentedService.PushMetaDataAsync(wd))) return e;

        return await git.PushBranchAsync(name, wd);
    }


    public Task<R> PullCurrentBranchAsync(string wd) =>
        git.PullCurrentBranchAsync(wd);

    public Task<R> PullBranchAsync(string name, string wd) =>
        git.PullBranchAsync(name, wd);

    public Task<R> SwitchToAsync(Repo repo, string branchName) =>
        augmentedService.SwitchToAsync(repo.AugmentedRepo, branchName);

    public Task<R> MergeBranchAsync(Repo repo, string branchName) =>
        augmentedService.MergeBranchAsync(repo.AugmentedRepo, branchName);

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

    public Task<R> UndoCommitAsync(string id, string wd) =>
        git.UndoCommitAsync(id, wd);

    public Task<R> UncommitLastCommitAsync(string wd) =>
        git.UncommitLastCommitAsync(wd);

    public Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setDisplayName) =>
        augmentedService.ResolveAmbiguityAsync(repo.AugmentedRepo, branchName, setDisplayName);

    public Task<R> SetBranchManuallyAsync(Repo repo, string commitId, string setDisplayName) =>
        augmentedService.SetBranchManuallyAsync(repo.AugmentedRepo, commitId, setDisplayName);

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
                        totalText.Append($"\n## [{nextTag}]\n{text}\n");
                    }
                    else
                    {
                        totalText.Append($"\n## [{nextTag}] - {nextTagDate.Iso()}\n{text}\n");
                    }
                }

                nextTag = tag.Name;
                nextTagDate = c.AuthorTime;
                text = "";
            }

            text += message;
        }

        return totalText.ToString();
    }

    public Task<R> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd) =>
        augmentedService.AddTagAsync(name, commitId, hasRemoteBranch, wd);

    public Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd) =>
        augmentedService.RemoveTagAsync(name, hasRemoteBranch, wd);

    public R MoveBranch(string name, int delta, Repo repo)
    {
        for (int i = 0; i < repo.Branches.Count; i++)
        {
            var branch = repo.Branches[i];
            if (branch.Name == name)
            {
                if (delta < 0 && i > 0)
                {
                    // Get branch before the one to move (skip possible remote branch)
                    var branch2 = repo.Branches[i];
                    if (i > 1 && branch2.Name == branch.RemoteName) branch2 = repo.Branches[i - 1];

                    repoState.Set(repo.Path, s => s.BranchOrders[branch.CommonName] = branch2.CommonName);
                    return R.Ok;
                }
                if (delta > 0 && i < repo.Branches.Count - 1)
                {
                    // Get branch next to the one to move (skip possible local branch)
                    var branch2 = repo.Branches[i + 1];
                    if (i < repo.Branches.Count - 2 && branch2.Name == branch.LocalName) branch2 = repo.Branches[i + 2];

                    repoState.Set(repo.Path, s => s.BranchOrders[branch2.CommonName] = branch.CommonName);
                    return R.Ok;
                }
            }
        }

        return R.Error("Failed to move branch");
    }
}

