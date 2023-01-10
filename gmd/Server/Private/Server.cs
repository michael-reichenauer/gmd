using gmd.Git;
using gmd.Server.Private.Augmented;

namespace gmd.Server.Private;

[SingleInstance]
class Server : IServer
{
    private readonly IGit git;
    private readonly IAugmentedService augmentedService;
    private readonly IConverter converter;
    private readonly IViewRepoCreater viewRepoCreater;

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


    public Task<R> CommitAllChangesAsync(string message, string wd) =>
          git.CommitAllChangesAsync(message, wd);

    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd)
    {
        var diffTask = commitId == Repo.UncommittedId
            ? git.GetUncommittedDiff(wd)
            : git.GetCommitDiffAsync(commitId, wd);

        if (!Try(out var gitCommitDiff, out var e, await diffTask)) return e;

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

    public Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId) =>
        augmentedService.UnresolveAmbiguityAsync(repo.AugmentedRepo, commitId);

    public Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) =>
        git.GetFileAsync(reference, wd);

    public async Task<R> CloneAsync(string uri, string path, string wd)
    {
        using (Timing.Start()) return await git.CloneAsync(uri, path, wd);
    }
}

